// RealFenixFailures.TestUnits/Services/SimulatorConnectionServiceTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Interfaces;

namespace RealFenixFailures.TestUnits.Services;

public class SimulatorConnectionServiceTests {
    private readonly Mock<IFenixFailureApiDispatcher> _dispatcher = new();
    private readonly SimulatorConnectionService _service;

    public SimulatorConnectionServiceTests() {
        _service = new SimulatorConnectionService(
            NullLogger<SimulatorConnectionService>.Instance,
            _dispatcher.Object,
            new Mock<IFailureTrigger>().Object,
            new Mock<ISimFlightDataProvider>().Object);
    }

    [Fact]
    public async Task DisarmFailureAsync_ReusesRequestWithoutArmedCondition() {
        _dispatcher
            .Setup(d => d.DisarmFailureAsync(It.IsAny<FenixArmFailureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DisarmFailureAsync("F_ENGINE_1", CancellationToken.None);

        Assert.True(result);
        _dispatcher.Verify(
            d => d.DisarmFailureAsync(new FenixArmFailureRequest("F_ENGINE_1", false, null), It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcher.Verify(d => d.ArmFailureAsync(It.IsAny<FenixArmFailureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisarmFailureAsync_EmptyId_ReturnsFalseWithoutCallingDispatcher() {
        var result = await _service.DisarmFailureAsync("", CancellationToken.None);

        Assert.False(result);
        _dispatcher.Verify(d => d.DisarmFailureAsync(It.IsAny<FenixArmFailureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
