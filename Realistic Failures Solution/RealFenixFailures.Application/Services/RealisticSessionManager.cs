using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using System.ComponentModel;

namespace RealFenixFailures.Application.Services;

/// <summary>
/// Owns the realistic-session lifecycle: start/stop/pause/resume, the periodic evaluation timer,
/// and event dispatch. All failure-evaluation logic is delegated to a <see cref="RealisticSessionEngine"/>
/// instance that is created when a session starts and discarded when it stops.
/// </summary>
public class RealisticSessionManager : IRealisticSessionManager {
    #region Fields
    private readonly ILogger<RealisticSessionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISimulatorConnectionService _simulator;
    private readonly IFlightEventDetector _flightEventDetector;

    private RealisticSessionEngine? _engine;
    private IServiceScope? _sessionScope;
    private PeriodicTimer? _evaluationTimer;
    private CancellationTokenSource? _evaluationCts;
    private Task? _evaluationLoopTask;
    private bool _isPaused;
    private readonly TimeSpan _evaluationInterval;
    private const int EvaluationIntervalSeconds = 30;
    #endregion

    #region Events
    public event EventHandler<FailureTriggeredEventArgs>? FailureTriggered;
    public event EventHandler<SessionErrorEventArgs>? SessionError;
    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
    #endregion

    #region Properties
    public RealisticSession? SessionState => _engine?.Session;

    public bool IsPaused {
        get => _isPaused;
        private set {
            if (_isPaused != value) {
                _isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
            }
        }
    }
    #endregion

    #region Constructor
    public RealisticSessionManager(
        ILogger<RealisticSessionManager> logger,
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        ISimulatorConnectionService simulatorConnectionService,
        IFlightEventDetector flightEventDetector,
        TimeSpan? evaluationInterval = null) {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _scopeFactory = scopeFactory;
        _simulator = simulatorConnectionService;
        _flightEventDetector = flightEventDetector;
        _evaluationInterval = evaluationInterval ?? TimeSpan.FromSeconds(EvaluationIntervalSeconds);
    }
    #endregion

    #region Session Commands
    public async Task<ServiceResult<string>> StartNewSessionAsync(RealisticModeContext context, CancellationToken ct) {
        if (SessionState != null) throw new InvalidOperationException();
        IServiceScope? scope = null;
        try {
            scope = _scopeFactory.CreateScope();
            var engine = new RealisticSessionEngine(
                _simulator,
                _loggerFactory.CreateLogger<RealisticSessionEngine>(),
                scope.ServiceProvider.GetRequiredService<IFlightSessionService>(),
                scope.ServiceProvider.GetRequiredService<IPresetService>(),
                scope.ServiceProvider.GetRequiredService<IFailureTrigger>(),
                _flightEventDetector,
                scope.ServiceProvider.GetRequiredService<ITriggeredFailureRepository>());

            var result = await engine.TryStartSession(context, ct);
            if (result.Started == false) {
                scope.Dispose();
                scope = null;
                return ServiceResult<string>.Fail(new InvalidOperationException(), result.Text);
            }

            var timer = new PeriodicTimer(_evaluationInterval);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopTask = Task.Run(() => RunEvaluationLoopAsync(timer, cts.Token), cts.Token);

            _engine = engine;
            _sessionScope = scope;
            scope = null;
            _evaluationTimer = timer;
            _evaluationCts = cts;
            _evaluationLoopTask = loopTask;
            IsPaused = false;
            _logger.LogInformation("Realistic session started for aircraft {Registration}", context.Aircraft.Registration);
            return ServiceResult<string>.Ok($"Realistic session started for aircraft {context.Aircraft.Registration}");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error starting realistic session");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Failed to start realistic session", Exception = ex });
            return ServiceResult<string>.Fail(ex, "Failed to start realistic session");
        } finally {
            scope?.Dispose();
        }
    }

    public async Task StopAsync(CancellationToken ct) {
        if (_engine == null) return;
        try {
            await StopLoopAsync();
            await _engine!.StopSession(ct);
        } finally {
            _engine = null;
            _sessionScope?.Dispose();
            _sessionScope = null;
            IsPaused = false;
        }
        _logger.LogInformation("Realistic session stopped");
    }

    public async Task PauseAsync(CancellationToken ct) {
        if (_engine == null || IsPaused) return;
        await StopLoopAsync();
        await DisarmActiveFailuresForPauseAsync(ct);
        IsPaused = true;
        _logger.LogInformation("Realistic session paused");
    }

    public async Task ResumeAsync(CancellationToken ct, bool restoreFailures = false) {
        if (_engine == null || !IsPaused) return;
        try {
            if (restoreFailures) {
                await RestoreFailuresAfterPauseAsync(ct);
            }
            var timer = new PeriodicTimer(_evaluationInterval);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopTask = Task.Run(() => RunEvaluationLoopAsync(timer, cts.Token), cts.Token);

            _evaluationTimer?.Dispose();
            _evaluationCts?.Dispose();
            _evaluationTimer = timer;
            _evaluationCts = cts;
            _evaluationLoopTask = loopTask;
            IsPaused = false;
            _logger.LogInformation("Realistic session resumed {Restore}", restoreFailures ? "con restauración de fallas" : "sin restauración");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error resuming realistic session");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Failed to resume session", Exception = ex });
        }
    }

    /// <summary>
    /// Desarma en Fenix las fallas activas de la sesión durante la pausa. El documento objetivo fija que la pausa
    /// debe DESARMAR (el piloto sigue volando sin fallas) aunque el motor siga pensando en memoria.
    /// Fallas que no se desarman se reportan en log (no se aborta la pausa).
    /// </summary>
    private async Task DisarmActiveFailuresForPauseAsync(CancellationToken ct) {
        if (_sessionScope is null || _engine?.Session is null) return;
        var repo = _sessionScope.ServiceProvider.GetService<ITriggeredFailureRepository>();
        if (repo is null) return;

        var active = await repo.GetActiveForSessionAsync(_engine.Session.Session.Id, ct);
        var failedIds = _engine.Session.FailedFailures.Select(f => f.FenixId).ToHashSet();
        var armedIds = _engine.Session.ArmedFailures.Select(f => f.FenixId).ToHashSet();
        var toDisarm = active.Where(x => failedIds.Contains(x.FenixFailureId) || armedIds.Contains(x.FenixFailureId)).ToList();

        foreach (var failure in toDisarm) {
            try {
                var ok = await _simulator.DisarmFailureAsync(failure.FenixFailureId, ct);
                if (ok) {
                    _logger.LogInformation("Pause: failure disarmed in Fenix: {Id}", failure.FenixFailureId);
                } else {
                    _logger.LogWarning("Pause: failed to disarm {Id}", failure.FenixFailureId);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Pause: error disarming {Id}", failure.FenixFailureId);
            }
        }
    }

    /// <summary>
    /// Reconstruye y envía a Fenix el armado de las fallas que estaban en curso antes de la pausa, cuando
    /// <c>restoreFailures=true</c>. Es una reconstrucción best-effort de la condición desde los campos string
    /// de la BD; si la condición no se puede reconstruir, la falla NO se re-arma y se registra en log.
    /// </summary>
    private async Task RestoreFailuresAfterPauseAsync(CancellationToken ct) {
        if (_sessionScope is null || _engine?.Session is null) return;
        var repo = _sessionScope.ServiceProvider.GetService<ITriggeredFailureRepository>();
        if (repo is null) return;

        var active = await repo.GetActiveForSessionAsync(_engine.Session.Session.Id, ct);
        foreach (var failure in active) {
            var armRequest = BuildArmRequestFrom(failure);
            if (armRequest is null) {
                _logger.LogWarning("Resume: cannot rebuild arm condition for {Id}; skip", failure.FenixFailureId);
                continue;
            }
            try {
                var dispatched = await DispatchArmAsync(armRequest, ct);
                if (dispatched) {
                    _logger.LogInformation("Resume: failure re-armed in Fenix: {Id}", failure.FenixFailureId);
                } else {
                    _logger.LogWarning("Resume: failed to re-arm {Id}", failure.FenixFailureId);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Resume: error re-arming {Id}", failure.FenixFailureId);
            }
        }
    }

    private FenixArmFailureRequest? BuildArmRequestFrom(TriggeredFailure failure) {
        // Reconstruir condición desde el TriggeredFailure:
        // si fallaba (State==Active) se reactiva; si estaba armada preparada se reconstruye la condición.
        int? Ias = ParseInt(failure.Ias);
        int? Alt = ParseInt(failure.AboveAltitude);
        int? Altb = ParseInt(failure.BelowAltitude);
        int? Time = ParseInt(failure.Time);
        bool hadAnyCondition = Ias.HasValue || Alt.HasValue || Altb.HasValue || Time.HasValue || !string.IsNullOrEmpty(failure.AfterEvent);
        var condition = hadAnyCondition
            ? new FenixArmFailureConditionRequest { Ias = Ias, Alt = Alt, Altb = Altb, Time = Time, AfterEvent = string.IsNullOrEmpty(failure.AfterEvent) ? null : failure.AfterEvent, AfterEventSeconds = ParseInt(failure.AfterEventSeconds) }
            : null;
        // failed=true si estaba activa o confirmada; si simplemente estaba preparada (Sent), se deja armada.
        bool shouldBeFailed = failure.State is FailureLifecycleState.Active;
        return new FenixArmFailureRequest(failure.FenixFailureId, shouldBeFailed, condition);
    }

    private static int? ParseInt(string? value) => int.TryParse(value, out var n) ? n : null;

    private Task<bool> DispatchArmAsync(FenixArmFailureRequest request, CancellationToken ct) {
        var dispatcher = _sessionScope?.ServiceProvider.GetService<IFenixFailureApiDispatcher>();
        if (dispatcher != null) {
            return dispatcher.ArmFailureAsync(request, ct);
        }
        return Task.FromResult(false);
    }
    #endregion

    #region Loop Session Timer
    /// <summary>
    /// Resultado de <see cref="EvaluateAndExecuteAsync"/>: indica si el loop debe continuar y, si terminó por un
    /// fallo fatal, la excepción que lo causó (para que el loop limpie el estado y notifique <c>SessionError</c>).
    /// </summary>
    private sealed record EvaluationLoopSignal(bool ShouldContinue, Exception? FatalError) {
        public static EvaluationLoopSignal Continue() => new(true, null);
        public static EvaluationLoopSignal Stop() => new(false, null);
        public static EvaluationLoopSignal StopFatal(Exception error) => new(false, error);
    }

    private async Task RunEvaluationLoopAsync(PeriodicTimer timer, CancellationToken ct) {
        try {
            while (await timer.WaitForNextTickAsync(ct) && !ct.IsCancellationRequested) {
                var signal = await EvaluateAndExecuteAsync(ct);
                if (!signal.ShouldContinue) {
                    if (signal.FatalError is not null) {
                        CleanUpAfterFatalEvaluation(signal.FatalError);
                    }
                    return;
                }
            }
        } catch (OperationCanceledException) {
            _logger.LogInformation("Evaluation loop canceled");
        } catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error in evaluation loop");
            CleanUpAfterFatalEvaluation(ex);
        }
    }

    /// <summary>
    /// Cierre seguro ante un fallo fatal de evaluación. Se ejecuta DENTRO de la task del loop (nunca se auto-espera),
    /// por lo que no invoca <c>StopLoopAsync</c> ni <c>StopAsync</c> (que esperarían <c>_evaluationLoopTask</c>).
    /// Descarta engine, scope, timer/cts y deja el manager sin sesión ni modo aparentemente activo.
    /// </summary>
    private void CleanUpAfterFatalEvaluation(Exception fatalError) {
        _evaluationCts?.Cancel();
        _evaluationCts?.Dispose();
        _evaluationCts = null;
        _evaluationTimer?.Dispose();
        _evaluationTimer = null;
        _evaluationLoopTask = null;
        _engine = null;
        _sessionScope?.Dispose();
        _sessionScope = null;
        IsPaused = false;
        _logger.LogWarning(fatalError, "Realistic session terminated after a fatal evaluation error");
        SessionError?.Invoke(this, new SessionErrorEventArgs {
            Message = "Error fatal durante la evaluación; la sesión realista se cerró",
            Exception = fatalError
        });
    }

    private async Task StopLoopAsync() {
        _evaluationCts?.Cancel();
        var loopTask = _evaluationLoopTask;
        _evaluationLoopTask = null;
        if (loopTask != null) {
            try {
                await loopTask;
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Evaluation loop exited with an unexpected error");
            }
        }
        _evaluationTimer?.Dispose();
        _evaluationTimer = null;
        _evaluationCts?.Dispose();
        _evaluationCts = null;
    }
    #endregion

    #region Main Timer Tick Session Evaluator
    private async Task<EvaluationLoopSignal> EvaluateAndExecuteAsync(CancellationToken ct) {
        if (SessionState == null || _engine == null) return EvaluationLoopSignal.Stop();
        try {
            var result = await _engine.EvaluateAsync(ct);

            if (result.ShouldPauseSession) {
                _evaluationCts?.Cancel();
                IsPaused = true;
                _logger.LogInformation("Realistic session paused (simulator data unavailable)");
                return EvaluationLoopSignal.Stop();
            }

            foreach (var triggered in result.TriggeredFailures) {
                FailureTriggered?.Invoke(this, new FailureTriggeredEventArgs {
                    FenixFailureId = triggered.FenixFailureId,
                    Description = triggered.Description,
                    TriggeredAtUtc = triggered.TriggeredAtUtc
                });
            }

            return EvaluationLoopSignal.Continue();
        } catch (OperationCanceledException) {
            // Pausa/stop manual: termina el loop sin limpiar el estado (la sesión sigue para pausar/reanudar).
            return EvaluationLoopSignal.Stop();
        } catch (Exception ex) {
            _logger.LogError(ex, "Error during evaluation");
            return EvaluationLoopSignal.StopFatal(ex);
        }
    }
    #endregion

    #region Privates

    #endregion
}
