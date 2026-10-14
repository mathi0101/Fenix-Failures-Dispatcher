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

namespace RealFenixFailures.TestUnits.Session;

public class RealisticSessionEngineTests {
    private readonly Mock<ISimulatorConnectionService> _simulator = new();
    private readonly Mock<IFlightSessionService> _sessionService = new();
    private readonly Mock<IPresetService> _presetService = new();
    private readonly Mock<IFailureTrigger> _failureTrigger = new();
    private readonly Mock<IFlightEventDetector> _flightEventDetector = new();

    private RealisticSessionEngine CreateEngine() => new(
        _simulator.Object,
        NullLogger<RealisticSessionEngine>.Instance,
        _sessionService.Object,
        _presetService.Object,
        _failureTrigger.Object,
        _flightEventDetector.Object);

    private static RealisticModeContext Context() =>
        new(RiskLevel.Moderate, new UserAircraftDto { Id = 1, Registration = "LV-XYZ" });

    private static SimulatorAircraftStateSnapshot OnGate() =>
        new() { IsOnGround = true, AltitudeMSL = 100, AltitudeAGL = 0 };

    private static SimulatorAircraftStateSnapshot EnginesRunningOnGround() =>
        new() {
            IsOnGround = true,
            AltitudeMSL = 100,
            AltitudeAGL = 0,
            Engine1Combustion = true,
            Engine2Combustion = false,
            Engine1N1Percent = 40,
            Engine2N1Percent = 0,
            GroundSpeed = 0,
        };

    private void SetupConnected() {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, true, SimpleFlightPhaseEnum.OnGround));
    }

    private void SetupOnGateData() {
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(OnGate()));
    }

    private void SetupValidPersistence() {
        _sessionService
            .Setup(s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightSession { Id = 42, StartedAt = DateTime.UtcNow, RiskLevel = RiskLevel.Moderate, UserAircraftId = 1 });
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FailurePreset> {
                new FailurePreset { Id = 1, Name = "PRESET-A", FlightPhase = ComplexFlightPhaseEnum.Unknown },
                new FailurePreset { Id = 2, Name = "PRESET-B", FlightPhase = ComplexFlightPhaseEnum.Unknown }
            });
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
    public async Task TryStartSession_AircraftNotParkedWithEnginesOff_DoesNotPersistSession() {
        SetupConnected();
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(EnginesRunningOnGround()));
        var engine = CreateEngine();

        var result = await engine.TryStartSession(Context(), CancellationToken.None);

        Assert.False(result.Started);
        Assert.Null(engine.Session);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            .ReturnsAsync(new FlightSession { Id = 42, StartedAt = DateTime.UtcNow, RiskLevel = RiskLevel.Moderate, UserAircraftId = 1 });
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
    public async Task EvaluateAsync_FenixFailuresUnavailable_ReturnsDisconnectedWithoutThrowing() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var engine = CreateEngine();

        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto>.Fail(new InvalidOperationException()));

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.True(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
    }

    private void SetupTriggerPath(PresetFailureDefinition chosen, bool disarmResult) {
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto>.Ok(new AllFenixFailuresResponseDto()));
        _simulator
            .Setup(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _simulator
            .Setup(s => s.DisarmFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(disarmResult);
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FailurePreset> {
                new FailurePreset {
                    Id = 1,
                    Name = "TEST",
                    FlightPhase = ComplexFlightPhaseEnum.Unknown,
                    PresetFailureDefinitions = { chosen }
                }
            });
        _failureTrigger
            .Setup(f => f.TryTriggerFailure(It.IsAny<FailurePreset>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<RiskLevel>(), It.IsAny<double>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(chosen);
    }

    [Fact]
    public async Task EvaluateAsync_PersistenceFailure_DisarmsSameFailureAndDoesNotReportTrigger() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var chosen = new PresetFailureDefinition {
            FenixFailureId = "F_ENGINE_1",
            Probability = 1.0
        };
        SetupTriggerPath(chosen, disarmResult: true);

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        var result = await engine.EvaluateAsync(CancellationToken.None);

        Assert.False(result.ShouldPauseSession);
        Assert.Empty(result.TriggeredFailures);
        Assert.Null(engine.Session!.LastTriggeredAtUtc);
        _simulator.Verify(s => s.ExecuteFailureAsync(It.IsAny<PresetFailureDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
        _simulator.Verify(s => s.DisarmFailureAsync("F_ENGINE_1", It.IsAny<CancellationToken>()), Times.Once);
        _sessionService.Verify(
            s => s.AddTriggeredFailureAsync(It.IsAny<int>(), "F_ENGINE_1", It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_PersistenceAndDisarmBothFail_DoesNotClaimTriggeredFailure() {
        SetupConnected();
        SetupOnGateData();
        SetupValidPersistence();
        var chosen = new PresetFailureDefinition {
            FenixFailureId = "F_ENGINE_1",
            Probability = 1.0
        };
        SetupTriggerPath(chosen, disarmResult: false);

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

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
        SetupValidPersistence();
        var chosen = new PresetFailureDefinition {
            FenixFailureId = "F_ENGINE_1",
            Probability = 1.0
        };
        SetupTriggerPath(chosen, disarmResult: true);
        // Cancelación de persistencia: se distingue del resto; se desarma igual y después se propaga.
        _sessionService
            .Setup(s => s.AddTriggeredFailureAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("persistence canceled"));

        var engine = CreateEngine();
        var start = await engine.TryStartSession(Context(), CancellationToken.None);
        Assert.True(start.Started);

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.EvaluateAsync(CancellationToken.None));

        Assert.Null(engine.Session!.LastTriggeredAtUtc);
        _simulator.Verify(s => s.DisarmFailureAsync("F_ENGINE_1", CancellationToken.None), Times.Once);
    }
}
