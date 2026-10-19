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

#if DEBUG && false
        services.AddSingleton<IFenixApiClient, MockFenixApiClient>();
#else
        services.AddHttpClient<IFenixApiClient, FenixApiClient>();
#endif

        services.AddSingleton<IFenixFailureApiDispatcher, FenixApiFailureService>();
        return services;
    }
}
