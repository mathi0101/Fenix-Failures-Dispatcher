namespace RealFenixFailures.Application.Interfaces;

// Seed temporal SOLO de DEBUG: siembra una aeronave de ejemplo con sesiones,
// fallas y desgaste acumulado para no cargar datos a mano al regenerar la base
// durante el desarrollo. Implementa IInitializerService y se registra solo bajo
// #if DEBUG en DI; debe correr DESPUÉS del catálogo de fallas porque sus
// TriggeredFailure referencian FenixFailureId del catálogo (FK).
public interface IDebugSampleDataSeeder : IInitializerService {
    Task SeedIfEmptyAsync(CancellationToken ct);
}
