using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
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
        IsPaused = true;
        _logger.LogInformation("Realistic session paused");
    }

    public async Task ResumeAsync(CancellationToken ct) {
        if (_engine == null || !IsPaused) return;
        try {
            var timer = new PeriodicTimer(_evaluationInterval);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopTask = Task.Run(() => RunEvaluationLoopAsync(timer, cts.Token), cts.Token);

            _evaluationTimer?.Dispose();
            _evaluationCts?.Dispose();
            _evaluationTimer = timer;
            _evaluationCts = cts;
            _evaluationLoopTask = loopTask;
            IsPaused = false;
            _logger.LogInformation("Realistic session resumed");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error resuming realistic session");
            SessionError?.Invoke(this, new SessionErrorEventArgs { Message = "Failed to resume session", Exception = ex });
        }
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
