using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;
using RealFenixFailures.Infrastructure.Services;

namespace RealFenixFailures.Infrastructure;

public static class DependencyInjection {
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) {
        var connectionString = configuration.GetConnectionString("Sqlite") ?? "Data Source=realfenixfailures.db";

        services.AddDbContext<RealFenixDbContext>((sp, options) => {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            options.UseSqlite(connectionString)
                .UseLoggerFactory(loggerFactory)
                .EnableDetailedErrors()
                .ConfigureWarnings(w => w.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)));
#if DEBUG
            options.EnableSensitiveDataLogging();
#endif
        });
        services.Configure<FailureEngineSettings>(configuration.GetSection(FailureEngineSettings.SectionName));

        services.AddScoped<IFenixFailureDefinitionRepository, FenixFailureDefinitionRepository>();
        services.AddScoped<IPresetRepository, PresetRepository>();
        services.AddScoped<IFlightSessionRepository, FlightSessionRepository>();
        services.AddScoped<ITriggeredFailureRepository, TriggeredFailureRepository>();
        services.AddScoped<IFlightPlanRepository, FlightPlanRepository>();

        services.AddScoped<IFailuresPersistenceService, FailuresPersistenceService>();
        services.AddScoped<IPresetsLoader, PresetsLoader>();
        services.AddScoped<IUserAircraftService, UserAircraftService>();

        return services;
    }
}
