using Microsoft.Extensions.Configuration;
using RealFenixFailures.Infrastructure;
using RealFenixFailures.Infrastructure.Logging;
using Serilog;
using Serilog.Events;

namespace RealFenixFailures.TestUnits.Logging;

public class SerilogDiagnosticsTests {
    [Fact]
    public void ApplyDefaultConfiguration_EnabledTrue_WritesDebugTokenToDiagnosticFileOnly() {
        var token = $"diag-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: true);

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext)
              .Debug("Enabled true diagnostic {Token}", token);
        logger.Dispose();

        Assert.Contains(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(token, StringComparison.Ordinal));
        AssertMainLogDoesNotContain(token);
    }

    [Fact]
    public void ApplyDefaultConfiguration_EnabledFalse_DoesNotWriteTokenAndDebugDisabled() {
        var token = $"diag-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: false);

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        Assert.False(logger.IsEnabled(LogEventLevel.Debug));
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext)
              .Debug("Enabled false diagnostic {Token}", token);
        logger.Dispose();

        Assert.DoesNotContain(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(token, StringComparison.Ordinal));
        AssertMainLogDoesNotContain(token);
    }

    [Fact]
    public void ApplyDefaultConfiguration_OverrideLevels_FilterFrameworkInfoPreserveOwnDebugAndWarnings() {
        const string debugDefault = "Debug";
        var httpInfoToken = $"http-info-{Guid.NewGuid():N}";
        var httpWarnToken = $"http-warn-{Guid.NewGuid():N}";
        var efInfoToken = $"ef-info-{Guid.NewGuid():N}";
        var systemInfoToken = $"system-info-{Guid.NewGuid():N}";
        var microsoftInfoToken = $"microsoft-info-{Guid.NewGuid():N}";
        var ownInfoToken = $"own-info-{Guid.NewGuid():N}";
        var ownDebugToken = $"own-debug-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: false, defaultLevel: debugDefault,
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

        var content = ReadMainLog(httpWarnToken, ownInfoToken, ownDebugToken);
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
        var efCommandDebugToken = $"ef-command-debug-{Guid.NewGuid():N}";
        var microsoftDebugToken = $"microsoft-debug-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: false, defaultLevel: "Debug",
            overrides: new Dictionary<string, string> {
                ["Microsoft"] = "Warning",
                ["Microsoft.EntityFrameworkCore.Database.Command"] = "Debug",
                ["System.Net.Http.HttpClient"] = "Warning"
            });

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", "Microsoft.EntityFrameworkCore.Database.Command").Debug(efCommandDebugToken);
        logger.ForContext("SourceContext", "Microsoft.Hosting.Lifetime").Debug(microsoftDebugToken);
        logger.Dispose();

        var content = ReadMainLog(efCommandDebugToken);
        Assert.Contains(efCommandDebugToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(microsoftDebugToken, content, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyDefaultConfiguration_MainSinkExcludesDiagnosticsWhenEnabled() {
        var diagToken = $"diag-excluded-token-{Guid.NewGuid():N}";
        var ownToken = $"own-token-{Guid.NewGuid():N}";
        var configuration = BuildConfiguration(enabled: true, defaultLevel: "Debug");

        var logger = new LoggerConfiguration().ApplyDefaultConfiguration(configuration).CreateLogger();
        logger.ForContext("SourceContext", SerilogExtensions.SimConnectDiagnosticsSourceContext).Debug(diagToken);
        logger.ForContext("SourceContext", "RealFenixFailures.App").Debug(ownToken);
        logger.Dispose();

        var content = ReadMainLog(ownToken);
        Assert.Contains(ownToken, content, StringComparison.Ordinal);
        Assert.DoesNotContain(diagToken, content, StringComparison.Ordinal);
        Assert.Contains(
            GetDiagnosticFiles(),
            file => File.ReadAllText(file).Contains(diagToken, StringComparison.Ordinal));
    }

    private static IConfiguration BuildConfiguration(
        bool enabled,
        string defaultLevel = "Warning",
        Dictionary<string, string>? overrides = null) {
        var data = new Dictionary<string, string?> {
            ["SimConnectDiagnostics:Enabled"] = enabled ? "true" : "false",
            ["Serilog:MinimumLevel:Default"] = defaultLevel
        };
        if (overrides != null) {
            foreach (var pair in overrides) {
                data[$"Serilog:MinimumLevel:Override:{pair.Key}"] = pair.Value;
            }
        }
        return new ConfigurationBuilder().Add(new InMemoryConfigurationSource(data)).Build();
    }

    private static IEnumerable<string> GetMainLogFiles() {
        if (!Directory.Exists(AppPaths.LogsDirectory)) {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(AppPaths.LogsDirectory, "realfenix-*.log");
    }

    private static string ReadMainLog(params string[] requiredTokens) {
        var content = "";
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline) {
            content = string.Concat(GetMainLogFiles()
                .Where(File.Exists)
                .Select(file => {
                    try { return File.ReadAllText(file); }
                    catch (IOException) { return string.Empty; }
                }));
            if (requiredTokens.All(token => content.Contains(token, StringComparison.Ordinal))) {
                return content;
            }
            Thread.Sleep(100);
        }
        return content;
    }

    private static IEnumerable<string> GetDiagnosticFiles() {
        if (!Directory.Exists(AppPaths.LogsDirectory)) {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(AppPaths.LogsDirectory, "simconnect-diagnostic-*.log");
    }

    private static void AssertMainLogDoesNotContain(string token) {
        foreach (var file in GetMainLogFiles()) {
            Assert.False(
                File.ReadAllText(file).Contains(token, StringComparison.Ordinal),
                $"El log principal no debe contener el token: {file}");
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
