// RealFenixFailures.TestUnits/Services/RealisticSessionManagerTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Mocks;

namespace RealFenixFailures.TestUnits.Services;

public class RealisticSessionManagerTests {
    private readonly Mock<ISimulatorConnectionService> _simulator = new();
    private readonly Mock<IFlightSessionService> _sessionService = new();
    private readonly Mock<IPresetService> _presetService = new();
    private readonly Mock<IFailureTrigger> _failureTrigger = new();
    private readonly Mock<IFlightEventDetector> _flightEventDetector = new()!;
    private readonly MockRandom _random = new();
    private DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private Mock<ITriggeredFailureRepository> _triggeredFailureRepository = null!;
    private Mock<IFlightPlanRepository> _flightPlanRepository = new();
    private Mock<IServiceScope> _scope = null!;
    private readonly Mock<IAircraftSystemWearRepository> _wearRepository = new();
    private readonly Mock<IFenixFailureDefinitionRepository> _fenixFailureDefinitions = new();

    private RealisticSessionManager CreateManager(TimeSpan? evaluationInterval = null, Func<IFailureRuleEvaluator, RealisticFailurePolicy>? policyFactory = null, RealisticModeSettings? realisticSettings = null) {
        _triggeredFailureRepository = new Mock<ITriggeredFailureRepository>();
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TriggeredFailure>());

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IFlightSessionService))).Returns(_sessionService.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IPresetService))).Returns(_presetService.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IFailureTrigger))).Returns(_failureTrigger.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(ITriggeredFailureRepository))).Returns(_triggeredFailureRepository.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IFlightPlanRepository))).Returns(_flightPlanRepository.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IAircraftSystemWearRepository))).Returns(_wearRepository.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IFenixFailureDefinitionRepository))).Returns(_fenixFailureDefinitions.Object);

        _scope = new Mock<IServiceScope>();
        _scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);

        return new RealisticSessionManager(
            NullLogger<RealisticSessionManager>.Instance,
            NullLoggerFactory.Instance,
            scopeFactory.Object,
            _simulator.Object,
            _flightEventDetector.Object,
            evaluationInterval,
            realisticSettings is null ? null : Microsoft.Extensions.Options.Options.Create(new FailureEngineSettings { RealisticMode = realisticSettings }),
            policyFactory: policyFactory);
    }

    [Fact]
    public async Task StopAsync_TwoScenarioHours_SavesWearAndPassesDeltaToFinalization() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailuresOk();
        var wear = new AircraftSystemWear {
            UserAircraftId = 1, WearPercentage = 50,
            WearableSystem = new AircraftWearableSystem { AtaId = 24, Name = "Electrical", ShortName = "Elec", WearPerFlightHour = 0.6 }
        };
        _wearRepository.Setup(r => r.GetForAircraftAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([wear]);
        var manager = CreateManager(TimeSpan.FromMilliseconds(20),
            evaluator => new RealisticFailurePolicy(evaluator, () => _now, _random));
        // El anclaje de desgaste es el ENGINES_STARTED: se lo hace llegar al engine por el loop real.
        _sessionService
            .Setup(s => s.AddEventLogAsync(It.IsAny<int>(), It.IsAny<SessionEventCategory>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Assert.True((await manager.StartNewSessionAsync(Context(), CancellationToken.None)).IsSuccess);
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Callback(() => _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object,
                new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos")))
            .Returns(ComplexFlightPhaseEnum.Cruise);
        await Task.Delay(200);
        _now = _now.AddHours(2);

        await manager.StopAsync(CancellationToken.None);

        Assert.Equal(51.2, wear.WearPercentage, 6);
        _wearRepository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sessionService.Verify(s => s.FinalizeSessionAsync(1, It.IsAny<DateTime>(),
            It.Is<IReadOnlyList<SystemWearDeltaDto>>(d => d.Count == 1 && d[0].AtaId == 24
                && Math.Abs(d[0].DeltaPercentage - 1.2) < 0.000001), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WearCap_AccumulatesRandomSubsetOfSystems() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailuresOk();
        var settings = new RealisticModeSettings { Wear = new WearSettings { SessionSystemsCap = 2 } };
        AircraftSystemWear NewWear(int ataId) => new() {
            UserAircraftId = 1, WearPercentage = 0,
            WearableSystem = new AircraftWearableSystem { AtaId = ataId, Name = $"S{ataId}", ShortName = $"S{ataId}", WearPerFlightHour = 0.6 }
        };
        var w21 = NewWear(21);
        var w22 = NewWear(22);
        var w23 = NewWear(23);
        var w24 = NewWear(24);
        _wearRepository.Setup(r => r.GetForAircraftAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([w21, w22, w23, w24]);
        var manager = CreateManager(TimeSpan.FromMilliseconds(20),
            evaluator => new RealisticFailurePolicy(evaluator, () => _now, _random), settings);
        // El anclaje de desgaste es el ENGINES_STARTED: se lo hace llegar al engine por el loop real.
        _sessionService
            .Setup(s => s.AddEventLogAsync(It.IsAny<int>(), It.IsAny<SessionEventCategory>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Assert.True((await manager.StartNewSessionAsync(Context(), CancellationToken.None)).IsSuccess);
        _flightEventDetector
            .Setup(f => f.ProcessSnapshot(It.IsAny<SimulatorAircraftStateSnapshot>()))
            .Callback(() => _flightEventDetector.Raise(d => d.FlightEventDetected += null, _flightEventDetector.Object,
                new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos")))
            .Returns(ComplexFlightPhaseEnum.Cruise);
        await Task.Delay(200);
        _now = _now.AddHours(2);

        await manager.StopAsync(CancellationToken.None);

        _sessionService.Verify(s => s.FinalizeSessionAsync(1, It.IsAny<DateTime>(),
            It.Is<IReadOnlyList<SystemWearDeltaDto>>(d => d.Count >= 1 && d.Count <= 2), It.IsAny<CancellationToken>()), Times.Once);
        _wearRepository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        var untouched = new[] { w21, w22, w23, w24 }.Count(w => w.WearPercentage == 0);
        Assert.True(untouched >= 2);
    }

    private void SetupConnected() {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, true, SimpleFlightPhaseEnum.OnGround));
    }

    private void SetupValidData(IEnumerable<PresetFailureDefinition>? definitions = null) {
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(new SimulatorAircraftStateSnapshot {
                IsOnGround = true,
                AltitudeMSL = 100,
                AltitudeAGL = 0,
                HasSimulatorData = true,
            }));
        _sessionService
            .Setup(s => s.StartNewAsync(It.IsAny<RealisticModeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightSession { Id = 1, FlightPlanId = 1, StartedAt = _now, RiskLevel = RiskLevel.Moderate, UserAircraftId = 1 });
        _flightPlanRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightPlan {
                Id = 1, OriginIcao = "SAEZ", DestinationIcao = "SBGR", AircraftIcaoType = "A320",
                Source = "SIMBRIEF", DownloadedAtUtc = _now
            });
        var presets = definitions is null
            ? new List<FailurePreset>()
            : new List<FailurePreset> {
                new() { Id = 1, Name = "PRESET", FlightPhase = ComplexFlightPhaseEnum.Unknown, PresetFailureDefinitions = definitions.ToList() }
            };
        // Universo de fallas aleatorias = catálogo completo (tipos-de-falla.md §4.2): las entidades de las
        // definiciones hacen de catálogo de prueba.
        _fenixFailureDefinitions
            .Setup(r => r.GetAllWithSystemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions?.Select(d => d.FenixFailure!).Where(e => e is not null).ToList() ?? []);
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(presets);
    }

    /// <summary>Fenix responde fallas vacías: un tick normal no arma nada y no falla.</summary>
    private void SetupFenixFailuresOk() {
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(new AllFenixFailuresResponseDto()));
    }

    private static RealisticModeContext Context() =>
        new(RiskLevel.Moderate, new UserAircraftDto { Id = 1, Registration = "LV-XYZ" }, FlightPlanId: 1);

    [Fact]
    public async Task StartNewSessionAsync_InvalidStart_ClearsStateAndAllowsRetry() {
        SetupConnected();
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Fail(new InvalidOperationException()));
        var manager = CreateManager();

        var first = await manager.StartNewSessionAsync(Context(), CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.Null(manager.SessionState);
        Assert.False(manager.IsPaused);
        _scope.Verify(s => s.Dispose(), Times.Once);
        _sessionService.Verify(
            s => s.StartNewAsync(It.IsAny<RealisticModeContext>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // A retry with valid simulator data must succeed.
        SetupValidData();
        var second = await manager.StartNewSessionAsync(Context(), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.NotNull(manager.SessionState);

        await manager.StopAsync(CancellationToken.None);
        Assert.Null(manager.SessionState);
    }

    [Fact]
    public async Task Pause_Resume_AreIdempotentAndToggleIsPaused() {
        SetupConnected();
        SetupValidData();
        var manager = CreateManager();

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);
        Assert.False(manager.IsPaused);
        _scope.Verify(s => s.Dispose(), Times.Never);

        await manager.PauseAsync(CancellationToken.None);
        Assert.True(manager.IsPaused);

        await manager.PauseAsync(CancellationToken.None);
        Assert.True(manager.IsPaused);

        await manager.ResumeAsync(CancellationToken.None);
        Assert.False(manager.IsPaused);

        await manager.ResumeAsync(CancellationToken.None);
        Assert.False(manager.IsPaused);

        await manager.StopAsync(CancellationToken.None);
        Assert.Null(manager.SessionState);
        _scope.Verify(s => s.Dispose(), Times.Once);
        _sessionService.Verify(
            s => s.FinalizeSessionAsync(1, It.IsAny<DateTime>(), It.IsAny<IReadOnlyList<SystemWearDeltaDto>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisconnectPause_KeepsSessionForResume_AndIsNotFatal() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailuresOk();
        var manager = CreateManager(TimeSpan.FromMilliseconds(20));

        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(manager.IsPaused) && manager.IsPaused) paused.TrySetResult();
        };

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);

        // Pérdida de datos del simulador: el siguiente tick debe pausar, no tratar como fallo fatal.
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(false, false, SimpleFlightPhaseEnum.Disconnected));

        await paused.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(manager.IsPaused);
        Assert.NotNull(manager.SessionState); // el engine/scope se conserva para reanudar
        _scope.Verify(s => s.Dispose(), Times.Never());

        // Conexión restablecida: reanudar manualmente sigue funcionando sobre la misma sesión.
        SetupConnected();
        await manager.ResumeAsync(CancellationToken.None);
        Assert.False(manager.IsPaused);
        Assert.NotNull(manager.SessionState);

        await manager.StopAsync(CancellationToken.None);
        Assert.Null(manager.SessionState);
        _scope.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task FatalEvaluationError_ClearsManagerStateAndNotifiesSessionError() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailuresOk();
        var manager = CreateManager(TimeSpan.FromMilliseconds(20));

        var errorFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.SessionError += (_, _) => errorFired.TrySetResult();

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);

        // Fallo fatal de evaluación: excepción no relacionada con desconexión ni cancelación.
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("evaluation exploded"));

        await errorFired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(manager.SessionState);
        Assert.False(manager.IsPaused);
        _scope.Verify(s => s.Dispose(), Times.Once);
        _sessionService.Verify(
            s => s.FinalizeSessionAsync(1, It.IsAny<DateTime>(), It.IsAny<IReadOnlyList<SystemWearDeltaDto>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Pause_DisarmsActiveFailuresInFenix() {
        SetupConnected();
        SetupValidData();
        var fenixFailure = new FenixFailureDto("F_ENGINE_1", "Motor 1", Failed: true, FailureCondition: null);
        SetupFenixFailures(fenixFailure);

        var manager = CreateManager(TimeSpan.FromMilliseconds(20));

        // El repo reporta la falla en curso para la sesión.
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TriggeredFailure {
                Id = 5,
                FlightSessionId = 1,
                FenixFailureId = "F_ENGINE_1",
                TriggeredAt = DateTime.UtcNow,
                FlightPhase = ComplexFlightPhaseEnum.Takeoff,
                State = FailureLifecycleState.Active
            } });

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);

        // Esperar a que el tick procese la falla de Fenix como activa en la sesión.
        await Task.Delay(200);

        await manager.PauseAsync(CancellationToken.None);
        Assert.True(manager.IsPaused);

        _simulator.Verify(
            s => s.DisarmFailureAsync("F_ENGINE_1", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Pause_DisarmsActiveFailuresAndBlocksNewEvaluationsWhilePaused() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailures(new FenixFailureDto("F_ENGINE_1", "Motor 1", Failed: true, FailureCondition: null));

        var manager = CreateManager(TimeSpan.FromMilliseconds(20));

        // El repo reporta la falla en curso activa para la sesión.
        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TriggeredFailure {
                Id = 7,
                FlightSessionId = 1,
                FenixFailureId = "F_ENGINE_1",
                TriggeredAt = DateTime.UtcNow,
                FlightPhase = ComplexFlightPhaseEnum.Takeoff,
                State = FailureLifecycleState.Active
            } });

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);
        Assert.False(manager.IsPaused);

        // Confirmar que el loop evalúa (consulta fallas Fenix) mientras la sesión está activa.
        await Task.Delay(200);
        var evaluationsWhileActive = _simulator.Invocations.Count(i =>
            i.Method.Name == nameof(ISimulatorConnectionService.GetCurrentFenixFailures));
        Assert.True(evaluationsWhileActive > 0);

        await manager.PauseAsync(CancellationToken.None);
        Assert.True(manager.IsPaused);

        // Pausa: desarma la falla activa de la sesión en Fenix.
        _simulator.Verify(
            s => s.DisarmFailureAsync("F_ENGINE_1", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Pausada: el loop quedó detenido y ningún tick nuevo consulta/evalúa fallas.
        var evaluationsAtPause = _simulator.Invocations.Count(i =>
            i.Method.Name == nameof(ISimulatorConnectionService.GetCurrentFenixFailures));
        await Task.Delay(200);
        var evaluationsWhilePaused = _simulator.Invocations.Count(i =>
            i.Method.Name == nameof(ISimulatorConnectionService.GetCurrentFenixFailures));
        Assert.Equal(evaluationsAtPause, evaluationsWhilePaused);

        await manager.StopAsync(CancellationToken.None);
        Assert.Null(manager.SessionState);
    }

    [Fact]
    public async Task Resume_WithRestoreFailures_TogglesAndDoesNotThrow() {
        SetupConnected();
        SetupValidData();
        SetupFenixFailuresOk();

        var manager = CreateManager(TimeSpan.FromMilliseconds(20));

        _triggeredFailureRepository
            .Setup(r => r.GetActiveForSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TriggeredFailure {
                Id = 6,
                FlightSessionId = 1,
                FenixFailureId = "F_HYD_1",
                TriggeredAt = DateTime.UtcNow,
                FlightPhase = ComplexFlightPhaseEnum.Climb,
                State = FailureLifecycleState.Queued
            } });

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);

        await manager.PauseAsync(CancellationToken.None);
        Assert.True(manager.IsPaused);

        await manager.ResumeAsync(CancellationToken.None, restoreFailures: true);

        Assert.False(manager.IsPaused);
        _scope.Verify(s => s.Dispose(), Times.Never);
    }

    private void SetupFenixFailures(params FenixFailureDto[] failures) {
        var grouped = new AllFenixFailuresResponseDto {
            MajorGroups = new List<FenixMajorSystemGroupDto> {
                new(0, "Systems", "SYS", new List<FenixSystemGroupDto> { new("Group", failures) })
            }
        };
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto?>.Ok(grouped));
    }

    [Fact]
    public async Task EvaluationLoop_PreparesWithoutFenix_CountAndDiscardReflected() {
        // Arranque sin Fenix: el loop de evaluación (interval corto en test) prepara en cola
        // y PreparedCount/DiscardPreparedAsync reflejan la cola del engine.
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, false, SimpleFlightPhaseEnum.OnGround));
        SetupValidData(new[] { new PresetFailureDefinition {
            FenixFailureId = "F_ENGINE_1",
            Probability = 1.0,
            FenixFailure = new FenixFailureDefinition {
                FenixFailureId = "F_ENGINE_1",
                Name = "Motor 1",
                Severity = FailureSeverity.Minor,
                Group = new FenixFailureGroup {
                    Name = "G-ENGINE",
                    System = new FenixFailureSystem { Name = "S-ENGINE", ShortName = "S" }
                }
            }
        } });
        Func<IFailureRuleEvaluator, RealisticFailurePolicy> policyFactory =
            _ => new RealisticFailurePolicy(new FailureRuleEvaluator(), () => _now, _random);
        var manager = CreateManager(TimeSpan.FromMilliseconds(20), policyFactory);

        var preparedChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(manager.PreparedCount) && manager.PreparedCount > 0) preparedChanged.TrySetResult();
        };

        var start = await manager.StartNewSessionAsync(Context(), CancellationToken.None);
        Assert.True(start.IsSuccess);
        Assert.Equal(0, manager.PreparedCount);

        _now = _now.AddHours(1);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.0);
        await preparedChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(manager.PreparedCount > 0);

        var discarded = await manager.DiscardPreparedAsync(CancellationToken.None);
        Assert.True(discarded > 0);

        await manager.StopAsync(CancellationToken.None);
        Assert.Equal(0, manager.PreparedCount);
        Assert.Null(manager.SessionState);
    }
}
