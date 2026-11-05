using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ICatalogSyncService"/>. Claves naturales del diff: sistemas por
/// <c>AtaId</c>, grupos por <c>(AtaId, nombre)</c> y fallas por <c>FenixFailureId</c> (PK).
/// Reglas del diff de fallas: alta nueva (INSERT), cambio curado (UPDATE campo a campo solo si
/// difirió), reactivación (volvió al JSON) y baja lógica <c>IsActive = false</c> (salió del JSON;
/// nunca DELETE físico: TriggeredFailure y PresetFailureDefinition la referencian con FK Restrict).
/// Sistemas y grupos son upsert-only (renombrar un grupo crea uno nuevo y mueve las definiciones;
/// el viejo queda huérfano y sin consumidores — no se borra para no complejizar el diff).
/// Presets Training/Realistic: se reemplazan completos (DELETE + reinsert en bloque); son hojas
/// del grafo de FK (solo los referencian sus hijos en Cascade) y TriggeredFailure guarda
/// PresetId/PresetName como snapshot sin FK. Los Custom del usuario jamás se borran.
/// </summary>
public class CatalogSyncService : ICatalogSyncService {
    private const int SingleRowId = 1;

    private readonly RealFenixDbContext _dbContext;
    private readonly IFenixStreamFailuresReaderService _failuresReader;
    private readonly IPresetsLoader _presetsLoader;
    private readonly ILogger<CatalogSyncService> _logger;

    public CatalogSyncService(RealFenixDbContext dbContext, IFenixStreamFailuresReaderService failuresReader,
        IPresetsLoader presetsLoader, ILogger<CatalogSyncService> logger) {
        _dbContext = dbContext;
        _failuresReader = failuresReader;
        _presetsLoader = presetsLoader;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken ct) {
        var currentHash = ComputeCatalogsHash();

        var state = await _dbContext.CatalogSyncStates.SingleOrDefaultAsync(s => s.Id == SingleRowId, ct);
        if (state is not null && state.CatalogsHash == currentHash) return;

        // Los catálogos se leen y parsean ANTES de abrir la transacción: si un recurso embebido
        // está roto, se explota sin tocar la BD.
        var failureCatalog = await ReadFailureCatalogAsync(ct);
        var trainingPresets = await _presetsLoader.GetTrainingPresetsAsync(ct);
        var realisticPresets = await _presetsLoader.GetRealisticPresetsAsync(ct);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try {
            var counts = await SyncFailuresAsync(failureCatalog, ct);
            var presetsReplaced = await SyncModePresetsAsync(trainingPresets, realisticPresets, ct);

            if (state is null) {
                _dbContext.CatalogSyncStates.Add(new CatalogSyncState {
                    Id = SingleRowId, CatalogsHash = currentHash, SyncedAtUtc = DateTime.UtcNow
                });
            } else {
                state.CatalogsHash = currentHash;
                state.SyncedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Sincronización de catálogos aplicada (hash {Hash}). Fallas: +{Inserted} nuevas, ~{Updated} actualizadas, " +
                "{Deactivated} dadas de baja, {Reactivated} reactivadas (total JSON: {JsonTotal}). Presets de modos reemplazados: {Presets}.",
                currentHash, counts.Inserted, counts.Updated, counts.Deactivated, counts.Reactivated, counts.JsonTotal, presetsReplaced);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error sincronizando catálogos embebidos; transacción revertida. Hash esperado: {Hash}", currentHash);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<AllFenixFailuresResponseDto> ReadFailureCatalogAsync(CancellationToken ct) {
        using var stream = EmbeddedJsonLoader.LoadFromEmbeddedJson(EmbeddedResourceNames.FailuresCatalog)
            ?? throw new InvalidOperationException("No se pudo leer failures.json embebido");
        var catalog = await _failuresReader.ReadAsync(stream, ct);
        if (catalog.MajorGroups.Count == 0)
            throw new InvalidOperationException("failures.json embebido no contiene sistemas");
        return catalog;
    }

    /// <summary>Hash SHA-256 del contenido combinado de los tres JSON embebidos, con el nombre de
    /// cada recurso como separador (evita colisiones por reordenamiento y ancla el hash a la versión).</summary>
    private static string ComputeCatalogsHash() {
        using var sha = SHA256.Create();
        using var combined = new MemoryStream();
        foreach (var name in new[] {
                     EmbeddedResourceNames.FailuresCatalog,
                     EmbeddedResourceNames.TrainingPresets,
                     EmbeddedResourceNames.RealisticPresets
                 }) {
            combined.Write(Encoding.UTF8.GetBytes(name + "\n"));
            using var stream = EmbeddedJsonLoader.LoadFromEmbeddedJson(name)
                ?? throw new InvalidOperationException($"No se pudo leer {name} embebido");
            stream.CopyTo(combined);
            combined.Write("\n"u8);
        }

        return Convert.ToHexString(sha.ComputeHash(combined.ToArray()));
    }

    private async Task<(int Inserted, int Updated, int Deactivated, int Reactivated, int JsonTotal)> SyncFailuresAsync(
        AllFenixFailuresResponseDto catalog, CancellationToken ct) {
        // Árbol completo persistido, trackeado: una sola query con includes para clasificar el diff en memoria.
        var systems = await _dbContext.FenixFailureSystems
            .Include(s => s.FailureGroups)
            .ThenInclude(g => g.FailureDefinitions)
            .ToListAsync(ct);

        var jsonFailures = catalog.GetFailuresList();
        var jsonIds = jsonFailures.Select(f => f.FenixId).ToHashSet();

        var systemsByAta = systems.GroupBy(s => s.AtaId).ToDictionary(g => g.Key, g => g.First());
        var bdFailuresById = systems.SelectMany(s => s.FailureGroups)
            .SelectMany(g => g.FailureDefinitions)
            .GroupBy(f => f.FenixFailureId)
            .ToDictionary(g => g.Key, g => g.First());

        // Upsert de sistemas y grupos en orden topológico (sistema → grupo → definición), dejando
        // resuelto el grupo destino de cada falla del JSON.
        var groupByFenixId = new Dictionary<string, FenixFailureGroup>();
        foreach (var major in catalog.MajorGroups) {
            if (!systemsByAta.TryGetValue(major.Id, out var system)) {
                system = new FenixFailureSystem { Name = major.Title, ShortName = major.ShortTitle, AtaId = major.Id };
                systemsByAta[major.Id] = system;
                _dbContext.FenixFailureSystems.Add(system);
            } else if (system.Name != major.Title || system.ShortName != major.ShortTitle) {
                system.Name = major.Title;
                system.ShortName = major.ShortTitle;
            }

            foreach (var jsonGroup in major.SystemGroups) {
                var group = system.FailureGroups.FirstOrDefault(g => g.Name == jsonGroup.Name);
                if (group is null) {
                    group = new FenixFailureGroup { Name = jsonGroup.Name, System = system };
                    system.FailureGroups.Add(group);
                }

                foreach (var jsonFailure in jsonGroup.Failures)
                    groupByFenixId[jsonFailure.FenixId] = group;
            }
        }

        int inserted = 0, updated = 0, reactivated = 0;
        foreach (var jsonFailure in jsonFailures) {
            var group = groupByFenixId[jsonFailure.FenixId];
            if (!bdFailuresById.TryGetValue(jsonFailure.FenixId, out var definition)) {
                _dbContext.FenixFailureDefinitions.Add(new FenixFailureDefinition {
                    FenixFailureId = jsonFailure.FenixId,
                    Name = jsonFailure.Description,
                    Severity = jsonFailure.Severity,
                    CanBeRepaired = jsonFailure.Repairable,
                    Group = group,
                    IsActive = true
                });
                inserted++;
                continue;
            }

            var changed = false;
            if (definition.Name != jsonFailure.Description) {
                definition.Name = jsonFailure.Description;
                changed = true;
            }

            if (definition.Severity != jsonFailure.Severity) {
                definition.Severity = jsonFailure.Severity;
                changed = true;
            }

            if (definition.CanBeRepaired != jsonFailure.Repairable) {
                definition.CanBeRepaired = jsonFailure.Repairable;
                changed = true;
            }

            // Navegación en vez de GroupId crudo: el grupo puede ser nuevo (Id = 0 hasta el save).
            if (!ReferenceEquals(definition.Group, group)) {
                definition.Group = group;
                changed = true;
            }

            if (!definition.IsActive) {
                definition.IsActive = true;
                changed = true;
                reactivated++;
            }

            if (changed) updated++;
        }

        // Baja lógica: activas que el JSON vigente ya no declara. La fila queda intacta para el historial.
        int deactivated = 0;
        foreach (var definition in bdFailuresById.Values) {
            if (definition.IsActive && !jsonIds.Contains(definition.FenixFailureId)) {
                definition.IsActive = false;
                deactivated++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return (inserted, updated, deactivated, reactivated, jsonFailures.Count);
    }

    private async Task<int> SyncModePresetsAsync(IReadOnlyList<FailurePreset> trainingPresets,
        IReadOnlyList<FailurePreset> realisticPresets, CancellationToken ct) {
        // Integridad curada: todo FenixFailureId referenciado por un preset debe existir en el catálogo
        // (activo o de baja lógica). El diff de fallas corre antes, así que las altas ya están en memoria.
        var knownIds = (await _dbContext.FenixFailureDefinitions.Select(f => f.FenixFailureId).ToListAsync(ct)).ToHashSet();
        foreach (var preset in trainingPresets.Concat(realisticPresets)) {
            foreach (var definition in preset.PresetFailureDefinitions.Where(d => !knownIds.Contains(d.FenixFailureId)))
                throw new InvalidOperationException(
                    $"El preset '{preset.Name}' referencia una falla inexistente en failures.json: {definition.FenixFailureId}");
        }

        // Reemplazo en bloque de los dos tipos sembrados; el DELETE físico es seguro: los presets son
        // hojas del grafo (solo sus hijos en Cascade) y el historial guarda PresetId/PresetName sin FK.
        var stale = await _dbContext.FailurePresets
            .Include(p => p.PresetFailureDefinitions)
            .Where(p => p.PresetTypeId == (int)PresetTypeEnum.TrainingMode || p.PresetTypeId == (int)PresetTypeEnum.RealisticMode)
            .ToListAsync(ct);
        _dbContext.FailurePresets.RemoveRange(stale);
        _dbContext.FailurePresets.AddRange(trainingPresets);
        _dbContext.FailurePresets.AddRange(realisticPresets);
        await _dbContext.SaveChangesAsync(ct);

        return trainingPresets.Count + realisticPresets.Count;
    }
}
