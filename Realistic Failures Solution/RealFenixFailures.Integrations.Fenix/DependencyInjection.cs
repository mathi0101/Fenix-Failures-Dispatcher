using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Integrations.Fenix.Interfaces;
using RealFenixFailures.Integrations.Fenix.Models;
using RealFenixFailures.Integrations.Fenix.Services;

namespace RealFenixFailures.Integrations.Fenix;

public static class DependencyInjection {
    public static IServiceCollection AddFenixIntegration(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<FenixApiOptions>(configuration.GetSection(FenixApiOptions.SectionName));
        services.AddSingleton<IFenixStreamFailuresReaderService, FenixStreamFailuresReaderService>();

        // Mock seleccionable por config (FenixApi:UseMock) para probar el motor sin Fenix real corriendo.
        // Solo aplica en builds DEBUG: en Release siempre va contra la API real.
#if DEBUG
        if (bool.TryParse(configuration["FenixApi:UseMock"], out var useMock) && useMock) {
            services.AddSingleton<IFenixApiClient, MockFenixApiClient>();
            services.AddSingleton<IFenixFailureApiDispatcher, FenixApiFailureService>();
            return services;
        }
#endif

        services.AddHttpClient<IFenixApiClient, FenixApiClient>();
        services.AddSingleton<IFenixFailureApiDispatcher, FenixApiFailureService>();
        return services;
    }
}
