using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain;
using RealFenixFailures.Infrastructure;
using RealFenixFailures.Infrastructure.Logging;
using RealFenixFailures.Integrations.Fenix;
using RealFenixFailures.Integrations.SimBrief;
using RealFenixFailures.Integrations.SimConnect;
using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels;
using RealFenixFailures.UI.ViewModels.Realistic;
using RealFenixFailures.UI.Views;
using Serilog;
using RealFenixFailures.UI.Diagnostics;

namespace RealFenixFailures.UI.DependencyInjection;

public static class HostBuilderExtensions {
    public static IHostBuilder ConfigureRealFenixFailures(this IHostBuilder hostBuilder) {
        return hostBuilder
            .UseSerilog((context, services, loggerConfiguration) => {
                loggerConfiguration.ApplyDefaultConfiguration(context.Configuration);
                loggerConfiguration.Enrich.WithProperty("AppVersion", AppVersionInfo.Current);
                loggerConfiguration.WriteTo.Sink(new UnexpectedErrorSink(CrashReporter.ReportUnexpected));
            })
            .ConfigureServices((context, services) => {
                services.AddDomain();
                services.AddApplication();
                services.AddInfrastructure(context.Configuration);
                services.AddSimConnectIntegration();
                services.AddFenixIntegration(context.Configuration);
                services.AddSimbriefIntegration(context.Configuration);
                services.AddSingleton<UserPreferencesService>();
                services.Configure<TelemetrySettings>(context.Configuration.GetSection("Telemetry"));
                services.AddSingleton<TelemetrySettings>(sp => sp.GetRequiredService<IOptions<TelemetrySettings>>().Value);
                services.AddSingleton<TelemetryService>();
                services.AddSingleton<IUpdateChecker, GithubUpdateChecker>();
                services.AddSingleton<IRealisticExpectationService, RealisticExpectationService>();
                services.AddTransient<SettingsWindowViewModel>();
                services.AddTransient<MainWindow>();
                services.AddTransient<MainWindowViewModel>();
                services.AddScoped<RealisticModeViewModel>();
            });
    }
}
