using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using System.ComponentModel;

namespace RealFenixFailures.Application.Session;

/// <summary>
/// Encapsulates all realistic-failure evaluation logic for a single active session.
/// A new instance is created by <c>RealisticSessionManager</c> when a session starts and is
/// discarded when the session stops. The engine owns the failure-selection, probability and
/// state-mutation logic; the manager only owns the session lifecycle, the timer and event dispatch.
/// </summary>
/// <remarks>
/// Not sealed so it can be subclassed/mocked in tests.
/// </remarks>
public class RealisticSessionEngine : INotifyPropertyChanged {

    #region Constants
    /// <summary>Base probability that a given candidate failure is triggered on a tick where it is evaluated.</summary>
    private const double BaseProbability = 0.10;

    /// <summary>Minimum time (seconds) that must elapse since the last triggered failure before another can fire.</summary>
    private const int MinCooldownSeconds = 60;

    /// <summary>Maximum number of failures that may be active simultaneously in a session.</summary>
    private const int MaxSimultaneousActiveFailures = 3;
    #endregion

    #region Fields
    private readonly ISimulatorConnectionService _simulator;
    private readonly ILogger<RealisticSessionEngine> _logger;
    private readonly IFlightSessionService _sessionService;
    private readonly IPresetService _presetService;
    private readonly IFailureTrigger _failureTrigger;
    private readonly IFlightEventDetector _flightEventDetector;
    private readonly ITriggeredFailureRepository _triggeredFailureRepository;
    private readonly Random _random = Random.Shared;
    private readonly object _eventQueueLock = new();
    private readonly Queue<FlightEventDto> _pendingFlightEvents = new();
    private bool _subscribedToFlightEvents;


    private RealisticSession? _session;

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    public RealisticSession? Session {
        get => _session;
        private set => _session = value;
    }
    public bool IsRunning => Session != null;

    #endregion

    #region Constructor
    public RealisticSessionEngine(
        ISimulatorConnectionService simulator,
        ILogger<RealisticSessionEngine> logger,
        IFlightSessionService sessionService,
        IPresetService presetService,
        IFailureTrigger failureTrigger,
        IFlightEventDetector flightEventDetector,
        ITriggeredFailureRepository triggeredFailureRepository) {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionService = sessionService;
        _presetService = presetService;
        _failureTrigger = failureTrigger ?? throw new ArgumentNullException(nameof(failureTrigger));
        _flightEventDetector = flightEventDetector ?? throw new ArgumentNullException(nameof(flightEventDetector));
        _triggeredFailureRepository = triggeredFailureRepository ?? throw new ArgumentNullException(nameof(triggeredFailureRepository));
    }
    #endregion

    #region Public API

    public async Task<StartSessionResult> TryStartSession(RealisticModeContext context, CancellationToken ct) {
        var conn = await _simulator.GetConnectionStatusAsync(ct);
        var data = await _simulator.GetSimulatorData(ct);

        if (!conn.IsSimConnectConnected || !conn.IsFenixConnected)
            return new StartSessionResult(false, "Simulator connection not detected yet");

        if (!data.IsSuccess || data.Value is null)
            return new StartSessionResult(false, "Simulator data unavailable");

        var snapshot = data.Value;
        bool isAircraftParkedAndEnginesOff = snapshot.IsOnGround && !(snapshot.Engine1IsRunning || snapshot.Engine2IsRunning);
        if (!isAircraftParkedAndEnginesOff)
            return new StartSessionResult(false, "Aircraft must be on gate with all engines off");

        // Cargar presets ANTES de persistir la sesión: si la carga falla, no queda sesión persistida huérfana.
        var presets = (await _presetService.GetRealisticPresetsAsync(ct)).ToList();
        var newSession = await _sessionService.StartNewAsync(context.RiskLevel, context.Aircraft, ct);
        Session = new RealisticSession(newSession, context.Aircraft) {
            AvailablePresets = presets
        };

        try {
            await _sessionService.AddEventLogAsync(newSession.Id, SessionEventCategory.SessionStarted, "SESSION_STARTED", "Sesión realista iniciada", ComplexFlightPhaseEnum.Unknown, DateTime.UtcNow, ct);
        } catch (Exception) {
            try {
                await _sessionService.StopAsync(newSession.Id, DateTime.UtcNow, CancellationToken.None);
            } catch (Exception stopEx) {
                _logger.LogWarning(stopEx, "Failed to close session {SessionId} after start-event registration failure", newSession.Id);
            }
            Session = null;
            throw;
        }

        SubscribeToFlightEvents();

        return new StartSessionResult(true, string.Empty);
    }

    public async Task StopSession(CancellationToken ct) {
        if (!IsRunning) return;
        try {
            if (Session!.Session.FinishedAt == null) {
                try {
                    await _sessionService.AddEventLogAsync(Session.Session.Id, SessionEventCategory.SessionStopped, "SESSION_STOPPED", "Sesión realista detenida", Session.CurrentFlightPhase, DateTime.UtcNow, ct);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to register stop event for session {SessionId}", Session.Session.Id);
                }
                await _sessionService.StopAsync(Session.Session.Id, DateTime.UtcNow, ct);
            }
            Session = null;
        } finally {
            ClearPendingFlightEvents();
            UnsubscribeFromFlightEvents();
        }
    }

    /// <summary>
    /// Evaluates the current session state against the simulator once (one timer tick).
    /// Triggers at most a single failure per tick. Returns an <see cref="EvaluationResult"/>
    /// describing what happened so the manager can raise events / pause the session.
    /// </summary>
    public async Task<EvaluationResult> EvaluateAsync(CancellationToken ct) {
        if (!IsRunning) return EvaluationResult.Disconnected();
        try {
            (SimulatorAircraftStateSnapshot rawData, IReadOnlyList<FenixFailureDto> failures) = await GetSimulatorData(ct);

            if (!rawData.HasSimulatorData) {
                _logger.LogDebug("Skipping realistic evaluation tick: no simulator data in snapshot (HasSimulatorData=false)");
                return EvaluationResult.NoAction();
            }

            var currentPhase = _flightEventDetector.ProcessSnapshot(rawData);
            await DrainFlightEventsAsync(currentPhase, ct);
            Session!.ProcessSimData(rawData, failures, currentPhase);
            await ReconcileFailureLifecycleAsync(currentPhase, ct);
        } catch (SimulatorDataUnavailableException ex) {
            _logger.LogWarning("Error al recibir datos en EvaluateAsync, Ex: {a}", ex.Message);
            return EvaluationResult.Disconnected();
        } catch (OperationCanceledException) {
            // Cancelación (pausa/stop manual): no es un fallo fatal; no se cierra la sesión acá.
            throw;
        } catch (Exception ex) {
            _logger.LogCritical("Error fatal en EvaluateAsync. Finalizando sesion. Ex: {a}", ex.Message);
            await StopSession(CancellationToken.None);
            throw;
        }

        return await TryTriggerFailureAsync(ct);
    }

    #endregion

    #region Privates

    private void SubscribeToFlightEvents() {
        if (_subscribedToFlightEvents) return;
        _flightEventDetector.FlightEventDetected += OnFlightEventDetected;
        _subscribedToFlightEvents = true;
    }

    private void UnsubscribeFromFlightEvents() {
        if (!_subscribedToFlightEvents) return;
        _flightEventDetector.FlightEventDetected -= OnFlightEventDetected;
        _subscribedToFlightEvents = false;
    }

    private void OnFlightEventDetected(object? sender, FlightEventDto flightEvent) {
        lock (_eventQueueLock) {
            _pendingFlightEvents.Enqueue(flightEvent);
        }
    }

    private void ClearPendingFlightEvents() {
        lock (_eventQueueLock) {
            _pendingFlightEvents.Clear();
        }
    }

    private async Task DrainFlightEventsAsync(ComplexFlightPhaseEnum phase, CancellationToken ct) {
        if (!IsRunning) return;

        List<FlightEventDto> pending;
        lock (_eventQueueLock) {
            if (_pendingFlightEvents.Count == 0) return;
            pending = [.. _pendingFlightEvents];
            _pendingFlightEvents.Clear();
        }

        foreach (var flightEvent in pending) {
            var (category, code) = flightEvent.Kind switch {
                FlightEventKind.PhaseChanged => (SessionEventCategory.PhaseChanged, PhaseCode(phase)),
                FlightEventKind.EnginesStarted => (SessionEventCategory.EnginesStarted, "ENGINES_STARTED"),
                FlightEventKind.EnginesStopped => (SessionEventCategory.EnginesStopped, "ENGINES_STOPPED"),
                FlightEventKind.AltitudeCrossed => (SessionEventCategory.AltitudeCrossed, "ALTITUDE_CROSSED"),
                _ => (SessionEventCategory.SessionError, "UNKNOWN_EVENT")
            };
            await _sessionService.AddEventLogAsync(Session!.Session.Id, category, code, flightEvent.Message, phase, flightEvent.TimestampUtc, ct);
        }
    }

    private static string PhaseCode(ComplexFlightPhaseEnum phase) => phase.ToString().ToUpperInvariant();

    /// <summary>
    /// Reconciliación del ciclo de vida por lectura de Fenix (diseño objetivo §10): cada tick compara las fallas
    /// persistidas en curso contra lo que el avión reporta real en <c>FenixFailureDto</c>.
    ///   • Sent → Active cuando <c>failed=true</c> (confirmada por Fenix).
    ///   • Active → ResolvedByPilot cuando <c>failed=false</c> y ya no está armada (el piloto la despejó).
    /// </summary>
    private async Task ReconcileFailureLifecycleAsync(ComplexFlightPhaseEnum phase, CancellationToken ct) {
        var session = Session;
        if (session is null) return;

        var active = await _triggeredFailureRepository.GetActiveForSessionAsync(session.Session.Id, ct);
        if (active.Count == 0) return;

        var failedById = session.FailedFailures.ToDictionary(f => f.FenixId);
        var armedById = session.ArmedFailures.ToDictionary(f => f.FenixId);
        var utcNow = DateTime.UtcNow;

        foreach (var failure in active) {
            var liveFailed = failedById.TryGetValue(failure.FenixFailureId, out var ff) ? ff.Failed : false;
            var liveArmed = armedById.ContainsKey(failure.FenixFailureId);

            switch (failure.State) {
                case FailureLifecycleState.Sent when liveFailed:
                    await _sessionService.UpdateTriggeredFailureStateAsync(
                        session.Session.Id, failure.FenixFailureId, FailureLifecycleState.Active,
                        phase, null, utcNow, ct);
                    _logger.LogInformation("Failure confirmed active by Fenix: {Id}", failure.FenixFailureId);
                    break;

                case FailureLifecycleState.Active when !liveFailed && !liveArmed:
                    await _sessionService.UpdateTriggeredFailureStateAsync(
                        session.Session.Id, failure.FenixFailureId, FailureLifecycleState.ResolvedByPilot,
                        phase, ResolutionSource.Pilot, utcNow, ct);
                    _logger.LogInformation("Failure resolved by pilot (Fenix cleared it): {Id}", failure.FenixFailureId);
                    break;
            }
        }
    }

    private static double RiskProbabilityMultiplier(RiskLevel riskLevel) => riskLevel switch {
        RiskLevel.Low => 0.5,
        RiskLevel.Moderate => 1.0,
        RiskLevel.High => 1.75,
        _ => 1.0
    };

    private async Task<EvaluationResult> TryTriggerFailureAsync(CancellationToken ct) {
        var session = Session!;
        var now = DateTime.UtcNow;

        if (session.LastTriggeredAtUtc is DateTime lastTriggered
            && (now - lastTriggered).TotalSeconds < MinCooldownSeconds) {
            _logger.LogDebug("Skipping trigger evaluation: cooldown active ({Elapsed}s < {MinCooldown}s)",
                (now - lastTriggered).TotalSeconds, MinCooldownSeconds);
            return EvaluationResult.NoAction();
        }

        if (session.ActiveFailureCount >= MaxSimultaneousActiveFailures) {
            _logger.LogDebug("Skipping trigger evaluation: max simultaneous failures reached ({Count}/{Max})",
                session.ActiveFailureCount, MaxSimultaneousActiveFailures);
            return EvaluationResult.NoAction();
        }

        var candidatePresets = session.AvailablePresets
            .Where(p => p.FlightPhase == session.CurrentFlightPhase)
            .ToArray();
        if (candidatePresets.Length == 0) {
            _logger.LogDebug("No candidate presets for current phase {Phase}", session.CurrentFlightPhase);
            return EvaluationResult.NoAction();
        }

        _random.Shuffle(candidatePresets);
        var effectiveProbability = BaseProbability * RiskProbabilityMultiplier(session.RiskLevel);
        var activeFenixFailureIds = session.ActiveFenixFailureIds;

        foreach (var preset in candidatePresets) {
            var chosen = _failureTrigger.TryTriggerFailure(preset, session.CurrentFlightPhase, session.RiskLevel, effectiveProbability, activeFenixFailureIds);
            if (chosen == null) continue;

            var armed = await _simulator.ExecuteFailureAsync(chosen, ct);
            if (!armed) continue;

            try {
                await _sessionService.AddTriggeredFailureAsync(session.Session.Id, chosen, session.CurrentFlightPhase, now, ct);
            } catch (OperationCanceledException ex) {
                // Persistencia cancelada: la falla quedó armada y aún no está registrada. Se desarma con token
                // independiente (seguro pese a la cancelación) y recién después se propaga la cancelación.
                _logger.LogWarning(ex, "Persistence of triggered failure {FenixFailureId} canceled; attempting disarm before propagating cancellation",
                    chosen.FenixFailureId);
                try {
                    var disarmed = await _simulator.DisarmFailureAsync(chosen.FenixFailureId, CancellationToken.None);
                    if (disarmed) {
                        _logger.LogWarning("Persistence canceled but disarm succeeded for {FenixFailureId}; failure not recorded",
                            chosen.FenixFailureId);
                    } else {
                        _logger.LogCritical("Compensation failed: persistence canceled AND disarm failed for {FenixFailureId}; failure remains armed but is not recorded",
                            chosen.FenixFailureId);
                    }
                } catch (Exception disarmEx) {
                    _logger.LogCritical(disarmEx, "Compensation failed: disarm threw while compensating canceled persistence for {FenixFailureId}; failure may remain armed",
                        chosen.FenixFailureId);
                }
                throw;
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to persist triggered failure {FenixFailureId} for session {SessionId}; attempting disarm",
                    chosen.FenixFailureId, session.Session.Id);
                var disarmed = await _simulator.DisarmFailureAsync(chosen.FenixFailureId, ct);
                if (disarmed) {
                    _logger.LogWarning("Persistence failed but disarm succeeded for {FenixFailureId}; failure not recorded",
                        chosen.FenixFailureId);
                } else {
                    _logger.LogCritical("Compensation failed: persistence failed AND disarm failed for {FenixFailureId}; failure remains armed but is not recorded",
                        chosen.FenixFailureId);
                }
                return EvaluationResult.NoAction();
            }

            session.RecordTriggeredFailure(now);

            _logger.LogInformation("Failure triggered: {FenixFailureId} (preset {PresetName}) during phase {Phase} for session {SessionId}",
                chosen.FenixFailureId, preset.Name, session.CurrentFlightPhase, session.Session.Id);

            var info = new TriggeredFailureInfo(
                chosen.FenixFailureId,
                chosen.FenixFailure?.Name ?? chosen.FenixFailureId,
                preset.Name,
                now);
            return EvaluationResult.Completed([info]);
        }

        return EvaluationResult.NoAction();
    }
    private async Task<(SimulatorAircraftStateSnapshot rawData, IReadOnlyList<FenixFailureDto> failures)> GetSimulatorData(CancellationToken ct) {
        const string msgSimConn = "Simulator connection unavailable (Fenix={Fenix}, SimConnect={SimConnect}). Pausing evaluation.";
        const string msgSimData = "Simulator data corrupted (Fenix={Fenix}, SimConnect={SimConnect}). Pausing evaluation.";
        var conn = await _simulator.GetConnectionStatusAsync(ct);
        if (!conn.IsFenixConnected || !conn.IsSimConnectConnected) {
            _logger.LogWarning(
                msgSimConn,
                conn.IsFenixConnected, conn.IsSimConnectConnected);
            throw new SimulatorDataUnavailableException(msgSimConn);
        }

        var rawSimulatorDataResult = await _simulator.GetSimulatorData(ct);
        var fenixFailuresResult = await _simulator.GetCurrentFenixFailures(ct);
        var rawDataValue = rawSimulatorDataResult.Value;
        var fenixFailuresValue = fenixFailuresResult.Value;
        if (!rawSimulatorDataResult.IsSuccess || rawDataValue is null
            || !fenixFailuresResult.IsSuccess || fenixFailuresValue is null) {
            _logger.LogWarning(
                msgSimData,
                fenixFailuresResult.IsSuccess, rawSimulatorDataResult.IsSuccess);
            throw new SimulatorDataUnavailableException(msgSimData);
        }

        var failures = fenixFailuresValue.GetFailuresList();
        return (rawDataValue, failures);
    }

    #endregion

    private sealed class SimulatorDataUnavailableException : Exception {
        public SimulatorDataUnavailableException(string message) : base(message) { }
    }

}
