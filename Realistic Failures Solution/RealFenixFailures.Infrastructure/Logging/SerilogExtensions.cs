using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace RealFenixFailures.Infrastructure.Logging;

public static class SerilogExtensions {
    /// <summary>
    /// Categoría de logger que deben usar los emisores de diagnóstico de SimConnect
    /// (datos crudos recibidos y estados de vuelo calculados) para ser ruteados al
    /// archivo de diagnóstico en lugar del log principal.
    /// </summary>
    public const string SimConnectDiagnosticsSourceContext = "SimConnect.Diagnostics";

    public static LoggerConfiguration ApplyDefaultConfiguration(this LoggerConfiguration loggerConfiguration, IConfiguration configuration) {
        var serilogSection = configuration.GetSection("Serilog");

        // Los logs viven junto al resto de los datos del usuario (%AppData%\RealFenixFailures\logs),
        // no al lado del ejecutable: la carpeta del exe puede ser de solo lectura (Program Files)
        // o estar sincronizada (OneDrive) y no debe ensuciarse con archivos rodantes.
        AppPaths.EnsureCreated();

        ApplyMinimumLevel(loggerConfiguration, serilogSection.GetSection("MinimumLevel"));

        loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.With(new UtcTimestampEnricher())
            .WriteTo.Logger(mainLogger => mainLogger
                .Filter.ByExcluding(Matching.FromSource(SimConnectDiagnosticsSourceContext))
                .WriteTo.File(
                    Path.Combine(AppPaths.LogsDirectory, "realfenix-.log"),
                    rollingInterval: Enum.TryParse<RollingInterval>(serilogSection["WriteTo:0:Args:rollingInterval"], ignoreCase: true, out var rollingInterval) ? rollingInterval : RollingInterval.Day,
                    outputTemplate: serilogSection["WriteTo:0:Args:outputTemplate"] ?? "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] <s:{SourceContext}> {Message:lj} {NewLine}{Exception}"));

        if (configuration.GetValue<bool>("SimConnectDiagnostics:Enabled")) {
            loggerConfiguration.MinimumLevel.Override(SimConnectDiagnosticsSourceContext, LogEventLevel.Debug);
            loggerConfiguration.WriteTo.Logger(diagnosticLogger => diagnosticLogger
                .Filter.ByIncludingOnly(Matching.FromSource(SimConnectDiagnosticsSourceContext))
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(AppPaths.LogsDirectory, "simconnect-diagnostic-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
        }

        return loggerConfiguration;
    }

    private static void ApplyMinimumLevel(LoggerConfiguration loggerConfiguration, IConfigurationSection minimumLevelSection) {
        if (Enum.TryParse<LogEventLevel>(minimumLevelSection["Default"], ignoreCase: true, out var defaultLevel)) {
            loggerConfiguration.MinimumLevel.Is(defaultLevel);
        }

        foreach (var overrideSection in minimumLevelSection.GetSection("Override").GetChildren()) {
            if (Enum.TryParse<LogEventLevel>(overrideSection.Value, ignoreCase: true, out var overrideLevel)) {
                loggerConfiguration.MinimumLevel.Override(overrideSection.Key, overrideLevel);
            }
        }
    }
}
