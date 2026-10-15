// RealFenixFailures.TestUnits/Services/RealisticSessionManagerTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;

namespace RealFenixFailures.TestUnits.Services;

public class RealisticSessionManagerTests {
    private readonly Mock<ISimulatorConnectionService> _simulator = new();
    private readonly Mock<IFlightSessionService> _sessionService = new();
    private readonly Mock<IPresetService> _presetService = new();
    private readonly Mock<IFailureTrigger> _failureTrigger = new();
    private readonly Mock<IFlightEventDetector> _flightEventDetector = new();
    private Mock<IServiceScope> _scope = null!;

    private RealisticSessionManager CreateManager(TimeSpan? evaluationInterval = null) {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IFlightSessionService))).Returns(_sessionService.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IPresetService))).Returns(_presetService.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IFailureTrigger))).Returns(_failureTrigger.Object);

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
            evaluationInterval);
    }

    private void SetupConnected() {
        _simulator
            .Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto(true, true, SimpleFlightPhaseEnum.OnGround));
    }

    private void SetupValidData() {
        _simulator
            .Setup(s => s.GetSimulatorData(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SimulatorAircraftStateSnapshot>.Ok(new SimulatorAircraftStateSnapshot {
                IsOnGround = true,
                AltitudeMSL = 100,
                AltitudeAGL = 0,
                HasSimulatorData = true,
            }));
        _sessionService
            .Setup(s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlightSession { Id = 1, StartedAt = DateTime.UtcNow, RiskLevel = RiskLevel.Moderate, UserAircraftId = 1 });
        _presetService
            .Setup(p => p.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FailurePreset>());
    }

    /// <summary>Fenix responde fallas vacías: un tick normal no arma nada y no falla.</summary>
    private void SetupFenixFailuresOk() {
        _simulator
            .Setup(s => s.GetCurrentFenixFailures(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AllFenixFailuresResponseDto>.Ok(new AllFenixFailuresResponseDto()));
    }

    private static RealisticModeContext Context() =>
        new(RiskLevel.Moderate, new UserAircraftDto { Id = 1, Registration = "LV-XYZ" });

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
            s => s.StartNewAsync(It.IsAny<RiskLevel>(), It.IsAny<UserAircraftDto>(), It.IsAny<CancellationToken>()),
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
            s => s.StopAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
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
            s => s.StopAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
