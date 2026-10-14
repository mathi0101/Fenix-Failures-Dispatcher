using Microsoft.Extensions.DependencyInjection;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;

namespace RealFenixFailures.Application;

public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        services.AddScoped<IPresetService, PresetService>();
        services.AddScoped<IInitializerService, InitializerService>();
        services.AddScoped<IFlightSessionService, FlightSessionService>();
        services.AddScoped<IEngineOrchestrator, EngineOrchestrator>();
        services.AddSingleton<ISimulatorConnectionService, SimulatorConnectionService>();
        services.AddSingleton<IFlightEventDetector, FlightEventDetector>();
        services.AddSingleton<IRealisticSessionManager, RealisticSessionManager>();
        return services;
    }
}