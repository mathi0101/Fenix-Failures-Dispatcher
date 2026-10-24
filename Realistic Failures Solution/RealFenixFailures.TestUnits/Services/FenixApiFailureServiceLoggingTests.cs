using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Integrations.Fenix.Interfaces;
using RealFenixFailures.Integrations.Fenix.Models;
using RealFenixFailures.Integrations.Fenix.Services;
using RealFenixFailures.TestUnits.Logging;

namespace RealFenixFailures.TestUnits.Services;

public class FenixApiFailureServiceLoggingTests {
    private readonly List<LogEntry> _entries = new();

    private FenixApiFailureService CreateService(Mock<IFenixApiClient> apiClient, List<LogEntry>? entries = null) {
        var targetEntries = entries ?? _entries;
        var options = new FenixApiOptions {
            BaseUrl = "http://localhost",
            Port = 8083,
            ManualFailuresPath = "/fenix/failures/manual",
            SaveManualPath = "/fenix/failures/saveManual",
            HealthCheckIntervalSeconds = 10,
            HealthCheckTimeout = 2
        };
        var monitor = new Mock<IOptionsMonitor<FenixApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new FenixApiFailureService(
            apiClient.Object,
            new Mock<IFenixStreamFailuresReaderService>().Object,
            monitor.Object,
            new RecordingLogger<FenixApiFailureService>(targetEntries));
    }

    private static FenixArmFailureRequest ArmRequest() => new("F_ENGINE_1", false, null);

    private static List<LogEntry> TransitionLogs(List<LogEntry> entries) =>
        entries.Where(e => e.Level == LogLevel.Information && e.Message.StartsWith("API Fenix")).ToList();

    private static Mock<IFenixApiClient> ApiWithSendFailureSequence(IEnumerable<bool> results) {
        var apiClient = new Mock<IFenixApiClient>();
        var sequence = apiClient.SetupSequence(c => c.SendFailureAsync(It.IsAny<FenixSaveManualRequest>(), It.IsAny<CancellationToken>()));
        foreach (var success in results) {
            sequence.ReturnsAsync(success ? (Stream?)new MemoryStream() : (Stream?)null);
        }
        return apiClient;
    }

    [Fact]
    public async Task InitialFailure_DoesNotLogDisconnection() {
        var apiClient = ApiWithSendFailureSequence(new[] { false });
        var service = CreateService(apiClient);

        var result = await service.ArmFailureAsync(ArmRequest(), CancellationToken.None);

        Assert.False(result);
        Assert.Empty(TransitionLogs(_entries));
    }

    [Fact]
    public async Task FailedHealthProbe_ReturnsDisconnected_AndCachesDiagnosticWithoutError() {
        var apiClient = new Mock<IFenixApiClient>();
        apiClient.Setup(c => c.IsApiAlive(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Probe failed"));
        var service = CreateService(apiClient);

        Assert.False(await service.IsApiAvailableAsync(CancellationToken.None));
        Assert.False(await service.IsApiAvailableAsync(CancellationToken.None));

        var entry = Assert.Single(_entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("health check", entry.Message);
        apiClient.Verify(c => c.IsApiAlive(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitialGetAllFailure_DoesNotLogDisconnection() {
        var apiClient = new Mock<IFenixApiClient>();
        apiClient.Setup(c => c.GetManualFailuresAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);
        var service = CreateService(apiClient);

        var result = await service.GetAllFailuresAsync(CancellationToken.None);

        Assert.Empty(TransitionLogs(_entries));
        Assert.Null(result);
    }

    [Fact]
    public async Task StateChanges_LogSingleShortInfoPerTransition() {
        var apiClient = ApiWithSendFailureSequence(new[] { true, true, false, false, true });
        var service = CreateService(apiClient);

        await service.ArmFailureAsync(ArmRequest(), CancellationToken.None);
        await service.ArmFailureAsync(ArmRequest(), CancellationToken.None);
        await service.DisarmFailureAsync(ArmRequest(), CancellationToken.None);
        await service.DisarmFailureAsync(ArmRequest(), CancellationToken.None);
        await service.ArmFailureAsync(ArmRequest(), CancellationToken.None);

        var transitions = TransitionLogs(_entries);
        Assert.Equal(
            new[] { "API Fenix conectada", "API Fenix desconectada", "API Fenix conectada" },
            transitions.Select(e => e.Message));
        Assert.All(transitions, e => Assert.True(e.Level == LogLevel.Information && e.Message.Length < 40));
    }

    [Fact]
    public async Task ConcurrentUpdates_LogSingleLinePerStateChange() {
        var apiClient = ApiWithSendFailureSequence(Enumerable.Repeat(true, 8));
        var service = CreateService(apiClient);

        var armTasks = Enumerable.Range(0, 8)
            .Select(_ => service.ArmFailureAsync(ArmRequest(), CancellationToken.None))
            .ToArray();
        await Task.WhenAll(armTasks);

        Assert.Equal(new[] { "API Fenix conectada" }, TransitionLogs(_entries).Select(e => e.Message));

        var disconnectEntries = new List<LogEntry>();
        var disconnectedClient = ApiWithSendFailureSequence(new[] { true }.Concat(Enumerable.Repeat(false, 8)));
        var service2 = CreateService(disconnectedClient, disconnectEntries);

        await service2.ArmFailureAsync(ArmRequest(), CancellationToken.None);
        var disarmTasks = Enumerable.Range(0, 8)
            .Select(_ => service2.DisarmFailureAsync(ArmRequest(), CancellationToken.None))
            .ToArray();
        await Task.WhenAll(disarmTasks);

        Assert.Equal(
            new[] { "API Fenix conectada", "API Fenix desconectada" },
            TransitionLogs(disconnectEntries).Select(e => e.Message));
    }
}
