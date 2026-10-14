using Microsoft.Extensions.DependencyInjection;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Services;

namespace RealFenixFailures.Integrations.SimConnect;

public static class DependencyInjection {
    public static IServiceCollection AddSimConnectIntegration(this IServiceCollection services) {
#if DEBUG
        services.AddSingleton<MockSimConnectClient>();
        services.AddSingleton<ISimConnectClient>(provider => provider.GetRequiredService<MockSimConnectClient>());
        services.AddSingleton<IFlightSimulationController>(provider => provider.GetRequiredService<MockSimConnectClient>());
#else
        services.AddSingleton<SimConnectClient>();
        services.AddSingleton<ISimConnectClient>(provider => provider.GetRequiredService<SimConnectClient>());
        services.AddSingleton<IFlightSimulationController>(provider => provider.GetRequiredService<SimConnectClient>());
#endif
        services.AddSingleton<ISimFlightDataProvider, SimulatorFlightDataProvider>();
        return services;
    }
}
