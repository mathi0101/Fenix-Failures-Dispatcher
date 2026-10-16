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

    [Fact]
    public void ApplyDefaultConfiguration_OverrideLevels_FilterFrameworkInfoPreserveOwnDebugAndWarnings() {
        var mainLogPath = Path.Combine(Path.GetTempPath(), $"serilog-tests-main-{Guid.NewGuid():N}.log");
        const string debugDefault = "Debug";
        const string httpInfoToken = "http-info-token";
        const string httpWarnToken = "http-warn-token";
        const string efInfoToken = "ef-info-token";
        const string systemInfoToken = "system-info-token";
        const string microsoftInfoToken = "microsoft-info-token";
        const string ownInfoToken = "own-info-token";
        const string ownDebugToken = "own-debug-token";
        var configuration = BuildConfiguration(enabled: false, mainLogPath, defaultLevel: debugDefault,
            overrides: new Dictionary<string, string> {
                ["Microsoft"] = "Warning",
                ["System"] = "Warning",
                ["Microsoft.EntityFrameworkCore.Database.Command"] = "Warning"
            });

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", "System.Net.Http.HttpClient.IFenixApiClient.LogicalHandler").Information(httpInfoToken);
        logger.ForContext("SourceContext", "System.Net.Http.HttpClient.IFenixApiClient.LogicalHandler").Warning(httpWarnToken);
        logger.ForContext("SourceContext", "Microsoft.EntityFrameworkCore.Database.Command").Information(efInfoToken);
        logger.ForContext("SourceContext", "System.Private").Information(systemInfoToken);
        logger.ForContext("SourceContext", "Microsoft.Hosting").Information(microsoftInfoToken);
        logger.ForContext("SourceContext", "RealFenixFailures.App").Information(ownInfoToken);
        logger.ForContext("SourceContext", "RealFenixFailures.App").Debug(ownDebugToken);
        logger.Dispose();

        var content = ReadMainLog(mainLogPath, httpWarnToken, ownInfoToken, ownDebugToken);
        Assert.Contains(httpWarnToken, content, StringComparison.Ordinal);
        Assert.Contains(ownInfoToken, content, StringComparison.Ordinal);
        Assert.Contains(ownDebugToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(httpInfoToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(efInfoToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(systemInfoToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(microsoftInfoToken, content, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyDefaultConfiguration_MostSpecificEfCommandOverrideDebug_AllowsDebugCommands() {
        var mainLogPath = Path.Combine(Path.GetTempPath(), $"serilog-tests-main-{Guid.NewGuid():N}.log");
        const string efCommandDebugToken = "ef-command-debug-token";
        const string microsoftDebugToken = "microsoft-debug-token";
        var configuration = BuildConfiguration(enabled: false, mainLogPath, defaultLevel: "Debug",
            overrides: new Dictionary<string, string> {
                ["Microsoft"] = "Warning",
                ["Microsoft.EntityFrameworkCore.Database.Command"] = "Debug",
                ["System.Net.Http.HttpClient"] = "Warning"
            });

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", "Microsoft.EntityFrameworkCore.Database.Command").Debug(efCommandDebugToken);
        logger.ForContext("SourceContext", "Microsoft.Hosting.Lifetime").Debug(microsoftDebugToken);
        logger.Dispose();

        var content = ReadMainLog(mainLogPath, efCommandDebugToken);
        Assert.Contains(efCommandDebugToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(microsoftDebugToken, content, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyDefaultConfiguration_MainSinkExcludesDiagnosticsWhenEnabled() {
        var mainLogPath = Path.Combine(Path.GetTempPath(), $"serilog-tests-main-{Guid.NewGuid():N}.log");
        var diagToken = $"diag-excluded-token-{Guid.NewGuid():N}";
        var ownToken = $"own-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: true, mainLogPath, defaultLevel: "Debug");

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext).Debug(diagToken);
        logger.ForContext("SourceContext", "RealFenixFailures.App").Debug(ownToken);
        logger.Dispose();

        var content = ReadMainLog(mainLogPath, ownToken);
        Assert.Contains(ownToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(diagToken, content, StringComparison.Ordinal);
        Assert.Contains(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(diagToken, StringComparison.Ordinal));
    }

    private static IConfiguration BuildConfiguration(
        bool enabled,
        string mainLogPath,
        string defaultLevel = "Warning",
        Dictionary<string, string>? overrides = null) {
        var data = new Dictionary<string, string?> {
            ["SimConnectDiagnostics:Enabled"] = enabled ? "true" : "false",
            ["Serilog:MinimumLevel:Default"] = defaultLevel,
            ["Serilog:WriteTo:0:Name"] = "File",
            ["Serilog:WriteTo:0:Args:path"] = mainLogPath
        };
        if (overrides != null) {
            foreach (var pair in overrides) {
                data[$"Serilog:MinimumLevel:Override:{pair.Key}"] = pair.Value;
            }
        }
        return new ConfigurationBuilder().Add(new InMemoryConfigurationSource(data)).Build();
    }

    private static string ReadMainLog(string mainLogPath, params string[] requiredTokens) {
        var content = "";
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline) {
            if (File.Exists(mainLogPath)) {
                content = File.ReadAllText(mainLogPath);
                if (requiredTokens.All(token => content.Contains(token, StringComparison.Ordinal))) {
                    return content;
                }
            }
            Thread.Sleep(100);
        }
        return content;
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
