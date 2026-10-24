using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Integrations.SimBrief.Interfaces;
using RealFenixFailures.Integrations.SimBrief.Models;
using RealFenixFailures.Integrations.SimBrief.Services;

namespace RealFenixFailures.Integrations.SimBrief;

public static class DependencyInjection {
    public static IServiceCollection AddSimbriefIntegration(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<SimBriefApiOptions>(configuration.GetSection(SimBriefApiOptions.SectionName));

        // IOfpFetcher: contrato de la capa de aplicación, implementado con el cliente HTTP de SimBrief.
        services.AddScoped<IOfpFetcher, SimBriefOfpFetcher>();

#if DEBUG && false // Incluso en debug podemos usar la api real sin problemas
        services.AddSingleton<ISimBriefApiClient, MockSimBriefApiClient>();
#else
        services.AddHttpClient<ISimBriefApiClient, SimBriefApiClient>();
#endif

        services.AddSingleton<ISimBriefOfpReader, SimBriefOfpReader>();
        return services;
    }
}
