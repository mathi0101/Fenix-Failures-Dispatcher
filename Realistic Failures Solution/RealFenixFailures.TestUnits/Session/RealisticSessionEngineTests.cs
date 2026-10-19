// RealFenixFailures.TestUnits/Session/RealisticSessionEngineTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Mocks;

namespace RealFenixFailures.TestUnits.Session;

public class RealisticSessionEngineTests {
    private readonly Mock<ISimulatorConnectionService> _simulator = new();
    private readonly Mock<IFlightSessionService> _sessionService = new();
    private readonly Mock<IPresetService> _presetService = new();
    private readonly Mock<IFlightEventDetector> _flightEventDetector = new();
    private readonly Mock<ITriggeredFailureRepository> _triggeredFailureRepository = new();
    private readonly MockRandom _random = new();
    private DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    public RealisticSessionEngineTests() {
        // Por defecto, sin fallas activas en BD para que la reconciliación del ciclo de vida no dispare NRE.
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TriggeredFailure>());
    }

    private RealisticSessionEngine CreateEngine(RealisticModeSettings? settings = null, IFenixFailureDefinitionRepository? fenixFailureDefinitions = null) => new(
        _simulator.Object,
        NullLogger<RealisticSessionEngine>.Instance,
        _sessionService.Object,
        _presetService.Object,
        _flightEventDetector.Object,
        _triggeredFailureRepository.Object,
        settings,
        new RealisticFailurePolicy(new FailureRuleEvaluator(), () => _now, _random),
        fenixFailureDefinitions);

    private static RealisticModeContext Context(RiskLevel risk = RiskLevel.Moderate) =>
        new(risk, new UserAircraftDto { Id = 1, Registration = "LV-XYZ" });

    private static SimulatorAircraftStateSnapshot OnGate() =>
        new() { IsOnGround = true, AltitudeMSL = 100, AltitudeAGL = 0, HasSimulatorData = true };

    private static SimulatorAircraftStateSnapshot Airborne() =>
        new() { IsOnGround = false, AltitudeMSL = 10000, AltitudeAGL = 9500, HasSimulatorData = true, GroundSpeed = 300 };

    private static SimulatorAircraftStateSnapshot EnginesRunningOnGround() =>
        new() {
            IsOnGround = true,
            AltitudeMSL = 100,
            AltitudeAGL = 0,
            HasSimulatorData = true,
            Engine1Combustion = true,
            Engine2Combustion = false,
            Engine1N1Percent = 40,
            Engine2N1Percent = 0,
            GroundSpeed = 0,
        };

    private static PresetFailureDefinition Def(string id, FailureSeverity severity, int systemId = 0) => new() {
        FenixFailureId = id,
        Probability = 1.0,
        FenixFailure = new FenixFailureDefinition {
            FenixFailureId = id,
            Name = id,
            Severity = severity,
            Group = new FenixFailureGroup {
                Name = "G-" + id,
                SystemId = systemId,
                System = new FenixFailureSystem { Id = systemId, Name = "S-" + systemId, ShortName = "S" }
            }
        }
    };

    private static FailurePreset Preset(ComplexFlightPhaseEnum phase, params PresetFailureDefinition[] defs) =>
        new() { Id = 1, Name = "PRESET", FlightPhase = phase, PresetFailureDefinitions = defs.ToList() };

    private void SetupConnected(bool fenixConnected = true) {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, fenixConnected, SimpleFlightPhaseEnum.OnGround));
    }

    private void SetupOnGateData() {
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(OnGate()));
    }

    private void SetupValidPersistence(RiskLevel risk = RiskLevel.Moderate, IEnumerable<PresetFailureDefinition>? definitions = null) {
        _sessionService
            .Setup(s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightSession { Id = 42, StartedAt = _now, RiskLevel = risk, UserAircraftId = 1 });
        var presets = new List<FailurePreset> {
            new() { Id = 1, Name = "PRESET-A", FlightPhase = ComplexFlightPhaseEnum.Unknown, PresetFailureDefinitions = definitions?.ToList() ?? [] }
        };
        if (definitions is null) {
            presets.Add(new FailurePreset { Id = 2, Name = "PRESET-B", FlightPhase = ComplexFlightPhaseEnum.Unknown });
        }
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(presets);
    }

    private void SetupFenixFailuresOk() {
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(new AllFenixFailuresResponseDto()));
    }

    private void SetupFenixFailures(params FenixFailureDto[] failures) {
        var grouped = new AllFenixFailuresResponseDto {
            MajorGroups = new List<FenixMajorSystemGroupDto> {
                new(
                    "Systems",
                    "SYS",
                    new List<FenixSystemGroupDto> {
                        new("Group", failures)
                    })
            }
        };
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(grouped));
    }

    [Fact]
    public async Task TryStartSession_ConnectionLost_DoesNotPersistSession() {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(false, false, SimpleFlightPhaseEnum.Disconnected));
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.False(result.Started);
        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryStartSession_DataFailure_DoesNotPersistSessionAndDoesNotThrow() {
        SetupConnected();
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Fail(new InvalidOperationException()));
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.False(result.Started);
        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryStartSession_NoSimulatorData_DoesNotPersistSession() {
        SetupConnected();
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(new SimulatorAircraftStateSnapshot {
                IsOnGround = true,
                HasSimulatorData = false,
            }));
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.False(result.Started);
        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryStartSession_AirborneOrEnginesRunning_PersistsSession() {
        // El arranque ya no exige gate ni motores apagados (tarea 1/2: arranque sin restricción de fase).
        SetupConnected();
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(EnginesRunningOnGround()));
        SetupValidPersistence();
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.True(result.Started);
        Assert.NotNull(engine.Session);
        Assert.Equal(0, engine.PreparedCount);
    }

    [Fact]
    public async Task TryStartSession_ValidConditions_PersistsSession() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var context = Context();
        var engine = CreateEngine();

        var result = await engine.TryStartSession(context, CancellationToken.None);

        Assert.True(result.Started);
        Assert.NotNull(engine.Session);
        Assert.Equal(2, engine.Session.AvailablePresets.Count);
        _sessionService.Verify(
            s => s.StartNewAsync(RiskLevel.Moderate, context.Aircraft, It.IsAny<CancellationToken>()),
            Times.Once);
        _presetService.Verify(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryStartSession_PresetLoadFails_DoesNotPersistSessionAndSessionStaysNull() {
        SetupConnected();
        SetupOnGateData();
        _sessionService
            .Setup(s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightSession { Id = 42, StartedAt = _now, RiskLevel = RiskLevel.Moderate, UserAircraftId = 1 });
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("presets unavailable"));
        var engine = CreateEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.TryStartSession(Context(), CancellationToken.None));

        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _presetService.Verify(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryStartSession_WithoutFenix_PersistsSessionWithoutReadingFenix() {
        // Primera sesión sin Fenix: arranca y prepara en cola (sin leer el listado de fallas).
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.True(result.Started);
        Assert.NotNull(engine.Session);
        _simulator.Verify(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_FatalError_StopsSessionAndRethrows() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        // Excepción no relacionada con desconexión: debe considerarse fatal y cerrar la sesión.
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection service exploded"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.EvaluateAsync(CancellationToken.None));

        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StopAsync(42, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_SimConnectLost_PausesWithoutStoppingSession() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(false, false, SimpleFlightPhaseEnum.Disconnected));

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.True(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        Assert.NotNull(engine.Session);
    }

    [Fact]
    public async Task EvaluateAsync_FenixUnavailableAtFirstStart_DoesNotPauseAndDoesNotSend() {
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();

        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_SnapshotWithoutSimulatorData_ReturnsNoActionAndSkipsSessionAndTrigger() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        SetupFenixFailuresOk();
        var engine = CreateEngine();

        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        // El snapshot del tick pierde los datos coherentes: el tick se salta sin procesar nada.
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(new SimulatorAircraftStateSnapshot {
                IsOnGround = true,
                AltitudeMSL = 100,
                AltitudeAGL = 0,
                HasSimulatorData = false,
            }));

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        _flightEventDetector.Verify(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()), Times.Never);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(ComplexFlightPhaseEnum.Unknown, engine.Session!.CurrentFlightPhase);
        Assert.Null(engine.Session.LastTriggeredAtUtc);
    }

    [Fact]
    public async Task EvaluateAsync_PersistenceFailure_DisarmsSameFailureAndDoesNotReportTrigger() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_ENGINE_1", FailureSeverity.Major);
        SetupValidPersistence(definitions: new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _simulator
            .Setup(s => s.DisarmFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0); // roll del tick
        _random.SetupNextDouble(0.0); // selección de candidato

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        Assert.Null(engine.Session!.LastTriggeredAtUtc);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
        _simulator.Verify(s => s.DisarmFailureAsync("F_ENGINE_1", It.IsAny<CancellationToken>()), Times.Once);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.Is<PresetFailureDefinition>(p => p.FenixFailureId == "F_ENGINE_1"), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_PersistenceAndDisarmBothFail_DoesNotClaimTriggeredFailure() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_ENGINE_1", FailureSeverity.Major);
        SetupValidPersistence(definitions: new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _simulator
            .Setup(s => s.DisarmFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        Assert.Null(engine.Session!.LastTriggeredAtUtc);
        _simulator.Verify(s => s.DisarmFailureAsync("F_ENGINE_1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_PersistenceCanceled_StillAttemptsDisarmAndPropagatesCancellation() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_ENGINE_1", FailureSeverity.Major);
        SetupValidPersistence(definitions: new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _simulator
            .Setup(s => s.DisarmFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Cancelación de persistencia: se distingue del resto; se desarma igual y después se propaga.
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("persistence canceled"));

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.EvaluateAsync(CancellationToken.None));

        Assert.Null(engine.Session!.LastTriggeredAtUtc);
        _simulator.Verify(s => s.DisarmFailureAsync("F_ENGINE_1", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task TryStartSession_ValidConditions_PersistsSessionStartedEvent() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.True(result.Started);
        Assert.NotNull(engine.Session);
        _sessionService.Verify(
            s => s.AddEventLogAsync(42, SessionEventCategory.SessionStarted, "SESSION_STARTED", It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_DetectedFlightEvents_ArePersistedWithCodeCategoryPhase() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        SetupFenixFailuresOk();
        var engine = CreateEngine();

        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Callback(() => {
                _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object, new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos"));
                _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object, new FlightEventDto(DateTime.UtcNow, FlightEventKind.PhaseChanged, "Despegue"));
            })
            .Returns(ComplexFlightPhaseEnum.Takeoff);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        _sessionService.Verify(
            s => s.AddEventLogAsync(42, SessionEventCategory.EnginesStarted, "ENGINES_STARTED", "Motores encendidos", ComplexFlightPhaseEnum.Takeoff, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionService.Verify(
            s => s.AddEventLogAsync(42, SessionEventCategory.PhaseChanged, "TAKEOFF", "Despegue", ComplexFlightPhaseEnum.Takeoff, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_TriggeredFailure_PassesChosenPresetToService() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_ENGINE_1", FailureSeverity.Major);
        chosen.ProbabilityGroup = 2;
        chosen.Ias = "180";
        chosen.Above_Altitude = "10000";
        chosen.Below_Altitude = "30000";
        chosen.Time = "120";
        chosen.AfterEvent = "TAKEOFF";
        chosen.AfterEventSeconds = "15";
        SetupValidPersistence(definitions: new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Single(result.TriggeredFailures);
        Assert.NotNull(engine.Session!.LastTriggeredAtUtc);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(42, It.Is<PresetFailureDefinition>(p =>
                    ReferenceEquals(p, chosen)
                    && p.FenixFailure!.Name == "F_ENGINE_1"
                    && p.Probability == 1.0
                    && p.ProbabilityGroup == 2
                    && p.Ias == "180"
                    && p.AfterEventSeconds == "15"),
                It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_LongCadenceGap_DoesNotBurstDueToDeltaCap() {
        // Delta de 10 h >> EvaluationDeltaCapSeconds (300): la cadencia se calcula sobre el delta acotado,
        // así el tick no resorte tras una caída larga. Con roll 0.05 (mayor que p(300s)≈0.026) no dispara.
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_ENGINE_1", FailureSeverity.Major) });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(10);
        _random.SetupNextDouble(0.05);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(result.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_LowRisk_AllowsMajor_RejectsCritical() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(RiskLevel.Low, new[] { Def("F_ENGINE_1", FailureSeverity.Critical) });
        SetupFenixFailuresOk();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Low), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(result.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_LowRisk_ChoosesMajorCandidate() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_HYD_1", FailureSeverity.Major);
        SetupValidPersistence(RiskLevel.Low, new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Low), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Single(result.TriggeredFailures);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(42, It.Is<PresetFailureDefinition>(p => p.FenixFailureId == "F_HYD_1"), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_CapsCountOnlyOwnFailures_NotExternals() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_ENGINE_1", FailureSeverity.Minor) });
        // 4 fallas propias Minor activas en BD + 1 externa Minor en Fenix (fuera de la cartera de la sesión).
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 4).Select(i => new TriggeredFailure {
                Id = i, FlightSessionId = 42, FenixFailureId = "F_OWN_" + i,
                TriggeredAt = _now, FlightPhase = ComplexFlightPhaseEnum.Unknown,
                State = FailureLifecycleState.Active, Severity = FailureSeverity.Minor
            }).ToArray());
        SetupFenixFailures(new FenixFailureDto("F_EXT_1", "Externa", Failed: true, FailureCondition: null));
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Low), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        // La externa no consume cupo: 4 propias < cap Minor 5, queda lugar para F_ENGINE_1.
        Assert.Single(result.TriggeredFailures);
    }

    [Fact]
    public async Task EvaluateAsync_CapFullFromOwnActive_DoesNotChooseNewFailure() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(RiskLevel.Low, new[] { Def("F_ENGINE_1", FailureSeverity.Minor) });
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 5).Select(i => new TriggeredFailure {
                Id = i, FlightSessionId = 42, FenixFailureId = "F_OWN_" + i,
                TriggeredAt = _now, FlightPhase = ComplexFlightPhaseEnum.Unknown,
                State = FailureLifecycleState.Active, Severity = FailureSeverity.Minor
            }).ToArray());
        SetupFenixFailuresOk();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Low), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(result.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_FirstStartWithoutFenix_PreparesInQueueWithoutSending() {
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_ENGINE_1", FailureSeverity.Minor) });
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);
        Assert.Equal(0, engine.PreparedCount);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Equal(1, engine.PreparedCount);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_OfflinePrepared_FlushedWhenFenixAvailable() {
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        var prepared = Def("F_ENGINE_1", FailureSeverity.Major);
        SetupValidPersistence(definitions: new[] { prepared });
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await engine.EvaluateAsync(CancellationToken.None);
        Assert.Equal(1, engine.PreparedCount);

        // Fenix vuelve: el siguiente tick envía la preparada (único envío del tick).
        SetupConnected(fenixConnected: true);
        SetupFenixFailuresOk();
        _random.SetupNextDouble(0.0); // consumido por el re-check de conexión solo si falla el envío
        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Single(result.TriggeredFailures);
        Assert.Equal(0, engine.PreparedCount);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(42, It.Is<PresetFailureDefinition>(p => p.FenixFailureId == "F_ENGINE_1"), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_OfflinePrepared_RevalidatedAgainstNewPhase_DroppedIfIneligible() {
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_ENGINE_1", FailureSeverity.Major) });
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await engine.EvaluateAsync(CancellationToken.None);
        Assert.Equal(1, engine.PreparedCount);

        // Fase cambió a Takeoff (el preset es de fase Unknown): la preparada ya no es elegible y se descarta.
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, true, SimpleFlightPhaseEnum.Flying));
        SetupFenixFailuresOk();
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Returns(ComplexFlightPhaseEnum.Takeoff);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(result.TriggeredFailures);
        Assert.Equal(0, engine.PreparedCount);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_PreparedItemKeptWhenSeverityCapTemporarilyFull() {
        // Reservas de cola contadas una sola vez (no por item): 3 propias + 2 preparadas Minor llenan el cupo 5;
        // la preparada se CONSERVA (no se descarta ni se elimina por un cupo temporal).
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence(RiskLevel.Low, new[] { Def("F_A", FailureSeverity.Minor), Def("F_B", FailureSeverity.Minor) });
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 3).Select(i => new TriggeredFailure {
                Id = i, FlightSessionId = 42, FenixFailureId = "F_OWN_" + i,
                TriggeredAt = _now, FlightPhase = ComplexFlightPhaseEnum.Unknown,
                State = FailureLifecycleState.Active, Severity = FailureSeverity.Minor
            }).ToArray());
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Low), CancellationToken.None);
        Assert.True(start.Started);

        // Preparar 2 en cola offline (el segundo evaluá con otro salto de reloj).
        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await engine.EvaluateAsync(CancellationToken.None);
        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await engine.EvaluateAsync(CancellationToken.None);
        Assert.Equal(2, engine.PreparedCount);

        SetupConnected(fenixConnected: true);
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(result.TriggeredFailures);
        Assert.Equal(2, engine.PreparedCount);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_FenixLostAfterHavingIt_WaitsWithoutChoosingOrResolving() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_ENGINE_1", FailureSeverity.Major) });
        SetupFenixFailuresOk();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        var result = await engine.EvaluateAsync(CancellationToken.None);
        Assert.False(result.ShouldPauseSession);
        Assert.True(engine.Session!.AllFenixFailures.Count == 0); // la lectura vacía marcó hasEverHadFenixRead

        // Pérdida posterior de Fenix: la sesión espera sin elegir, enviar ni reconciliar.
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Fail(new InvalidOperationException("fenix gone")));

        var waiting = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(waiting.ShouldPauseSession);
        Assert.Empty(waiting.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionService.Verify(
            s => s.UpdateTriggeredFailureStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<FailureLifecycleState>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<ResolutionSource?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_NullFenixRead_TreatedAsUnavailableWithoutResolve() {
        SetupConnected();
        SetupOnGateData();
        var persisted = new TriggeredFailure {
            Id = 8, FlightSessionId = 42, FenixFailureId = "F_HYD_1",
            TriggeredAt = _now, FlightPhase = ComplexFlightPhaseEnum.Climb,
            State = FailureLifecycleState.Active, Severity = FailureSeverity.Major
        };
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { persisted });
        SetupValidPersistence();
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(null!));
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        _sessionService.Verify(
            s => s.UpdateTriggeredFailureStateAsync(It.IsAny<int>(), "F_HYD_1", It.IsAny<FailureLifecycleState>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<ResolutionSource?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_ActiveFailureIdAbsentFromFenixRead_DoesNotResolve() {
        SetupConnected();
        SetupOnGateData();
        var persisted = new TriggeredFailure {
            Id = 8, FlightSessionId = 42, FenixFailureId = "F_HYD_1",
            TriggeredAt = _now, FlightPhase = ComplexFlightPhaseEnum.Climb,
            State = FailureLifecycleState.Active, Severity = FailureSeverity.Major
        };
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { persisted });
        SetupValidPersistence();
        // Lectura válida pero sin el id de la falla en curso: no hay transición.
        SetupFenixFailures(new FenixFailureDto("F_OTHER_1", "Otra", Failed: false, FailureCondition: null));
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        _sessionService.Verify(
            s => s.UpdateTriggeredFailureStateAsync(It.IsAny<int>(), "F_HYD_1", It.IsAny<FailureLifecycleState>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<ResolutionSource?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_CriticalTrigger_SetsGeneralRespite_NoNewSendsWithinIt() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence(RiskLevel.High, new[] { Def("F_ENGINE_1", FailureSeverity.Critical) });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.High), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        var first = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Single(first.TriggeredFailures);
        Assert.True(engine.Session!.CriticalRespiteUntilUtc > _now);

        // Dentro del respiro (High: 600 s) no se elige ni se envía nada nuevo.
        _now = _now.AddMinutes(5);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        var second = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Empty(second.TriggeredFailures);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_Squawk7700_HasNoCaps_ChoosesCritical() {
        SetupConnected();
        SetupOnGateData();
        var chosen = Def("F_ENGINE_1", FailureSeverity.Critical);
        SetupValidPersistence(RiskLevel.Squawk7700, new[] { chosen });
        SetupFenixFailuresOk();
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<PresetFailureDefinition>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(RiskLevel.Squawk7700), CancellationToken.None);
        Assert.True(start.Started);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.Single(result.TriggeredFailures);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(42, It.Is<PresetFailureDefinition>(p => p.FenixFailureId == "F_ENGINE_1"), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PreparedQueue_LimitEnforcedWithUniqueIds_AndDiscardClears() {
        var settings = new RealisticModeSettings { PreparedQueueLimit = 2 };
        SetupConnected(fenixConnected: false);
        SetupOnGateData();
        SetupValidPersistence(definitions: new[] { Def("F_A", FailureSeverity.Minor), Def("F_B", FailureSeverity.Minor), Def("F_C", FailureSeverity.Minor) });
        var engine = CreateEngine(settings);
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        for (var i = 0; i < 2; i++) {
            _now = _now.AddHours(1);
            _random.SetupNextDouble(0.0);
            _random.SetupNextDouble(0.0);
            await engine.EvaluateAsync(CancellationToken.None);
        }
        Assert.Equal(2, engine.PreparedCount);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await engine.EvaluateAsync(CancellationToken.None);
        Assert.Equal(2, engine.PreparedCount); // tope respetado (ids únicos: F_A y F_B)

        var discarded = engine.DiscardPrepared();
        Assert.Equal(2, discarded);
        Assert.Equal(0, engine.PreparedCount);
    }

    [Fact]
    public async Task StopSession_UnsubscribesAndIgnoresLaterFlightEvents() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();

        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        await engine.StopSession(CancellationToken.None);

        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.AddEventLogAsync(42, SessionEventCategory.SessionStopped, "SESSION_STOPPED", It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionService.Verify(
            s => s.StopAsync(42, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object, new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos"));

        _sessionService.Verify(
            s => s.AddEventLogAsync(42, SessionEventCategory.EnginesStarted, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_SentFailureConfirmedByFenix_TransitionsToActive() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        // Falla persistida en BD como Sent.
        var persisted = new TriggeredFailure {
            Id = 7,
            FlightSessionId = 42,
            FenixFailureId = "F_ENGINE_1",
            TriggeredAt = _now,
            FlightPhase = ComplexFlightPhaseEnum.Takeoff,
            State = FailureLifecycleState.Sent,
            Severity = FailureSeverity.Major
        };
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { persisted });

        // Fenix la reporta activa (failed=true).
        SetupFenixFailures(new FenixFailureDto("F_ENGINE_1", "Motor 1", Failed: true, FailureCondition: null));

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        _sessionService.Verify(
            s => s.UpdateTriggeredFailureStateAsync(42, "F_ENGINE_1", FailureLifecycleState.Active, It.IsAny<ComplexFlightPhaseEnum>(), null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_ActiveFailureClearedByFenix_TransitionsToResolvedByPilot() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        var persisted = new TriggeredFailure {
            Id = 8,
            FlightSessionId = 42,
            FenixFailureId = "F_HYD_1",
            TriggeredAt = _now,
            FlightPhase = ComplexFlightPhaseEnum.Climb,
            State = FailureLifecycleState.Active,
            Severity = FailureSeverity.Major
        };
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { persisted });

        // El piloto despejó la falla en Fenix: ya no failed, ya no armada.
        SetupFenixFailures(new FenixFailureDto("F_HYD_1", "Hydraul", Failed: false, FailureCondition: null));

        await engine.EvaluateAsync(CancellationToken.None);

        _sessionService.Verify(
            s => s.UpdateTriggeredFailureStateAsync(42, "F_HYD_1", FailureLifecycleState.ResolvedByPilot, It.IsAny<ComplexFlightPhaseEnum>(), ResolutionSource.Pilot, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
