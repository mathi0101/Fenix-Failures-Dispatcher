using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application;
using RealFenixFailures.Domain;
using RealFenixFailures.Infrastructure;
using RealFenixFailures.Infrastructure.Logging;
using RealFenixFailures.Integrations.Fenix;
using RealFenixFailures.Integrations.SimConnect;
using RealFenixFailures.UI.ViewModels;
using RealFenixFailures.UI.ViewModels.Realistic;
using RealFenixFailures.UI.Views;
using Serilog;

namespace RealFenixFailures.UI.DependencyInjection;

public static class HostBuilderExtensions {
    public static IHostBuilder ConfigureRealFenixFailures(this IHostBuilder hostBuilder) {
        return hostBuilder
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace))
            .UseSerilog((context, services, loggerConfiguration) => {
                loggerConfiguration.ApplyDefaultConfiguration(context.Configuration);
            })
            .ConfigureServices((context, services) => {
                services.AddDomain();
                services.AddApplication();
                services.AddInfrastructure(context.Configuration);
                services.AddSimConnectIntegration();
                services.AddFenixIntegration(context.Configuration);
                services.AddTransient<MainWindow>();
                services.AddTransient<MainWindowViewModel>();
                services.AddScoped<RealisticModeViewModel>();
            });
    }
}