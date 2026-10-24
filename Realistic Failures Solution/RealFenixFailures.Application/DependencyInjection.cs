using Microsoft.Extensions.DependencyInjection;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application;

public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        // Reloj de simulación compartido: un único ScaledTimeProvider alimenta los mocks (SimConnect/Fenix)
        // y los loops del motor. Con escala 1 el comportamiento es idéntico al tiempo real.
        // Factory explícita (sin ctor por DI): el ctor opcional de ScaledTimeProvider toma un TimeProvider y
        // con resolución automática ciclaría contra el registro de TimeProvider de abajo (recursión infinita).
        services.AddSingleton<ScaledTimeProvider>(static _ => new ScaledTimeProvider());
        services.AddSingleton<TimeProvider>(static provider => provider.GetRequiredService<ScaledTimeProvider>());

        services.AddScoped<IPresetService, PresetService>();
        services.AddScoped<IInitializerService, DatabaseSeeder>();
        services.AddScoped<IFlightSessionService, FlightSessionService>();
        services.AddScoped<ISimbriefOfpService, SimbriefOfpService>();
        services.AddScoped<IEngineOrchestrator, EngineOrchestrator>();
        services.AddSingleton<ISimulatorConnectionService, SimulatorConnectionService>();
        services.AddSingleton<IFlightEventDetector, FlightEventDetector>();
        services.AddSingleton<IRealisticSessionManager, RealisticSessionManager>();
        return services;
    }
}