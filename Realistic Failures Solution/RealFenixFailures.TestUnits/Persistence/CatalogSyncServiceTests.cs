using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;
using RealFenixFailures.Infrastructure.Services;
using RealFenixFailures.Integrations.Fenix.Services;

namespace RealFenixFailures.TestUnits.Persistence;

/// <summary>
/// Sincronizador de catálogos por hash: siembra inicial, no-op idempotente y reconciliación de
/// drift (update/soft-delete/reactivación de fallas, reemplazo de presets de modos, Custom intactos).
/// Ningún esperado es un literal: los counts y los valores curados se derivan en runtime de los
/// mismos JSON embebidos que lee el motor (misma fuente del código), según la política de
/// aserciones de docs/desarrollo.md — curar un catálogo actualiza los esperados sin tocar el test.
/// </summary>
public class CatalogSyncServiceTests {
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private static readonly FenixStreamFailuresReaderService Reader = new(NullLogger<FenixStreamFailuresReaderService>.Instance);
    private static readonly PresetsLoader Loader = new();

    private CatalogSyncService CreateService(RealFenixDbContext context) {
        return new CatalogSyncService(
            context,
            Reader,
            Loader,
            NullLogger<CatalogSyncService>.Instance);
    }

    private RealFenixDbContext CreateContext() {
        _connection.Open();
        var options = new DbContextOptionsBuilder<RealFenixDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new RealFenixDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<CatalogSyncState> GetStateAsync(RealFenixDbContext context) {
        return await context.CatalogSyncStates.SingleAsync(s => s.Id == 1);
    }

    /// <summary>El catálogo de fallas embebido, leído con el mismo parser que usa el sync/código productivo.</summary>
    private static AllFenixFailuresResponseDto ReadEmbeddedFailures() {
        using var stream = EmbeddedJsonLoader.LoadFromEmbeddedJson(EmbeddedResourceNames.FailuresCatalog)
            ?? throw new InvalidOperationException("No se pudo leer failures.json embebido");
        return Reader.ReadAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Presets embebidos de los dos modos, leídos con el mismo loader del sync.</summary>
    private static async Task<(IReadOnlyList<FailurePreset> Training, IReadOnlyList<FailurePreset> Realistic)> ReadEmbeddedPresetsAsync() {
        return (
            await Loader.GetTrainingPresetsAsync(CancellationToken.None),
            await Loader.GetRealisticPresetsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FirstRun_SeedsFullEmbeddedCatalog_AllActive_AndStampsHash() {
        using var context = CreateContext();
        var json = ReadEmbeddedFailures();
        var jsonFailures = json.GetFailuresList();
        var (training, realistic) = await ReadEmbeddedPresetsAsync();
        var service = CreateService(context);

        await service.SyncAsync(CancellationToken.None);

        var definitions = await context.FenixFailureDefinitions.ToListAsync();
        Assert.Equal(jsonFailures.Count, definitions.Count);
        Assert.All(definitions, d => Assert.True(d.IsActive));
        Assert.Equal(json.MajorGroups.Count, await context.FenixFailureSystems.CountAsync());

        // Distribución curada (severidad y repairable) idéntica a la del catálogo embebido.
        var dbBySeverity = definitions.GroupBy(d => d.Severity).ToDictionary(g => g.Key, g => g.Count());
        foreach (var group in jsonFailures.GroupBy(f => f.Severity))
            Assert.Equal(group.Count(), dbBySeverity[group.Key]);
        Assert.Equal(jsonFailures.Count(f => f.Repairable), definitions.Count(d => d.CanBeRepaired));

        var dbTraining = await context.FailurePresets.Where(p => p.PresetTypeId == (int)PresetTypeEnum.TrainingMode).ToListAsync();
        var dbRealistic = await context.FailurePresets.Where(p => p.PresetTypeId == (int)PresetTypeEnum.RealisticMode).ToListAsync();
        Assert.Equal(training.Count, dbTraining.Count);
        Assert.Equal(realistic.Count, dbRealistic.Count);

        var state = await GetStateAsync(context);
        Assert.Equal(64, state.CatalogsHash.Length);
    }

    [Fact]
    public async Task SecondRun_WithSameCatalogs_IsNoOp() {
        using var context = CreateContext();
        var service = CreateService(context);

        await service.SyncAsync(CancellationToken.None);
        var stateAfterFirst = await GetStateAsync(context);
        var definitionIds = await context.FenixFailureDefinitions.Select(d => d.FenixFailureId).OrderBy(x => x).ToListAsync();
        var trainingPresetIds = await context.FailurePresets
            .Where(p => p.PresetTypeId == (int)PresetTypeEnum.TrainingMode)
            .Select(p => p.Id).OrderBy(x => x).ToListAsync();

        await service.SyncAsync(CancellationToken.None);

        Assert.Equal(stateAfterFirst.CatalogsHash, (await GetStateAsync(context)).CatalogsHash);
        Assert.Equal(definitionIds, await context.FenixFailureDefinitions.Select(d => d.FenixFailureId).OrderBy(x => x).ToListAsync());
        // Sin reemplazo de presets: los Ids persistidos no cambian (no hubo DELETE + reinsert).
        Assert.Equal(trainingPresetIds, await context.FailurePresets
            .Where(p => p.PresetTypeId == (int)PresetTypeEnum.TrainingMode)
            .Select(p => p.Id).OrderBy(x => x).ToListAsync());
    }

    [Fact]
    public async Task Drift_OnHashMismatch_IsReconciledWithUpsertSoftDeleteAndReactivation() {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.SyncAsync(CancellationToken.None);
        var baselineCount = await context.FenixFailureDefinitions.CountAsync();

        // Muestra curada tomada del propio catálogo embebido (sin IDs ni valores horneados en el test).
        var jsonFailures = ReadEmbeddedFailures().GetFailuresList();
        var sample = jsonFailures.Skip(3).First();
        var jsonIds = jsonFailures.Select(f => f.FenixId).ToHashSet();

        // Drift simulado contra el catálogo embebido (el hash queda "vencido"):
        var drifted = await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == sample.FenixId);
        drifted.Name = "Nombre editado";
        drifted.Severity = FailureSeverity.Critical;
        drifted.CanBeRepaired = !sample.Repairable;

        var deactivatedId = jsonIds.First(id => id != sample.FenixId);
        var deactivated = await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == deactivatedId);
        deactivated.IsActive = false;

        var foreignGroup = drifted.Group;
        context.FenixFailureDefinitions.Add(new FenixFailureDefinition {
            FenixFailureId = "F_FAKE_NOT_IN_JSON", Name = "Falla fantasma", Severity = FailureSeverity.Minor, Group = foreignGroup
        });

        // Una falla del JSON eliminada físicamente de la BD (sin presets referenciándola): debe reinsertarse.
        var referencedByPresets = await context.PresetFailureDefinitions.Select(p => p.FenixFailureId).Distinct().ToListAsync();
        var orphan = await context.FenixFailureDefinitions
            .Where(d => !referencedByPresets.Contains(d.FenixFailureId)
                        && d.FenixFailureId != sample.FenixId && d.FenixFailureId != deactivatedId)
            .FirstAsync();
        var orphanId = orphan.FenixFailureId;
        context.FenixFailureDefinitions.Remove(orphan);
        await context.SaveChangesAsync();

        (await GetStateAsync(context)).CatalogsHash = "stale";
        await context.SaveChangesAsync();

        await service.SyncAsync(CancellationToken.None);

        // El drift queda corregido contra el catálogo embebido, campo a campo.
        var afterSync = await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == sample.FenixId);
        Assert.Equal(sample.Description, afterSync.Name);
        Assert.Equal(sample.Severity, afterSync.Severity);
        Assert.Equal(sample.Repairable, afterSync.CanBeRepaired);
        Assert.True(afterSync.IsActive);

        Assert.True((await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == deactivatedId)).IsActive);
        Assert.False((await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == "F_FAKE_NOT_IN_JSON")).IsActive);
        Assert.True((await context.FenixFailureDefinitions.SingleAsync(d => d.FenixFailureId == orphanId)).IsActive);

        // El total vuelve al del catálogo embebido: sin duplicados de PK (la reinsertada es la misma fila lógica).
        Assert.Equal(baselineCount + 1, await context.FenixFailureDefinitions.CountAsync());
    }

    [Fact]
    public async Task ModePresets_MirrorEmbeddedCatalog_CustomPresetsUntouched() {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.SyncAsync(CancellationToken.None);
        var (embeddedTraining, embeddedRealistic) = await ReadEmbeddedPresetsAsync();

        var knownFailureId = await context.FenixFailureDefinitions.Select(d => d.FenixFailureId).FirstAsync();
        var customPreset = new FailurePreset {
            Name = "Mi preset Custom", Description = "Descripción", TriggerDescription = "Trigger",
            PresetType = PresetTypeEnum.Custom, FlightPhase = ComplexFlightPhaseEnum.Unknown, Difficulty = DifficultyEnum.Easy,
            PresetFailureDefinitions = [new PresetFailureDefinition { FenixFailureId = knownFailureId, Probability = 1.0 }]
        };
        context.FailurePresets.Add(customPreset);
        await context.SaveChangesAsync();
        var customId = customPreset.Id;

        (await GetStateAsync(context)).CatalogsHash = "stale";
        await context.SaveChangesAsync();

        await service.SyncAsync(CancellationToken.None);

        // Los presets de modos espejan el catálogo embebido vigente (curar el JSON actualiza la BD al sincronizar).
        var dbTraining = await context.FailurePresets.Where(p => p.PresetTypeId == (int)PresetTypeEnum.TrainingMode).ToListAsync();
        var dbRealistic = await context.FailurePresets.Where(p => p.PresetTypeId == (int)PresetTypeEnum.RealisticMode).ToListAsync();
        Assert.Equal(embeddedTraining.Count, dbTraining.Count);
        Assert.Equal(embeddedRealistic.Count, dbRealistic.Count);
        Assert.Equal(
            embeddedRealistic.Select(p => (p.Name, p.Description, p.TriggerDescription)).OrderBy(x => x.Name),
            dbRealistic.Select(p => (p.Name, p.Description, p.TriggerDescription)).OrderBy(x => x.Name));

        var customAfter = await context.FailurePresets.Include(p => p.PresetFailureDefinitions).SingleAsync(p => p.Id == customId);
        Assert.Equal("Mi preset Custom", customAfter.Name);
        Assert.Single(customAfter.PresetFailureDefinitions);
    }

    [Fact]
    public async Task LegacyDatabase_SeededByOldSeeders_IsAdoptedWithoutDuplicates() {
        // BD sembrada por el camino viejo (FailuresPersistenceService + PresetService): sin fila de
        // estado. El primer sync debe adoptarla (diff sin cambios) y solo reemplazar presets.
        using var context = CreateContext();
        var legacyFailures = new FailuresPersistenceService(
            new FenixFailureDefinitionRepository(context), Reader);
        var legacyPresets = new PresetService(
            new PresetRepository(NullLogger<PresetRepository>.Instance, context), Loader);
        await legacyFailures.LoadInitialFailuresAsync(CancellationToken.None);
        await legacyPresets.InitializeAsync(CancellationToken.None);

        var jsonFailures = ReadEmbeddedFailures().GetFailuresList();
        var (training, realistic) = await ReadEmbeddedPresetsAsync();

        var service = CreateService(context);
        await service.SyncAsync(CancellationToken.None);

        Assert.Equal(jsonFailures.Count, await context.FenixFailureDefinitions.CountAsync());
        Assert.Equal(training.Count + realistic.Count,
            await context.FailurePresets.CountAsync(p => p.PresetTypeId != (int)PresetTypeEnum.Custom));
        Assert.True(await context.CatalogSyncStates.AnyAsync(s => s.Id == 1));
    }
}
