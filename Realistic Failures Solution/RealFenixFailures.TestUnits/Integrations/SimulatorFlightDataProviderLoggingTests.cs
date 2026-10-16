using Microsoft.Extensions.Logging;
using Moq;
using RealFenixFailures.Integrations.SimConnect.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Services;
using RealFenixFailures.TestUnits.Logging;

namespace RealFenixFailures.TestUnits.Integrations;

public class SimulatorFlightDataProviderLoggingTests {
    private readonly List<LogEntry> _entries = new();
    private readonly RecordingLoggerFactory _loggerFactory;

    public SimulatorFlightDataProviderLoggingTests() {
        _loggerFactory = new RecordingLoggerFactory(_entries);
    }

    private SimulatorFlightDataProvider CreateProvider(Mock<ISimConnectClient> client) {
        return new SimulatorFlightDataProvider(
            client.Object,
            new RecordingLogger<SimulatorFlightDataProvider>(_entries),
            _loggerFactory);
    }

    [Fact]
    public async Task FirstCheckFalse_DoesNotLogDisconnection() {
        var client = new Mock<ISimConnectClient>();
        client.Setup(c => c.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var provider = CreateProvider(client);

        var result = await provider.IsConnectedAsync(CancellationToken.None);

        Assert.False(result);
        Assert.DoesNotContain(_entries, e => e.Message.Contains("SimConnect desconectado"));
        Assert.DoesNotContain(_entries, e => e.Message.Contains("SimConnect conectado"));
    }

    [Fact]
    public async Task FirstCheckTrue_LogsSingleConnectionInfoAndNothingOnRepeatedCalls() {
        var client = new Mock<ISimConnectClient>();
        client.Setup(c => c.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var provider = CreateProvider(client);

        await provider.IsConnectedAsync(CancellationToken.None);
        await provider.IsConnectedAsync(CancellationToken.None);
        await provider.IsConnectedAsync(CancellationToken.None);

        var transitions = _entries.Where(e => e.Message.Contains("SimConnect conectado") || e.Message.Contains("SimConnect desconectado")).ToList();
        Assert.Single(transitions);
        Assert.Equal(LogLevel.Information, transitions[0].Level);
        Assert.Equal("SimConnect conectado", transitions[0].Message);
    }

    [Fact]
    public async Task TimeoutOnFirstCheck_NoWarningLogged() {
        var client = new Mock<ISimConnectClient>();
        client.Setup(c => c.IsConnectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        var provider = CreateProvider(client);

        var result = await provider.IsConnectedAsync(CancellationToken.None);

        Assert.False(result);
        Assert.DoesNotContain(_entries, e => e.Level == LogLevel.Warning || e.Level == LogLevel.Error);
        Assert.DoesNotContain(_entries, e => e.Message.Contains("SimConnect desconectado"));
    }

    [Fact]
    public async Task UnexpectedException_KeepsErrorLog() {
        var client = new Mock<ISimConnectClient>();
        client.Setup(c => c.IsConnectedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var provider = CreateProvider(client);

        var result = await provider.IsConnectedAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Contains(_entries, e => e.Level == LogLevel.Error && e.Message.Contains("Error during health check"));
    }
}
