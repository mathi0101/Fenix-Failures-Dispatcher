using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Services;

// Punto único de orquestación de los datos semilla del arranque. Desde el sincronizador de
// catálogos por hash, la siembra y el refresco del contenido curado (failures.json + presets de
// modos) es responsabilidad de ICatalogSyncService: compara el hash de los JSON embebidos con el
// último aplicado (CatalogSyncState) y reconcilia si difiere — BDs nuevas siembran de cero, BDs
// existentes con catálogos curados nuevos se autocorrigen al arrancar. Los seeds estáticos del
// esquema (PresetType, AircraftWearableSystem) siguen en SeedData.Apply (OnModelCreating) y el
// esquema mismo, en migraciones EF. Implementa IInitializerService: App.xaml.cs no cambia.
public class DatabaseSeeder : IInitializerService {
    private readonly ICatalogSyncService _catalogSyncService;

    public DatabaseSeeder(ICatalogSyncService catalogSyncService) {
        _catalogSyncService = catalogSyncService;
    }

    public async Task InitializeAsync(CancellationToken ct) {
        await _catalogSyncService.SyncAsync(ct);
    }
}
