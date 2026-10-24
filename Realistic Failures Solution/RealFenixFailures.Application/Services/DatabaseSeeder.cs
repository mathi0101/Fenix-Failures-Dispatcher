using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Services;

// Punto único de orquestación de los datos semilla del arranque: carga el
// catálogo de fallas (IFailuresPersistenceService) y los presets
// (IPresetService.InitializeAsync) reutilizando los servicios existentes;
// todo dato semilla futuro se orquesta desde acá. Los seeds estáticos del
// esquema (PresetType, AircraftWearableSystem) siguen en SeedData.Apply
// (OnModelCreating). Implementa IInitializerService: App.xaml.cs no cambia.
public class DatabaseSeeder : IInitializerService {
    private readonly IFailuresPersistenceService _failuresPersistenceService;
    private readonly IPresetService _presetService;

    public DatabaseSeeder(IFailuresPersistenceService failuresPersistenceService, IPresetService presetsLoader) {
        _failuresPersistenceService = failuresPersistenceService;
        _presetService = presetsLoader;
    }

    public async Task InitializeAsync(CancellationToken ct) {
        await _failuresPersistenceService.LoadInitialFailuresAsync(ct);
        await _presetService.InitializeAsync(ct);
    }
}
