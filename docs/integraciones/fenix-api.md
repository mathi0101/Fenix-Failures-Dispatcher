# Integración Fenix API

Cliente HTTP de la API local de Fenix Simulations en `RealFenixFailures.Integrations.Fenix`.

## Propósito

- Consultar el estado real de las fallas del Fenix A320 (catalogadas y su condición/estado).
- Armar/desarmar fallas en el avión real.
- Health check cacheado para la UI.
- Reset de fallas activas al detener un modo.

## Endpoints usados

- `GET /fenix/failures/manual` — lista las fallas manuales disponibles (bloques ATA, grupos, fallas con estado).
- `POST /fenix/failures/saveManual` — aplica (o desactiva) una falla manual.

El URI final se construye desde configuración (`BaseUrl` + `Port` + path).

## Configuración (por nombres de claves)

Sección `FenixApi` (los valores concretos son locales; no se copian acá):

- `BaseUrl` — host de la API (valor local de cada máquina).
- `Port` — predeterminado **8083**.
- `ManualFailuresPath` — ruta GET.
- `SaveManualPath` — ruta POST.
- `HealthCheckIntervalSeconds` — TTL del health check cacheado (10).
- `HealthCheckTimeout` — timeout de cada llamada (2 s).

## Cliente real y mock

- **`FenixApiClient`** (Release): `HttpClient` configurado por DI; implementa `IFenixApiClient` (`IsApiAlive`, `GetManualFailuresAsync`, `SendFailureAsync`); maneja `HttpRequestException`/timeouts devolviendo `false`/`null` y logueando en `Debug` sin stacktrace (son fallos de conectividad esperables ya reportados como cambio de estado).
- **`MockFenixApiClient`** (DEBUG): mantiene estado en memoria de 10 fallas de ejemplo; responde el mismo contrato.
- **`FenixApiFailureService`** (`Services/FenixApiFailureService.cs`) implementa `IFenixFailureApiDispatcher`: `ArmFailureAsync`, `GetAllFailuresAsync`, `ResetAllFailuresAsync`, `IsApiAvailableAsync` (health check cacheado con `SemaphoreSlim`).

## Logs y transiciones (niveles)

Desde 2026-10-15 el estado de la API se informa una sola vez por transición, no por intento:

- `FenixApiFailureService.UpdateHealthState` emite una única línea `Information` corta por cambio de estado: **`API Fenix conectada`** al pasar a conectada y **`API Fenix desconectada`** al pasar a desconectada, con un `lock` simple que hace seguro el chequeo ante llamadas concurrentes (armado/desarme, `GetAllFailuresAsync` y `IsApiAvailableAsync` actualizan el mismo estado). El primer resultado `false` no se loguea como desconexión inicial y los resultados repetidos no generan líneas.
- `FenixApiClient`: los fallos de conectividad/timeout esperables en la consulta de disponibilidad (`IsApiAlive`) y en las llamadas HTTP ya reportadas como cambio de estado (`GetManualFailuresAsync`, `SendFailureAsync`) loguean `Debug` sin stacktrace (`HttpRequestException`/`TaskCanceledException`); los errores HTTP con respuesta no-success (p. ej. 4xx/5xx) siguen siendo `Warning` y las excepciones no esperadas no se silencian.

El DTO de armado/desarme es `FenixArmFailureRequest(Id, Failed, FailureCondition)` — **no existe un campo `armed`**. Una falla se considera activa/armada cuando `FailureCondition != null` (derivado en `FenixFailureDto.IsArmed`) o cuando `Failed == true`.

## Reset

`ResetAllFailuresAsync` consulta el estado actual, desactiva las fallas activas (`saveManual` con `(Failed=false, FailureCondition=null)`) y verifica que todas queden inactivas. Se invoca en `EngineOrchestrator.StopCurrentModeAsync`.

## Armado y desarme individual

- **Armado**: `ISimulatorConnectionService.ExecuteFailureAsync` arma una falla puntual en el avión (vía `FenixApiFailureService.ArmFailureAsync` / `POST /fenix/failures/saveManual`) con su condición `(Failed=false, FailureCondition=...)`.
- **Desarme individual**: `ISimulatorConnectionService.DisarmFailureAsync(fenixFailureId)` envía la request de desarme sin condición (`FenixApiFailureService.DisarmFailureAsync` con `(Failed=false, FailureCondition=null)`), que es la misma semántica que usa el reset: una falla con `Failed=false` y sin condición queda inactiva. Con id vacío no llama al dispatcher.
- **Compensación**: si en `RealisticSessionEngine.TryTriggerFailureAsync` una falla recién armada no logra persistirse como `TriggeredFailure`, se intenta **desarmar esa falla** en Fenix; si el desarme también falla, la falla queda armada sin registrar y se loguea `Critical` (no es un error fatal de la sesión). Si la persistencia se **cancela** (`OperationCanceledException`), igual se intenta el desarme (con token independiente, para no dejar la falla armada) y recién después se propaga la cancelación.
- **Pruebas automatizadas**: `SimulatorConnectionServiceTests` cubre el request de desarme y el id vacío; `RealisticSessionEngineTests` cubre la compensación con desarme exitoso y fallido.
- El desarme/reset **real** sigue pendiente de verificación manual (ver abajo).

## Verificaciones manuales pendientes

- Health check con Fenix corriendo y avión cargado.
- Listado real de fallas vía `GET /fenix/failures/manual`.
- Armado/desarme real de una falla vía `POST /fenix/failures/saveManual`.
- Reset efectivo de fallas activas al detener el modo.
- Desarme individual (`DisarmFailureAsync`) de una falla armada real.
- Uso completo del modo Realistic con Fenix real (no solo mocks).

Diseño y estado del modo que consume esta API: [docs/realistic-mode/estado-y-diseno.md](../realistic-mode/estado-y-diseno.md).
