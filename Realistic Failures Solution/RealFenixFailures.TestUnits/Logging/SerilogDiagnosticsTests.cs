using Microsoft.Extensions.Configuration;
using RealFenixFailures.Infrastructure.Logging;
using Serilog;
using Serilog.Events;

namespace RealFenixFailures.TestUnits.Logging;

public class SerilogDiagnosticsTests {
    private static readonly string DiagnosticsLogsDirectory =
        Path.Combine(Directory.GetCurrentDirectory(), "logs");

    [Fact]
    public void ApplyDefaultConfiguration_EnabledTrue_WritesDebugTokenToDiagnosticFileOnly() {
        var mainLogPath = Path.Combine(Path.GetTempPath(), $"serilog-tests-main-{Guid.NewGuid():N}.log");
        var token = $"diag-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: true, mainLogPath);

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext)
              .Debug("Enabled true diagnostic {Token}", token);
        logger.Dispose();

        Assert.Contains(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(token, StringComparison.Ordinal));
        AssertMainLogDoesNotContain(mainLogPath, token);
    }

    [Fact]
    public void ApplyDefaultConfiguration_EnabledFalse_DoesNotWriteTokenAndDebugDisabled() {
        var mainLogPath = Path.Combine(Path.GetTempPath(), $"serilog-tests-main-{Guid.NewGuid():N}.log");
        var token = $"diag-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: false, mainLogPath);

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        Assert.False(logger.IsEnabled(LogEventLevel.Debug));
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext)
              .Debug("Enabled false diagnostic {Token}", token);
        logger.Dispose();

        Assert.DoesNotContain(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(token, StringComparison.Ordinal));
        AssertMainLogDoesNotContain(mainLogPath, token);
    }

    private static IConfiguration BuildConfiguration(bool enabled, string mainLogPath) {
        var data = new Dictionary<string, string?> {
            ["SimConnectDiagnostics:Enabled"] = enabled ? "true" : "false",
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:WriteTo:0:Name"] = "File",
            ["Serilog:WriteTo:0:Args:path"] = mainLogPath
        };
        return new ConfigurationBuilder().Add(new InMemoryConfigurationSource(data)).Build();
    }

    private static IEnumerable<string> GetDiagnosticFiles() {
        if (!Directory.Exists(DiagnosticsLogsDirectory)) {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(DiagnosticsLogsDirectory, "simconnect-diagnostic-*.log");
    }

    private static void AssertMainLogDoesNotContain(string mainLogPath, string token) {
        if (File.Exists(mainLogPath)) {
            Assert.False(
                File.ReadAllText(mainLogPath).Contains(token, StringComparison.Ordinal),
                $"El log principal no debe contener el token: {mainLogPath}");
        }
    }

    private sealed class InMemoryConfigurationSource : IConfigurationSource {
        private readonly Dictionary<string, string?> _data;

        public InMemoryConfigurationSource(Dictionary<string, string?> data) => _data = data;

        public IConfigurationProvider Build(IConfigurationBuilder builder) => new InMemoryConfigurationProvider(_data);
    }

    private sealed class InMemoryConfigurationProvider : ConfigurationProvider {
        private readonly Dictionary<string, string?> _data;

        public InMemoryConfigurationProvider(Dictionary<string, string?> data) => _data = data;

        public override void Load() {
            Data.Clear();
            foreach (var pair in _data) {
                Data[pair.Key] = pair.Value;
            }
        }
    }
}
