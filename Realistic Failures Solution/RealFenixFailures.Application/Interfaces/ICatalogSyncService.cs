namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Sincronizador de catálogos embebidos: en cada arranque compara el hash SHA-256 del contenido
/// de los tres JSON curados (failures.json, training_presets.json, realistic_presets.json) contra
/// el último sincronizado (CatalogSyncState, fila única). Si difiere —o no existe— reconcilia la
/// BD con los catálogos: fallas por upsert/dif con baja lógica (nunca DELETE físico, por las FK
/// Restrict del historial) y presets de modos reemplazados en bloque (hojas del grafo de FK;
/// los presets Custom del usuario no se tocan). Todo en una transacción junto con el stamp del
/// hash: o se sincroniza completo o no se toca nada. Idempotente.
/// </summary>
public interface ICatalogSyncService {
    Task SyncAsync(CancellationToken ct);
}
