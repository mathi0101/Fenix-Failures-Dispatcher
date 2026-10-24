using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;
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
    private readonly RealisticModeSettings? _realisticModeSettings;
    private readonly Func<IFailureRuleEvaluator, RealisticFailurePolicy>? _policyFactory;

    private RealisticSessionEngine? _engine;
    private IServiceScope? _sessionScope;
    private CancellationTokenSource? _evaluationCts;
    private Task? _evaluationLoopTask;
    private readonly ScaledTimeProvider? _clock;
    private DateTimeOffset? _sessionStartedAtScenario;
    private bool _isPaused;
    private readonly TimeSpan _evaluationInterval;
    private const int EvaluationIntervalSeconds = 30;
    #endregion

    #region Events
    public event EventHandler<FailureTriggeredEventArgs>? FailureTriggered;
    public event EventHandler<FailureStateChangedEventArgs>? FailureStateChanged;
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

    /// <summary>Resultado consolidado de la última sesión cerrada (modal de resultados §13); nulo si nunca se cerró una.</summary>
    public SessionResultDto? LastSessionResult { get; private set; }

    public int PreparedCount => _engine?.PreparedCount ?? 0;

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
        TimeSpan? evaluationInterval = null,
        IOptions<FailureEngineSettings>? settings = null,
        Func<IFailureRuleEvaluator, RealisticFailurePolicy>? policyFactory = null,
        ScaledTimeProvider? simulatedClock = null) {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _scopeFactory = scopeFactory;
        _simulator = simulatorConnectionService;
        _flightEventDetector = flightEventDetector;
        _realisticModeSettings = settings?.Value?.RealisticMode ?? new RealisticModeSettings();
        _policyFactory = policyFactory;
        _clock = simulatedClock;
        var intervalSeconds = settings?.Value?.RealisticMode.EvaluationIntervalSeconds ?? EvaluationIntervalSeconds;
        _evaluationInterval = evaluationInterval ?? TimeSpan.FromSeconds(intervalSeconds);
    }
    #endregion

    #region Session Commands
    public async Task<ServiceResult<string>> StartNewSessionAsync(RealisticModeContext context, CancellationToken ct) {
        if (SessionState != null) throw new InvalidOperationException();
        IServiceScope? scope = null;
        try {
            scope = _scopeFactory.CreateScope();
            var evaluator = scope.ServiceProvider.GetService<IFailureRuleEvaluator>() ?? new FailureRuleEvaluator();
            var policy = _policyFactory is null
                ? new RealisticFailurePolicy(evaluator, utcNow: _clock is null ? null : () => _clock.GetUtcNow().UtcDateTime)
                : _policyFactory(evaluator);
            var engine = new RealisticSessionEngine(
                _simulator,
                _loggerFactory.CreateLogger<RealisticSessionEngine>(),
                scope.ServiceProvider.GetRequiredService<IFlightSessionService>(),
                scope.ServiceProvider.GetRequiredService<IPresetService>(),
                _flightEventDetector,
                scope.ServiceProvider.GetRequiredService<IFlightPlanRepository>(),
                scope.ServiceProvider.GetRequiredService<ITriggeredFailureRepository>(),
                settings: _realisticModeSettings,
                policy: policy,
                fenixFailureDefinitions: scope.ServiceProvider.GetService<IFenixFailureDefinitionRepository>(),
                aircraftSystemWearRepository: scope.ServiceProvider.GetRequiredService<IAircraftSystemWearRepository>());
            engine.PreparedQueueChanged += Engine_PreparedQueueChanged;

            var result = await engine.TryStartSession(context, ct);
            if (result.Started == false) {
                scope.Dispose();
                scope = null;
                return ServiceResult<string>.Fail(new InvalidOperationException(), result.Text);
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopTask = Task.Run(() => RunEvaluationLoopAsync(cts.Token), cts.Token);

            _engine = engine;
            _sessionScope = scope;
            scope = null;
            _sessionStartedAtScenario = _clock?.GetUtcNow();
            _evaluationCts = cts;
            _evaluationLoopTask = loopTask;
            IsPaused = false;
            _logger.LogInformation("Realistic session started for aircraft {Registration} (escala de tiempo {Scale}x)",
                context.Aircraft.Registration, _clock?.Scale ?? 1.0);
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
        int? sessionId = null;
        try {
            sessionId = _engine.Session?.Session.Id;
            await StopLoopAsync();
            await _engine!.StopSession(ct);
            // Resultado consolidado de la sesión recién cerrada, para el modal de resultados (§13).
            if (sessionId is int sid && _sessionScope != null) {
                var sessionService = _sessionScope.ServiceProvider.GetService<IFlightSessionService>();
                if (sessionService != null) LastSessionResult = await sessionService.GetResultAsync(sid, ct);
            }
        } finally {
            _engine.PreparedQueueChanged -= Engine_PreparedQueueChanged;
            _engine = null;
            _sessionScope?.Dispose();
            _sessionScope = null;
            _sessionStartedAtScenario = null;
            IsPaused = false;
            OnPropertyChanged(nameof(PreparedCount));
        }
        _logger.LogInformation("Realistic session stopped");
    }

    public Task<int> DiscardPreparedAsync(CancellationToken ct) {
        var engine = _engine;
        if (engine is null) return Task.FromResult(0);
        return Task.FromResult(engine.DiscardPrepared());
    }

    public async Task PauseAsync(CancellationToken ct) {
        if (_engine == null || IsPaused) return;
        // Marcar pausado ANTES de detener el loop: bloquea cualquier tick en curso/encolado
        // de elegir fallas nuevas y deja IsPaused=true aunque el desarme tarde.
        IsPaused = true;
        _logger.LogDebug("Realistic sesión {SessionId}: pausa solicitada, detener evaluación y desarmar fallas activas", _engine.Session?.Session.Id);
        await StopLoopAsync();
        await DisarmActiveFailuresForPauseAsync(ct);
        _logger.LogInformation("Realistic session paused");
    }

    public async Task ResumeAsync(CancellationToken ct, bool restoreFailures = false) {
        if (_engine == null || !IsPaused) return;
        _logger.LogDebug("Realistic sesión {SessionId}: reanudación solicitada, restaurar fallas {Restore}", _engine.Session?.Session.Id, restoreFailures);
        try {
            if (restoreFailures) {
                await RestoreFailuresAfterPauseAsync(ct);
            }
            _engine.ResetCadence();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var loopTask = Task.Run(() => RunEvaluationLoopAsync(cts.Token), cts.Token);

            _evaluationCts?.Dispose();
            _evaluationCts = cts;
            _evaluationLoopTask = loopTask;
            IsPaused = false;
            _logger.LogDebug("Realistic sesión {SessionId}: reanudación completada y timer iniciado, intervalo escenario {Interval}, escala {Scale}x", _engine.Session?.Session.Id, _evaluationInterval, _clock?.Scale ?? 1.0);
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
                    _logger.LogDebug("Realistic sesión {SessionId}: desarme por pausa confirmado para {FailureId}", _engine.Session.Session.Id, failure.FenixFailureId);
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
                    _logger.LogDebug("Realistic sesión {SessionId}: restauración confirmada para {FailureId}, failed {Failed}, condición {@Condition}",
                        _engine.Session.Session.Id, failure.FenixFailureId, armRequest.Failed, armRequest.FailureCondition);
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

    private async Task RunEvaluationLoopAsync(CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                await WaitForNextEvaluationTickAsync(ct);
                if (ct.IsCancellationRequested)
                    break;

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
    /// Tick de evaluación sobre el reloj de simulación: espera el intervalo en tiempo de escenario.
    /// Con escala &gt; 1 (o con saltos de escenario) el próximo tick ocurre proporcionalmente antes en
    /// tiempo de pared; los cambios de escala en vivo se reflejan en el tick siguiente.
    /// </summary>
    private async Task WaitForNextEvaluationTickAsync(CancellationToken ct) {
        var now = _clock?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        var due = now + _evaluationInterval;
        while (true) {
            now = _clock?.GetUtcNow() ?? DateTimeOffset.UtcNow;
            var remaining = due - now;
            if (remaining <= TimeSpan.Zero)
                return;

            var scale = _clock?.Scale ?? 1.0;
            var wallChunkMs = remaining.TotalMilliseconds / Math.Max(scale, ScaledTimeProvider.MinScale);
            var chunk = TimeSpan.FromMilliseconds(Math.Clamp(wallChunkMs, 15, 250));
            await Task.Delay(chunk, ct);
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
        _evaluationLoopTask = null;
        _engine = null;
        _sessionScope?.Dispose();
        _sessionScope = null;
        IsPaused = false;
        _logger.LogCritical(fatalError, "Realistic session terminated after a fatal evaluation error");
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
        _evaluationCts?.Dispose();
        _evaluationCts = null;
    }
    #endregion

    #region Main Timer Tick Session Evaluator
    private async Task<EvaluationLoopSignal> EvaluateAndExecuteAsync(CancellationToken ct) {
        if (IsPaused || SessionState == null || _engine == null) return EvaluationLoopSignal.Stop();
        try {
            var result = await _engine.EvaluateAsync(ct);
            if (result.TriggeredFailures.Count > 0 || result.StateChanges.Count > 0)
                _logger.LogDebug("Realistic sesión {SessionId}: evaluación con acciones, envíos {SentCount}, cambios de estado {StateCount}, escala {Scale}x, {ScenarioMark}",
                    _engine.Session?.Session.Id, result.TriggeredFailures.Count, result.StateChanges.Count, _clock?.Scale ?? 1.0, ScenarioMark());

            if (result.ShouldPauseSession) {
                _evaluationCts?.Cancel();
                IsPaused = true;
                _logger.LogInformation("Realistic session paused (simulator data unavailable)");
                return EvaluationLoopSignal.Stop();
            }

            foreach (var triggered in result.TriggeredFailures) {
                _logger.LogInformation("Falla {FenixId} disparada {ScenarioMark} (fase {Phase}, escala {Scale}x)",
                    triggered.FenixFailureId, ScenarioMark(), _flightEventDetector.CurrentPhase, _clock?.Scale ?? 1.0);
                FailureTriggered?.Invoke(this, new FailureTriggeredEventArgs {
                    FenixFailureId = triggered.FenixFailureId,
                    Description = triggered.Description,
                    TriggeredAtUtc = triggered.TriggeredAtUtc
                });
            }

            foreach (var change in result.StateChanges) {
                FailureStateChanged?.Invoke(this, new FailureStateChangedEventArgs {
                    FenixFailureId = change.FenixFailureId,
                    Description = change.Description,
                    NewState = change.NewState,
                    Severity = change.Severity,
                    OccurredAtUtc = change.OccurredAtUtc
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
    /// <summary>Marcador de tiempo de escenario (T+hh:mm:ss desde el inicio de la sesión) para trazabilidad en logs.</summary>
    private string ScenarioMark() {
        if (_clock is null || _sessionStartedAtScenario is not DateTimeOffset started)
            return string.Empty;
        return $"a T+{_clock.GetUtcNow() - started:hh\\:mm\\:ss}";
    }

    private void Engine_PreparedQueueChanged(object? sender, EventArgs e) {
        OnPropertyChanged(nameof(PreparedCount));
    }
    #endregion
}
