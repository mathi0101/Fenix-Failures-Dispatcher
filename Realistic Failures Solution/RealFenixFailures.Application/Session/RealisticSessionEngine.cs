using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;
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

    #region Fields
    private readonly ISimulatorConnectionService _simulator;
    private readonly ILogger<RealisticSessionEngine> _logger;
    private readonly IFlightSessionService _sessionService;
    private readonly IPresetService _presetService;
    private readonly IFlightEventDetector _flightEventDetector;
    private readonly ITriggeredFailureRepository _triggeredFailureRepository;
    private readonly IFlightPlanRepository _flightPlanRepository;
    private readonly IFenixFailureDefinitionRepository? _fenixFailureDefinitions;
    private readonly IAircraftSystemWearRepository? _aircraftSystemWearRepository;
    private readonly RealisticModeSettings _settings;
    private readonly RealisticFailurePolicy _policy;
    private readonly object _eventQueueLock = new();
    private readonly Queue<FlightEventDto> _pendingFlightEvents = new();
    private bool _subscribedToFlightEvents;
    private bool _hasEverHadFenixRead;
    private bool _groundSeeded;
    private Dictionary<string, int>? _ataByFenixId;
    private DateTime? _lastCadenceTickUtc;
    private DateTime _scenarioStartedAtUtc;


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

    public PreparedFailureQueue Queue { get; }
    public int PreparedCount => Queue.Count;
    public event EventHandler? PreparedQueueChanged;
    public int DiscardPrepared() => Queue.DiscardAll();

    #endregion

    #region Constructor
    public RealisticSessionEngine(
        ISimulatorConnectionService simulator,
        ILogger<RealisticSessionEngine> logger,
        IFlightSessionService sessionService,
        IPresetService presetService,
        IFlightEventDetector flightEventDetector,
        IFlightPlanRepository flightPlanRepository,
        ITriggeredFailureRepository triggeredFailureRepository,
        RealisticModeSettings? settings = null,
        RealisticFailurePolicy? policy = null,
        IFenixFailureDefinitionRepository? fenixFailureDefinitions = null,
        IAircraftSystemWearRepository? aircraftSystemWearRepository = null) {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionService = sessionService;
        _presetService = presetService;
        _flightEventDetector = flightEventDetector ?? throw new ArgumentNullException(nameof(flightEventDetector));
        _flightPlanRepository= flightPlanRepository ?? throw new ArgumentNullException(nameof(flightPlanRepository));
        _triggeredFailureRepository = triggeredFailureRepository ?? throw new ArgumentNullException(nameof(triggeredFailureRepository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _policy = policy ?? new RealisticFailurePolicy(new FailureRuleEvaluator());
        _fenixFailureDefinitions = fenixFailureDefinitions;
        _aircraftSystemWearRepository = aircraftSystemWearRepository;
        Queue = new PreparedFailureQueue(_settings.PreparedQueueLimit);
        Queue.Changed += (_, _) => {
            OnPropertyChanged(nameof(PreparedCount));
            PreparedQueueChanged?.Invoke(this, EventArgs.Empty);
        };
    }
    #endregion

    #region Public API

    public void ResetCadence() => _lastCadenceTickUtc = _policy.UtcNow;

    public async Task<StartSessionResult> TryStartSession(RealisticModeContext context, CancellationToken ct) {
        var conn = await _simulator.GetConnectionStatusAsync(ct);
        var data = await _simulator.GetSimulatorData(ct);

        if (!conn.IsSimConnectConnected)
            return new StartSessionResult(false, "Simulator connection not detected yet");

        if (!data.IsSuccess || data.Value is null)
            return new StartSessionResult(false, "Simulator data unavailable");

        var snapshot = data.Value;
        if (!snapshot.HasSimulatorData)
            return new StartSessionResult(false, "Simulator data unavailable (HasSimulatorData=false)");

        // Cargar presets ANTES de persistir la sesión: si la carga falla, no queda sesión persistida huérfana.
        var presets = (await _presetService.GetRealisticPresetsAsync(ct)).ToList();
        var newSession = await _sessionService.StartNewAsync(context, ct);

        newSession.FlightPlan = await _flightPlanRepository.GetByIdAsync(newSession.FlightPlanId, ct);
        if (newSession.FlightPlan == null)
            return new StartSessionResult(false, "Flight Plan data not loaded");

        Session = new RealisticSession(newSession, context.Aircraft, RealisticProfileCatalog.Resolve(_settings, newSession.RiskLevel)) {
            AvailablePresets = presets
        };
        _scenarioStartedAtUtc = _policy.UtcNow;
        foreach (var preset in Session.AvailablePresets)
            foreach (var def in preset.PresetFailureDefinitions)
                def.Preset ??= preset;

        try {
            await _sessionService.AddEventLogAsync(newSession.Id, SessionEventCategory.SessionStarted, "SESSION_STARTED", "Sesión realista iniciada", ComplexFlightPhaseEnum.Unknown, newSession.StartedAt, ct);
            await _sessionService.AddEventLogAsync(newSession.Id, SessionEventCategory.OfpLoaded, "OFP_LOADED",
                $"OFP {newSession.FlightPlan.FlightNumber ?? newSession.FlightPlan.Callsign ?? "sin identificador"} ({newSession.FlightPlan.Source}) asociado a la sesión",
                ComplexFlightPhaseEnum.Unknown, newSession.StartedAt, ct);
        } catch (Exception) {
            try {
                await _sessionService.StopAsync(newSession.Id, DateTime.UtcNow, CancellationToken.None);
            } catch (Exception stopEx) {
                _logger.LogWarning(stopEx, "Failed to close session {SessionId} after start-event registration failure", newSession.Id);
            }
            Session = null;
            throw;
        }

        _lastCadenceTickUtc = _policy.UtcNow;
        if (_settings.GroundFailureEnabled && snapshot.IsOnGround && !(snapshot.Engine1IsRunning || snapshot.Engine2IsRunning)) {
            await SeedGroundFailuresAsync(ct);
        }

        SubscribeToFlightEvents();

        return new StartSessionResult(true, string.Empty);
    }

    public async Task StopSession(CancellationToken ct) {
        if (!IsRunning) return;
        try {
            if (Session!.Session.FinishedAt == null) {
                var stoppedAt = DateTime.UtcNow;
                await DisarmActiveFailuresAsync(ct);
                var wearDeltas = await AccumulateWearAsync(Session, stoppedAt, ct);
                await _sessionService.FinalizeSessionAsync(Session.Session.Id, stoppedAt, wearDeltas, ct);
                try {
                    await _sessionService.AddEventLogAsync(Session.Session.Id, SessionEventCategory.SessionStopped, "SESSION_STOPPED", "Sesión realista detenida", Session.CurrentFlightPhase, stoppedAt, ct);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to register stop event for session {SessionId}", Session.Session.Id);
                }
            }
            Session = null;
        } finally {
            Queue.DiscardAll();
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
            var conn = await _simulator.GetConnectionStatusAsync(ct);
            if (!conn.IsSimConnectConnected) {
                _logger.LogWarning("SimConnect no disponible durante la evaluación; pausando evaluación.");
                _lastCadenceTickUtc = _policy.UtcNow;
                return EvaluationResult.Disconnected();
            }

            var rawDataResult = await _simulator.GetSimulatorData(ct);
            var rawData = rawDataResult.Value;
            if (!rawDataResult.IsSuccess || rawData is null) {
                _logger.LogWarning("Datos del simulador no disponibles durante la evaluación; pausando evaluación.");
                _lastCadenceTickUtc = _policy.UtcNow;
                return EvaluationResult.Disconnected();
            }

            if (!rawData.HasSimulatorData) {
                _logger.LogDebug("Skipping realistic evaluation tick: no simulator data in snapshot (HasSimulatorData=false)");
                _lastCadenceTickUtc = _policy.UtcNow;
                return EvaluationResult.NoAction();
            }

            var fenixAvailable = false;
            IReadOnlyList<FenixFailureDto>? failuresList = null;
            if (conn.IsFenixConnected) {
                var fenixResult = await _simulator.GetCurrentFenixFailures(ct);
                fenixAvailable = fenixResult.IsSuccess && fenixResult.Value is not null;
                failuresList = fenixResult.Value?.GetFailuresList();
            }
            if (fenixAvailable) _hasEverHadFenixRead = true;

            var currentPhase = _flightEventDetector.ProcessSnapshot(rawData);
            await DrainFlightEventsAsync(currentPhase, ct);
            Session!.ProcessSimData(rawData, failuresList ?? Session.AllFenixFailures, currentPhase);

            if (currentPhase != ComplexFlightPhaseEnum.OnGate && Queue.HasGround) {
                var discarded = Queue.DiscardGround();
                _logger.LogInformation("Fallas preparadas de tierra descartadas al salir de OnGate (fase {Phase}): {Count}", currentPhase, discarded);
            }

            if (!fenixAvailable) {
                if (_hasEverHadFenixRead) {
                    _logger.LogDebug("Fenix no disponible tras haber estado disponible: la sesión espera sin elegir/enviar/reconciliar.");
                    _lastCadenceTickUtc = _policy.UtcNow;
                    return EvaluationResult.NoAction();
                }
                if (Queue.Count >= _settings.PreparedQueueLimit) {
                    _lastCadenceTickUtc = _policy.UtcNow;
                    return EvaluationResult.NoAction();
                }
                var activeOwned = await _triggeredFailureRepository.GetActiveForSessionAsync(Session.Session.Id, ct);
                return await TryChooseAsync(currentPhase, activeOwned, fenixAvailable: false, ct);
            }

            var stateChanges = await ReconcileFailureLifecycleAsync(currentPhase, ct);

            var activeAfterReconcile = await _triggeredFailureRepository.GetActiveForSessionAsync(Session.Session.Id, ct);
            var flush = await FlushPreparedAsync(currentPhase, activeAfterReconcile, ct);
            if (flush.Attempted) return EvaluationResult.Completed(flush.Sent).WithStateChanges(stateChanges);

            var chosen = await TryChooseAsync(currentPhase, activeAfterReconcile, fenixAvailable: true, ct);
            return chosen.WithStateChanges(stateChanges);
        } catch (OperationCanceledException) {
            // Cancelación (pausa/stop manual): no es un fallo fatal; no se cierra la sesión acá.
            throw;
        } catch (Exception ex) {
            _logger.LogCritical("Error fatal en EvaluateAsync. Finalizando sesion. Ex: {a}", ex.Message);
            await StopSession(CancellationToken.None);
            throw;
        }
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
    private async Task<IReadOnlyList<FailureStateChangeInfo>> ReconcileFailureLifecycleAsync(ComplexFlightPhaseEnum phase, CancellationToken ct) {
        var session = Session;
        if (session is null) return [];

        var active = await _triggeredFailureRepository.GetActiveForSessionAsync(session.Session.Id, ct);
        if (active.Count == 0) return [];

        var liveById = session.AllFenixFailures.ToDictionary(f => f.FenixId);
        var utcNow = DateTime.UtcNow;
        var changes = new List<FailureStateChangeInfo>();

        foreach (var failure in active) {
            if (!liveById.TryGetValue(failure.FenixFailureId, out var live)) {
                continue;
            }

            switch (failure.State) {
                case FailureLifecycleState.Sent when live.Failed:
                    await _sessionService.UpdateTriggeredFailureStateAsync(
                        session.Session.Id, failure.FenixFailureId, FailureLifecycleState.Active,
                        phase, null, utcNow, ct);
                    _logger.LogInformation("Failure confirmed active by Fenix: {Id}", failure.FenixFailureId);
                    await LogStateChangeAsync(session.Session.Id, failure, FailureLifecycleState.Active, "FAILURE_CONFIRMED_ACTIVE", phase, utcNow, ct);
                    changes.Add(new FailureStateChangeInfo(failure.FenixFailureId, failure.FailureName ?? failure.FenixFailureId, FailureLifecycleState.Active, failure.Severity, utcNow));
                    break;

                case FailureLifecycleState.Active when !live.Failed && !live.IsArmed:
                    await _sessionService.UpdateTriggeredFailureStateAsync(
                        session.Session.Id, failure.FenixFailureId, FailureLifecycleState.ResolvedByPilot,
                        phase, ResolutionSource.Pilot, utcNow, ct);
                    _logger.LogInformation("Failure resolved by pilot (Fenix cleared it): {Id}", failure.FenixFailureId);
                    await LogStateChangeAsync(session.Session.Id, failure, FailureLifecycleState.ResolvedByPilot, "FAILURE_RESOLVED_BY_PILOT", phase, utcNow, ct);
                    changes.Add(new FailureStateChangeInfo(failure.FenixFailureId, failure.FailureName ?? failure.FenixFailureId, FailureLifecycleState.ResolvedByPilot, failure.Severity, utcNow));
                    break;
            }
        }

        return changes;
    }

    /// <summary>
    /// Registra en el log de eventos de la sesión la transición de ciclo de vida confirmada por Fenix
    /// (el evento <c>FailureStateChanged</c> quedaba sin uso). Un fallo al registrar no aborta la
    /// reconciliación: la transición ya se persiste en <c>UpdateTriggeredFailureStateAsync</c>.
    /// </summary>
    private async Task LogStateChangeAsync(int sessionId, TriggeredFailure failure, FailureLifecycleState newState, string eventCode, ComplexFlightPhaseEnum phase, DateTime utcNow, CancellationToken ct) {
        try {
            var description = newState switch {
                FailureLifecycleState.Active => $"Falla confirmada activa por Fenix: {failure.FailureName} ({failure.FenixFailureId})",
                FailureLifecycleState.ResolvedByPilot => $"Falla resuelta por el piloto: {failure.FailureName} ({failure.FenixFailureId})",
                _ => $"Falla en estado {newState}: {failure.FailureName} ({failure.FenixFailureId})"
            };
            await _sessionService.AddEventLogAsync(sessionId, SessionEventCategory.FailureStateChanged, eventCode, description, phase, utcNow, ct);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo registrar el evento de cambio de estado {State} para {Id}; la transición ya se persistió", newState, failure.FenixFailureId);
        }
    }

    private async Task<EvaluationResult> TryChooseAsync(ComplexFlightPhaseEnum phase, IReadOnlyList<TriggeredFailure> activeOwned, bool fenixAvailable, CancellationToken ct) {
        var session = Session!;
        var now = _policy.UtcNow;
        var elapsedSeconds = _lastCadenceTickUtc is DateTime lastTick
            ? Math.Min((now - lastTick).TotalSeconds, Math.Max(0, _settings.EvaluationDeltaCapSeconds))
            : 0;
        _lastCadenceTickUtc = now;

        if (session.CriticalRespiteUntilUtc is DateTime respiteUntil && now < respiteUntil) {
            _logger.LogDebug("Critical respite activo hasta {Until}; no se eligen fallas nuevas", respiteUntil);
            return EvaluationResult.NoAction();
        }

        // Score de agravamiento (spec §2): activas propias + reservas de cola pesan juntas; los extras
        // por mismo ATA aplican sobre ese conteo combinado. Null = sin tope (7700).
        var profile = session.RiskProfile;
        var physics = _settings.Physics;
        var ataLookup = await GetAtaLookupAsync(ct);
        var (score, ataCounts) = CurrentAggravation(activeOwned, ataLookup);

        var excluded = new HashSet<string>(session.ActiveFenixFailureIds);
        foreach (var f in activeOwned) excluded.Add(f.FenixFailureId);
        excluded.UnionWith(Queue.Snapshot().Select(q => q.FenixFailureId));

        var candidates = _policy.EvaluateEligible(
                session.AvailablePresets.SelectMany(p => p.PresetFailureDefinitions),
                phase, session.RiskLevel, excluded)
            .DistinctBy(c => c.FenixFailureId)
            .ToList();

        // Filtrado por score ANTES del roll (espec §2.3): un candidato entra si su costo (peso de severidad +
        // extras por ATA ya ocupado) no excede el cap; así no se gastan sorteos en severidades sin lugar.
        var wearByAta = session.Aircraft.SystemsWear
            .GroupBy(x => x.SystemWear.AtaId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.WearPercentage));
        var flightCandidates = new List<FlightFailureCandidate<PresetFailureDefinition>>();
        foreach (var candidate in candidates) {
            var severity = candidate.FenixFailure?.Severity ?? FailureSeverity.Minor;
            var extra = ataLookup.TryGetValue(candidate.FenixFailureId, out var ata)
                ? RealisticFailurePolicy.SameAtaExtra(physics, ataCounts.GetValueOrDefault(ata))
                : 0;
            var cost = RealisticFailurePolicy.AggravationWeight(physics, severity) + extra;
            if (profile.AggravationCap is int cap && score + cost > cap) continue;

            var wear = ataLookup.TryGetValue(candidate.FenixFailureId, out ata) ? wearByAta.GetValueOrDefault(ata) : (double?)null;
            var wearFactor = RealisticFailurePolicy.WearFactor(profile, wear);
            var lambda = RealisticFailurePolicy.FlightFailureLambda(severity, candidate.Probability, profile, physics, wearFactor);
            if (lambda > 0) flightCandidates.Add(new FlightFailureCandidate<PresetFailureDefinition>(candidate, lambda));
        }

        if (!_policy.RollTick(flightCandidates, elapsedSeconds, out var chosen) || chosen is null) {
            return EvaluationResult.NoAction();
        }

        if (!fenixAvailable) {
            var item = new PreparedFailureItem(
                chosen, chosen.FenixFailureId, chosen.FenixFailure?.Name ?? chosen.FenixFailureId,
                chosen.Preset?.Name ?? string.Empty,
                chosen.FenixFailure?.Severity ?? FailureSeverity.Minor, false, phase, now);
            if (Queue.TryEnqueue(item)) {
                _logger.LogInformation("Failure prepared (queued, no Fenix): {Id} ({Name}) phase {Phase}", chosen.FenixFailureId, item.Description, phase);
            }
            return EvaluationResult.NoAction();
        }

        return await SendFailureAndRecordAsync(session, chosen, phase, now, ct);
    }

    private async Task<EvaluationResult> SendFailureAndRecordAsync(RealisticSession session, PresetFailureDefinition chosen, ComplexFlightPhaseEnum phase, DateTime now, CancellationToken ct) {
        var armed = await _simulator.ExecuteFailureAsync(chosen, ct);
        if (!armed) {
            _logger.LogWarning("Fenix rechazó el armado de {Id}; no-evento, sin persistir.", chosen.FenixFailureId);
            return EvaluationResult.NoAction();
        }

        // La política usa tiempo de escenario; el historial y los DTO usan UTC de pared.
        var triggeredAtUtc = DateTime.UtcNow;
        try {
            await _sessionService.AddTriggeredFailureAsync(session.Session.Id, chosen, phase, triggeredAtUtc, ct);
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
        if (chosen.FenixFailure?.Severity == FailureSeverity.Critical) {
            session.CriticalRespiteUntilUtc = now + _policy.EffectiveRespite(session.RiskProfile, _settings.RespiteClampSeconds);
        }

        _logger.LogInformation("Failure triggered: {FenixFailureId} (preset {PresetName}) during phase {Phase} for session {SessionId}",
            chosen.FenixFailureId, chosen.Preset?.Name ?? string.Empty, phase, session.Session.Id);

        var info = new TriggeredFailureInfo(
            chosen.FenixFailureId,
            chosen.FenixFailure?.Name ?? chosen.FenixFailureId,
            chosen.Preset?.Name ?? string.Empty,
            triggeredAtUtc);
        return EvaluationResult.Completed([info]);
    }

    /// <summary>
    /// Score de agravamiento vigente (espec §2.1): peso de severidad de cada falla activa propia y de cada
    /// reserva de cola, más el conteo por ATA (activas + reservas) para los extras de mismo sistema.
    /// </summary>
    private (int Score, Dictionary<int, int> AtaCounts) CurrentAggravation(IReadOnlyList<TriggeredFailure> activeOwned, Dictionary<string, int> ataLookup) {
        var physics = _settings.Physics;
        var score = 0;
        var ataCounts = new Dictionary<int, int>();
        foreach (var f in activeOwned) {
            score += RealisticFailurePolicy.AggravationWeight(physics, f.Severity ?? FailureSeverity.Minor);
            if (ataLookup.TryGetValue(f.FenixFailureId, out var ata)) ataCounts[ata] = ataCounts.GetValueOrDefault(ata) + 1;
        }
        foreach (var q in Queue.Snapshot()) {
            score += RealisticFailurePolicy.AggravationWeight(physics, q.Severity);
            if (ataLookup.TryGetValue(q.FenixFailureId, out var ata)) ataCounts[ata] = ataCounts.GetValueOrDefault(ata) + 1;
        }
        return (score, ataCounts);
    }

    /// <summary>
    /// ATA de cada falla (FenixFailureId → AtaId), cacheado por engine: presets disponibles + catálogo
    /// persistido (las sembradas de tierra no vienen de un preset). Ids sin fuente resuelven null y
    /// quedan fuera de la agrupación por sistema (sin extras de ATA).
    /// </summary>
    private async Task<Dictionary<string, int>> GetAtaLookupAsync(CancellationToken ct) {
        if (_ataByFenixId is not null) return _ataByFenixId;
        var lookup = new Dictionary<string, int>();
        foreach (var def in Session!.AvailablePresets.SelectMany(p => p.PresetFailureDefinitions)) {
            if (def.FenixFailure?.Group?.System?.AtaId is int ata) lookup[def.FenixFailureId] = ata;
        }
        if (_fenixFailureDefinitions is not null) {
            foreach (var entity in await _fenixFailureDefinitions.GetAllWithSystemsAsync(ct)) {
                if (entity.Group?.System?.AtaId is int ata) lookup[entity.FenixFailureId] = ata;
            }
        }
        _ataByFenixId = lookup;
        return lookup;
    }

    /// <summary>
    /// Acumulación de desgaste por horas de vuelo (espec §3.2): al cerrar la sesión, cada sistema suma
    /// WearPerFlightHour × horas de escenario de sesión, con clamp 0–100. Best-effort: un
    /// fallo no interrumpe el stop de la sesión.
    /// </summary>
    /// <summary>
    /// Desarme best-effort de las fallas no resueltas de la sesión en Fenix antes de consolidar el cierre
    /// (diseño objetivo §9): intenta desarmar cada una; si Fenix no confirma o falla, se registra y continúa
    /// (el reset global del orquestador queda como red de seguridad). Ningún fallo interrumpe el cierre.
    /// </summary>
    private async Task DisarmActiveFailuresAsync(CancellationToken ct) {
        try {
            var active = await _triggeredFailureRepository.GetActiveForSessionAsync(Session!.Session.Id, ct);
            foreach (var failure in active) {
                try {
                    if (!await _simulator.DisarmFailureAsync(failure.FenixFailureId, ct))
                        _logger.LogWarning("Fenix no confirmó el desarme de {FenixFailureId} al cerrar la sesión {SessionId}",
                            failure.FenixFailureId, Session.Session.Id);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Error desarmando {FenixFailureId} al cerrar la sesión {SessionId}",
                        failure.FenixFailureId, Session.Session.Id);
                }
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudieron desarmar las fallas activas al cerrar la sesión {SessionId}", Session!.Session.Id);
        }
    }

    /// <summary>
    /// Acumula el desgaste por horas de vuelo de la sesión (diseño objetivo §12) y devuelve el delta aplicado
    /// por sistema para el modal de resultados (§13). Un error de desgaste no interrumpe el cierre.
    /// </summary>
    private async Task<IReadOnlyList<SystemWearDeltaDto>> AccumulateWearAsync(RealisticSession session, DateTime stoppedAtUtc, CancellationToken ct) {
        if (_aircraftSystemWearRepository is null) return [];
        try {
            var hours = (_policy.UtcNow - _scenarioStartedAtUtc).TotalHours;
            if (hours <= 0) return [];
            var wears = await _aircraftSystemWearRepository.GetForAircraftAsync(session.Aircraft.Id, ct);
            var deltas = new List<SystemWearDeltaDto>();
            foreach (var wear in wears) {
                var rate = wear.WearableSystem?.WearPerFlightHour ?? 0;
                if (rate <= 0) continue;
                var delta = rate * hours;
                wear.WearPercentage = Math.Clamp(wear.WearPercentage + delta, 0, 100);
                wear.LastUpdatedAt = stoppedAtUtc;
                deltas.Add(new SystemWearDeltaDto {
                    AtaId = wear.WearableSystem!.AtaId,
                    SystemName = wear.WearableSystem.Name,
                    DeltaPercentage = delta,
                    TotalPercentage = wear.WearPercentage
                });
            }
            if (deltas.Count > 0) await _aircraftSystemWearRepository.SaveAsync(ct);
            _logger.LogInformation("Desgaste acumulado por {Hours:F2} h de vuelo en {Count} sistemas (aeronave {AircraftId})",
                hours, deltas.Count, session.Aircraft.Id);
            return deltas;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo acumular el desgaste por horas de vuelo al cerrar la sesión {SessionId}", session.Session.Id);
            return [];
        }
    }

    private async Task<(List<TriggeredFailureInfo> Sent, bool Attempted)> FlushPreparedAsync(ComplexFlightPhaseEnum phase, IReadOnlyList<TriggeredFailure> activeOwned, CancellationToken ct) {
        if (Queue.Count == 0) return ([], false);
        var session = Session!;
        var now = _policy.UtcNow;
        var profile = session.RiskProfile;
        var physics = _settings.Physics;
        var ataLookup = await GetAtaLookupAsync(ct);
        var (score, ataCounts) = CurrentAggravation(activeOwned, ataLookup);
        var excludedIds = new HashSet<string>(activeOwned.Select(f => f.FenixFailureId));

        var sent = new List<TriggeredFailureInfo>();

        if (Queue.HasGround && phase == ComplexFlightPhaseEnum.OnGate) {
            var attempted = false;
            while (true) {
                var ground = Queue.TryTakeFirstGround();
                if (ground is null) break;
                attempted = true;
                var result = await TrySendPreparedItemAsync(ground, phase, now, score, excludedIds, profile, physics, ct);
                if (result.Status == PreparedSendResult.Sent) {
                    sent.Add(result.Failure!);
                } else if (result.Status == PreparedSendResult.Keep) {
                    Queue.RequeueFront(ground);
                    break;
                }
            }
            return (sent, attempted);
        }

        var airborne = Queue.TryTakeFirstAirborne();
        if (airborne is null) return (sent, false);

        var airborneResult = await TrySendPreparedItemAsync(airborne, phase, now, score, excludedIds, profile, physics, ct);
        if (airborneResult.Status == PreparedSendResult.Sent) {
            sent.Add(airborneResult.Failure!);
        } else if (airborneResult.Status == PreparedSendResult.Keep) {
            Queue.RequeueFront(airborne);
        }
        return (sent, true);
    }

    private enum PreparedSendResult { Sent, DroppedNoEvent, Keep }

    private async Task<(PreparedSendResult Status, TriggeredFailureInfo? Failure)> TrySendPreparedItemAsync(PreparedFailureItem item, ComplexFlightPhaseEnum phase, DateTime now, int score, HashSet<string> excludedIds, RealisticProfileSettings profile, RealisticPhysicsSettings physics, CancellationToken ct) {
        var session = Session!;
        if (excludedIds.Contains(item.FenixFailureId)) {
            _logger.LogWarning("Preparada descartada (ya enviada o activa): {Id}", item.FenixFailureId);
            return (PreparedSendResult.DroppedNoEvent, null);
        }
        var stillEligible = _policy.EvaluateEligible(new[] { item.Definition }, phase, session.RiskLevel, excludedIds);
        if (stillEligible.Count == 0) {
            _logger.LogInformation("Preparada descartada (ya no elegible físicamente en fase {Phase}): {Id}", phase, item.FenixFailureId);
            return (PreparedSendResult.DroppedNoEvent, null);
        }
        if (session.CriticalRespiteUntilUtc is DateTime respiteUntil && now < respiteUntil) {
            return (PreparedSendResult.Keep, null);
        }
        // El ítem ya computa en el score (como reserva). Enviarlo lo mueve de cola a activa: el score no
        // cambia, pero si el total vigente excede el cap (p. ej. cap bajado por config) espera a que enfríe.
        if (profile.AggravationCap is int cap && score > cap) {
            _logger.LogInformation("Preparada conservada (score de agravamiento {Score} sobre el cap {Cap}): {Id}", score, cap, item.FenixFailureId);
            return (PreparedSendResult.Keep, null);
        }

        var sendResult = await SendFailureAndRecordAsync(session, item.Definition, phase, now, ct);
        if (sendResult.TriggeredFailures.Count == 1) {
            excludedIds.Add(item.FenixFailureId);
            var failure = sendResult.TriggeredFailures[0];
            return (PreparedSendResult.Sent, failure with { Description = item.Description, PresetName = item.PresetName });
        }

        var connection = await _simulator.GetConnectionStatusAsync(ct);
        if (!connection.IsFenixConnected) {
            _logger.LogWarning("Preparada conservada por pérdida de conexión Fenix ({Id}); se reintenta en el próximo tick.", item.FenixFailureId);
            return (PreparedSendResult.Keep, null);
        }
        _logger.LogWarning("Preparada rechazada por Fenix ({Id}); no-evento, sin persistir.", item.FenixFailureId);
        return (PreparedSendResult.DroppedNoEvent, null);
    }

    private async Task SeedGroundFailuresAsync(CancellationToken ct) {
        if (_groundSeeded) return;
        _groundSeeded = true;
        var session = Session!;
        var candidates = await BuildGroundCandidatesAsync(ct);
        if (candidates.Count == 0) {
            _logger.LogWarning("No hay candidatos de fallas de tierra elegibles; no se siembran fallas de tierra.");
            return;
        }
        // Sorteo Bernoulli individual por falla (fórmula unificada: base de física × mult perfil × desgaste);
        // GroundFailuresCap actúa como tope, no como objetivo: la cantidad sembrada emerge de los sorteos.
        var selected = _policy.SelectGroundFailures(candidates, session.RiskProfile.GroundFailuresCap);
        var now = _policy.UtcNow;
        foreach (var candidate in selected) {
            var (def, presetName) = candidate.Item;
            var item = new PreparedFailureItem(
                def, def.FenixFailureId, def.FenixFailure?.Name ?? def.FenixFailureId, presetName,
                def.FenixFailure?.Severity ?? FailureSeverity.Minor, true, ComplexFlightPhaseEnum.OnGate, now);
            Queue.TryEnqueue(item);
        }
        _logger.LogInformation(
            "Fallas de tierra sembradas: {Count} de {Candidates} candidatos (perfil {Risk}, p_base {Base}, tope {Cap})",
            selected.Count, candidates.Count, session.RiskLevel, _settings.Physics.GroundSeedBaseProbability, session.RiskProfile.GroundFailuresCap);
    }

    private async Task<List<GroundFailureCandidate<(PresetFailureDefinition Def, string PresetName)>>> BuildGroundCandidatesAsync(CancellationToken ct) {
        // Las fallas de tierra son TODAS las definiciones persistidas con severidad 1 (Minor):
        // no hay whitelist configurable; el catálogo curado (failures.json, persistido en FenixFailureDefinition)
        // es la fuente única del universo de fallas y la sesión escoge desde esa lista.
        if (_fenixFailureDefinitions is null) return [];

        // Desgaste por ATA de la aeronave de la sesión: FenixFailureSystem.AtaId ↔ AircraftWearableSystem.AtaId.
        // Sin registro de desgaste para un sistema → wear 0 → probabilidad base sin amplificar.
        var profile = Session!.RiskProfile;
        var physics = _settings.Physics;
        var wearByAta = Session.Aircraft.SystemsWear
            .GroupBy(x => x.SystemWear.AtaId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.WearPercentage));

        var result = new List<GroundFailureCandidate<(PresetFailureDefinition Def, string PresetName)>>();
        var availableFailures = await _fenixFailureDefinitions.GetAllWithSystemsAsync(ct);
        foreach (var entity in availableFailures.Where(x=> x.Severity is FailureSeverity.Minor)) {
            if (entity.FenixFailureId.StartsWith("F_OH_NAV_IR_ALIGNMENT", StringComparison.Ordinal)) continue;
            var ataId = entity.Group?.System?.AtaId ?? 0;
            var wear = wearByAta.GetValueOrDefault(ataId);
            var wearFactor = RealisticFailurePolicy.WearFactor(profile, wear);
            var probability = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, profile, physics, wearFactor);
            // Candidato sintético que referencia la entidad persistida (trackeada por GetAllWithSystemsAsync,
            // misma identidad que el contexto de sesión): no se mutan navegaciones ajenas, así que el
            // SaveChanges posterior al seed no dispara conflictos de identity de EF.
            var def = new PresetFailureDefinition {
                FenixFailureId = entity.FenixFailureId,
                FenixFailure = entity,
                Probability = probability
            };
            result.Add(new GroundFailureCandidate<(PresetFailureDefinition, string)>(
                (def, entity.Group?.System?.Name ?? string.Empty), probability, wear));
        }
        return result;
    }

    #endregion

}
