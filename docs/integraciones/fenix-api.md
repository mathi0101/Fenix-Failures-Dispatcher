# Integración Fenix API

Cliente HTTP de la API local de Fenix Simulations en `RealFenixFailures.Integrations.Fenix`.

## Propósito

- Consultar el estado real de las fallas del Fenix A320 (catalogadas y su condición/estado).
- Armar/desarmar fallas en el avión real.
- Health check cacheado para la UI.
- Reset de fallas activas al detener un modo.

## API cruda — contrato HTTP

El URI final se construye desde configuración (`BaseUrl` + `Port` + path). Son dos endpoints:

- `GET /fenix/failures/manual` — lectura del estado de todas las fallas manuales disponibles.
- `POST /fenix/failures/saveManual` — escritura: arma, desarma, activa o desactiva una falla.

### GET /fenix/failures/manual — lectura de todas las fallas

Devuelve un JSON con la **misma forma que `failures.json`** (el catálogo embebido): `atas` → `{id, title, shortTitle, groups}` → `{groupName, failures}`. La diferencia es que cada unidad de falla trae su **estado real actual** del avión:

```json
{
  "id": "F_PNEUMATIC_CPC_1",
  "title": "CPC 1",
  "failureCondition": null,
  "failed": false
}
```

| Campo | Tipo | Significado |
|---|---|---|
| `id` | string | Identificador estable de la falla; es la clave para `saveManual`. |
| `title` | string | Nombre que muestra Fenix. |
| `failed` | bool | **Falla activa/inyectada en el avión** (equivale a "Failed" marcado en el EFB con Apply Changes). `false` = no está fallando. |
| `failureCondition` | object \| null | **Armado de la falla**: presente mientras la falla está en espera de que se cumpla una condición; `null` = sin armado. Ver estructura en la sección POST. |

`failed` y `failureCondition` son dimensiones independientes: una falla puede estar fallando sin armado, armada sin fallar, ambas o ninguna (las pestañas **Failed** y **Armed** del EFB muestran cada dimensión). El parser en `FenixStreamFailuresReaderService` deserializa este documento y lo mapea a `AllFenixFailuresResponseDto` (bloques ATA → grupos → `FenixFailureDto`).

**La respuesta no incluye severidad** (ni `Minor`/`Major`/`Critical`); tampoco `failures.json`. Para los perfiles con topes por severidad hace falta un catálogo propio curado por `id` (Fase 1 del plan del modo Realistic).

### POST /fenix/failures/saveManual — escritura (armar/desarmar/activar/desactivar)

Cuerpo (el mismo que serializa `FenixSaveManualRequest`):

```json
{
  "id": "F_ENG_1_OVHT",
  "failed": false,
  "failureCondition": { "ias": 1000 }
}
```

| Campo | Tipo | Efecto |
|---|---|---|
| `id` | string | Falla objetivo (id devuelto por el GET). |
| `failed` | bool | `true` inyecta la falla **de inmediato**; `false` la retira del estado fallando. |
| `failureCondition` | object \| null | Detalle del armado; `null` quita el armado. |

`failureCondition` acepta las **cinco opciones de armado del EFB, combinables** (la guía oficial de FenixSim las describe como "Arm Failure Condition"):

| Propiedad | Condición en el EFB | Semántica |
|---|---|---|
| `ias` | Above IAS | Se dispara al superar esta IAS. |
| `alt` | Above Alt AMSL | Se dispara al superar esta altitud (MSL). |
| `altb` | Below Alt AMSL | Se dispara al quedar por debajo de esta altitud (MSL). |
| `time` | After Time | Se dispara tras este número de segundos. |
| `afterEvent` | After Event | Evento de disparo (p. ej. `V1`, despegue o aterrizaje). |
| `afterEventSeconds` | — | Segundos adicionales después de `afterEvent`. |

Combinaciones (p. ej. `{"ias": 150, "alt": 2000}`): la falla se dispara cuando se cumplen todas las condiciones presentes.

**Operaciones típicas** (mismo endpoint, payload distinto):

| Operación | Payload | Efecto |
|---|---|---|
| Activar inmediata | `{"id":"...", "failed":true, "failureCondition":null}` | La falla ocurre ya (pestaña Failed del EFB). |
| Armar con condición | `{"id":"...", "failed":false, "failureCondition":{...}}` | La falla queda en espera y se dispara al cumplirse la condición (pestaña Armed). |
| Desarmar / limpiar | `{"id":"...", "failed":false, "failureCondition":null}` | Quita armado y falla activa; deja la falla totalmente limpia. Es la request que usan desarme individual y reset. |

No existe un campo `armed` en el protocolo: la intención se codifica en `failed` + `failureCondition`.

### Derivación de estados (modo Realistic)

| Lectura desde el GET | Significado |
|---|---|
| `failed=false` ∧ `failureCondition=null` | Limpia |
| `failed=false` ∧ `failureCondition≠null` | Armada (en espera) |
| `failed=true` | Activa (inyectada) |

En código, `FenixFailureDto.IsArmed` deriva `failureCondition != null`; "activa" es `failed == true`.

### Mapeo con la guía oficial de FenixSim

Guía oficial: [Failures Guide in FenixSim Aircraft](https://support.fenixsim.com/hc/en-us/articles/12457317731855-Failures-Guide-in-FenixSim-Aircraft). Coincidencias relevantes:

- **"Failed"** en el EFB = `failed:true` en `saveManual`.
- **"Arm Failure Condition"** con sus 5 opciones = los campos de `failureCondition` de arriba.
- **Quitar una falla manual** (unselect Failed + Apply Changes) = `failed:false` sin condición; la re-lectura por GET lo refleja. Este es el mecanismo por el que la app podrá detectar que el piloto resolvió una falla desde el FMGC/EFB (diseño objetivo §10, "lectura en vez de evento") sin que la app intervenga.
- **Fallas random** (MTBF) y **mantenimiento en tierra** (solo en rampa, por categoría, hasta 10 minutos de reparación) viven en MCDU → Config → Random Failures, **no** en la API manual: la app no las gestiona, pero el mantenimiento en rampa es la vía del FMGC para despejar fallas y debe entenderse como "resuelta por piloto" en el historial.

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
- **`MockFenixApiClient`** (DEBUG): mock v2. Sirve el **catálogo real embebido** `RealFenixFailures.Domain/Resources/failures.json` (mismos IDs, títulos, severidad, repairable y estructura ATA que consume el motor) con estado `failed` mutable en memoria persistente entre lecturas, y admite IDs desconocidos registrándolos con warning. Así, una inyección del motor con cualquier ID del catálogo se refleja en la re-lectura del estado, igual que con la API real (antes eran 10 fallas hardcodeadas que casi no coincidían con el catálogo).
- **Selección mock/real por config**: `FenixApi:UseMock` en `appsettings.json` (default `true` en el repo para desarrollo sin Fenix; solo aplica en build DEBUG — en Release siempre va contra la API real).
- **`FenixApiFailureService`** (`Services/FenixApiFailureService.cs`) implementa `IFenixFailureApiDispatcher`: `ArmFailureAsync`, `GetAllFailuresAsync`, `ResetAllFailuresAsync`, `IsApiAvailableAsync` (health check cacheado con `SemaphoreSlim`).

## Logs y transiciones (niveles)

Desde 2026-10-15 el estado de la API se informa una sola vez por transición, no por intento:

- `FenixApiFailureService.UpdateHealthState` emite una única línea `Information` corta por cambio de estado: **`API Fenix conectada`** al pasar a conectada y **`API Fenix desconectada`** al pasar a desconectada, con un `lock` simple que hace seguro el chequeo ante llamadas concurrentes (armado/desarme, `GetAllFailuresAsync` y `IsApiAvailableAsync` actualizan el mismo estado). El primer resultado `false` no se loguea como desconexión inicial y los resultados repetidos no generan líneas.
- `FenixApiClient`: los fallos de conectividad/timeout esperables en la consulta de disponibilidad (`IsApiAlive`) y en las llamadas HTTP ya reportadas como cambio de estado (`GetManualFailuresAsync`, `SendFailureAsync`) loguean `Debug` sin stacktrace (`HttpRequestException`/`TaskCanceledException`); los errores HTTP con respuesta no-success (p. ej. 4xx/5xx) siguen siendo `Warning` y las excepciones no esperadas no se silencian.

El DTO de armado/desarme es `FenixArmFailureRequest(Id, Failed, FailureCondition)` — **no existe un campo `armed`**. Una falla se considera activa/armada cuando `FailureCondition != null` (derivado en `FenixFailureDto.IsArmed`) o cuando `Failed == true`.

## Reset

`ResetAllFailuresAsync` consulta el estado actual vía `GetAllFailuresAsync`, y para cada falla con `failed=true` o `failureCondition != null` envía el desarme `(Failed=false, FailureCondition=null)`. Se invoca en `EngineOrchestrator.StopCurrentModeAsync`.

**Limitación conocida** (`FenixApiFailureService.cs:80-94`): no comprueba el resultado HTTP de cada desarme (los envía y sigue) y la verificación final solo confirma `failed=false`, **sin mirar `FailureCondition`**. Hasta reescribirlo con desarme verificado por envío y re-lectura de ambos campos, no conviene apoyar en él la pausa que desarma, el cierre de sesión confiable ni la recuperación de fallas (ver docs/realistic-mode/estado-y-diseno.md).

## Armado y desarme individual

- **Armado**: `ISimulatorConnectionService.ExecuteFailureAsync` arma una falla puntual en el avión (vía `FenixApiFailureService.ArmFailureAsync` / `POST /fenix/failures/saveManual`) con su condición `(Failed=false, FailureCondition=...)`.
- **Desarme individual**: `ISimulatorConnectionService.DisarmFailureAsync(fenixFailureId)` envía la request de desarme sin condición (`FenixApiFailureService.DisarmFailureAsync` con `(Failed=false, FailureCondition=null)`), que es la misma semántica que usa el reset: una falla con `Failed=false` y sin condición queda inactiva. Con id vacío no llama al dispatcher.
- **Compensación**: si en `RealisticSessionEngine.TryTriggerFailureAsync` una falla recién armada no logra persistirse como `TriggeredFailure`, se intenta **desarmar esa falla** en Fenix; si el desarme también falla, la falla queda armada sin registrar y se loguea `Critical` (no es un error fatal de la sesión). Si la persistencia se **cancela** (`OperationCanceledException`), igual se intenta el desarme (con token independiente, para no dejar la falla armada) y recién después se propaga la cancelación.
- **Pruebas automatizadas**: `SimulatorConnectionServiceTests` cubre el request de desarme y el id vacío; `RealisticSessionEngineTests` cubre la compensación con desarme exitoso y fallido.
- El desarme/reset **real** sigue pendiente de verificación manual (ver abajo).

## Verificación con la API real

**Confirmado de forma manual con el avión cargado** (revisión del usuario):

- `GET /fenix/failures/manual` devuelve el catálogo completo (misma forma que `failures.json`) con el estado real de cada falla (`failed` / `failureCondition`).
- `POST /fenix/failures/saveManual` arma, desarma, activa y desactiva fallas reales según el payload documentado arriba.

**Pendiente de verificación** (no probado de punta a punta):

- Health check con la app levantada y Fenix corriendo.
- Reset con desarme verificado por envío y re-lectura de ambos campos.
- Detección de "resuelta por piloto": despejar una falla desde el FMGC/EFB y observar que el GET lo refleja (base del diseño objetivo §10, aún sin validar en sesión).
- Mantenimiento en rampa (MCDU → Config → Random Failures) y su reflejo en el GET.
- Uso completo del modo Realistic con Fenix real (no solo mocks).

Diseño y estado del modo que consume esta API: [docs/realistic-mode/estado-y-diseno.md](../realistic-mode/estado-y-diseno.md).
