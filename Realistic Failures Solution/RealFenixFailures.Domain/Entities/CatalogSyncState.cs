namespace RealFenixFailures.Domain.Entities;

/// <summary>
/// Estado del sincronizador de catálogos embebidos (fila única, Id fijo = 1): guarda el hash
/// SHA-256 del contenido combinado de los tres JSON embebidos (failures.json, training_presets.json,
/// realistic_presets.json) tal como fue sembrado/reconciliado la última vez. Si el hash del arranque
/// difiere, <c>CatalogSyncService</c> re-sincroniza los catálogos contra la BD. División de
/// responsabilidades: el esquema es territorio de migraciones EF; el contenido curado de los
/// catálogos, de este sincronizador por hash.
/// </summary>
public class CatalogSyncState {
    public int Id { get; set; }
    public required string CatalogsHash { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}
