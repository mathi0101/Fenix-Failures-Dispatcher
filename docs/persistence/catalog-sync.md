# Sincronización de catálogos embebidos (CatalogSyncService)

Estado: **implementado** (2026-11-04, pendiente de verificación en sesión real MSFS/Fenix). Diseño acordado en la tarea del issue #12 (presets en inglés no llegaban a las BDs existentes porque los seeds eran «solo si está vacío»).

## Problema

Los catálogos curados (`failures.json`, `training_presets.json`, `realistic_presets.json` — EmbeddedResource versionadas) se sembraban a la SQLite una sola vez: `FailuresPersistenceService.LoadInitialFailuresAsync` y `PresetService.InitializeAsync` hacían «sembrar solo si no hay nada del tipo». Consecuencia: cualquier curación posterior (traducciones, calibraciones, presets nuevos) nunca llegaba a las BDs ya creadas —ni la de desarrollo ni la de la PC de testing— y no había camino de refresco sin borrar la base (inaceptable: pierde sesiones, historial y desgaste).

## Decisión: hash de contenido + reconciliación por tipo de dato

**Detección**: hash SHA-256 del contenido combinado de los tres JSON embebidos (con el nombre de cada recurso como separador), persistido en la fila única `CatalogSyncState (Id=1, CatalogsHash, SyncedAtUtc)`. En cada arranque `DatabaseSeeder → ICatalogSyncService.SyncAsync` compara el hash vigente: si es igual, **no-op** (costo: 1 query + hashear 3 streams chicos); si difiere —o no hay fila—, reconcilia. Hash de contenido en vez de número de versión manual: no hay nada que bumpar a mano y cualquier curación dispara el refresco exactamente cuando hace falta.

**Reconciliación por tipo** (el grafo de FK manda):

| Dato | Estrategia | Por qué |
|---|---|---|
| `FenixFailureDefinition` | Upsert por `FenixFailureId` (PK) + **baja lógica** `IsActive = false` | `TriggeredFailure` y `PresetFailureDefinition` la referencian con FK **Restrict**: un DELETE físico del historial viviente tira `SQLite Error 19`. Con baja lógica, la fila queda intacta y el historial consistente (además guarda `FailureName`/`Severity` por copia como snapshot). |
| `FenixFailureSystem` / `FenixFailureGroup` | **Upsert-only** (por `AtaId` / `(AtaId, nombre)`), nunca DELETE | Son metadatos sin consumidores directos; renombrar un grupo crea uno nuevo y mueve las definiciones (el diff resuelve por navegación, no por `GroupId` crudo). El viejo queda huérfano sin efecto. |
| `FailurePreset` Training/Realistic | **DELETE + reinsert en bloque** (con sus hijos en Cascade) | Son hojas del grafo de FK; `TriggeredFailure.PresetId` es columna snapshot **sin FK** (guarda `PresetName`). El reemplazo completo es lo más simple y honesto. |
| `FailurePreset` Custom | **Jamás se toca** | Datos del usuario, no del catálogo. |
| Esquema (tablas/columnas) | Migraciones EF | Territorio de migraciones: `SeedData` estático (wear rates) sigue por migraciones (`RebalanceWearRatesX25`). Regla: *esquema = migraciones, contenido curado = hash-sync*. |

## El diff de fallas en detalle

Dos lecturas (el árbol persistido trackeado con includes + el árbol del JSON con el mismo parser que el seed) y clasificación en memoria — **cero queries por fila** (384 fallas, 19 sistemas, 159 grupos):

| ¿En el JSON? | ¿En la BD? | Acción |
|---|---|---|
| Sí | No | INSERT |
| Sí | Sí, igual y activa | Nada (change detection campo a campo: no se toca el change tracker) |
| Sí | Sí, con cambios curados | UPDATE solo de lo que difirió (`Name`, `Severity`, `CanBeRepaired`, grupo por navegación) |
| Sí | Sí, inactiva | UPDATE + reactivar |
| No | Sí, activa | Soft delete (`IsActive = false`) |
| No | Sí, ya inactiva | Nada |

El `FenixFailureId` es inmutable por contrato: si Fenix renombra un ID, es *falla nueva + baja lógica de la vieja*, que es lo correcto para el historial.

## Garantías

- **Atomicidad**: todo el resync (fallas + presets + stamp del hash) corre en **una transacción** y el stamp se escribe dentro de ella. Si la app muere a mitad, la BD queda vieja-consistente y el próximo arranque reintenta (idempotente por diseño).
- **Orden topológico**: sistemas → grupos → fallas → presets (las FK bajan en esa dirección). El validador de presets falla la transacción si un preset referencia un `FenixFailureId` inexistente (los catálogos se curan juntos; el error tiene que explotar ruidoso en dev, no colarse a producción).
- **Consumo del motor**: los sitios de selección de candidatos (`GetFlightUniverseAsync`, seed de tierra) filtran `IsActive`; `GetAtaLookupAsync` sigue usando el catálogo completo porque la reconciliación de fallas ya disparadas necesita también las de baja lógica (una sesión viva puede referenciar una falla recién jubilada).
- **Adopción de BDs legadas**: una BD sembrada por el camino viejo (sin fila de estado) se adopta sin duplicados en el primer sync.

## Operación

- Forzar re-sync sin cambiar los JSON: actualizar a mano `CatalogSyncStates.CatalogsHash` a cualquier otro valor (o borrar la fila) y arrancar la app.
- Diagnóstico: el servicio loguea cada resync con su hash y conteos (`+nuevas, ~actualizadas, dadas de baja, reactivadas`).

## Tests

`RealFenixFailures.TestUnits/Persistence/CatalogSyncServiceTests.cs` (SQLite in-memory): siembra inicial completa (384/19, distribución de severidad 173/179/32, 20 repairable, 6+44 presets), no-op idempotente (no reemplaza presets), reconciliación de drift (update + soft delete + reactivación + reinsert de una fila borrada), presets reemplazados con Custom intacto, y adopción de BD legada sembrada por `FailuresPersistenceService`/`PresetService`.

## Límites conocidos

- Los JSONs embebidos son la única fuente: no hay edición de catálogo desde la UI.
- Si a futuro un preset custom del usuario pasara a referenciar fallas dadas de baja, el reemplazo de presets no lo afecta (Custom no se reemplaza); una falla inactiva referenciada por un preset de modo solo puede ocurrir si los JSON quedan inconsistentes entre sí — lo cubre el validador del sync.
- Verificación en sesión real MSFS/Fenix: pendiente (se hace al cierre del milestone; en dev se probó vía suite automatizada).
