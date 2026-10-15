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
        loggerConfiguration
            .Enrich.FromLogContext()
            .WriteTo.Logger(mainLogger => mainLogger
                .Filter.ByExcluding(Matching.FromSource(SimConnectDiagnosticsSourceContext))
                .ReadFrom.Configuration(configuration));

        if (configuration.GetValue<bool>("SimConnectDiagnostics:Enabled")) {
            loggerConfiguration.MinimumLevel.Override(SimConnectDiagnosticsSourceContext, LogEventLevel.Debug);
            loggerConfiguration.WriteTo.Logger(diagnosticLogger => diagnosticLogger
                .Filter.ByIncludingOnly(Matching.FromSource(SimConnectDiagnosticsSourceContext))
                .MinimumLevel.Debug()
                .WriteTo.File(
                    "logs/simconnect-diagnostic-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
        }

        return loggerConfiguration;
    }
}
