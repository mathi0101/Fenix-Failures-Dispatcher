# UI: telemetría anónima de uso (PostHog)

Documento de la telemetría de `RealFenixFailures.UI`: qué se envía, cómo, dónde se configura y qué hace falta para que funcione en producción. Fuente única del tema.

> **Propósito (mensaje oficial, va siempre que se describan las estadísticas al usuario)**: son anónimas y existen para mejorar la app. El mensaje al usuario se mantiene simple — sin enumerar eventos, propiedades ni detalles de qué se envía; la promesa de privacidad (nunca datos de vuelo ni personales) y el opt-out sí se mencionan siempre. El detalle técnico completo vive solo en este doc.

## Qué se envía (y qué no)

Eventos anónimos por instalación (ingesta HTTP directa a PostHog, sin SDK):

| Evento | Cuándo | Propiedades extra |
|---|---|---|
| `app_started` | Cada ejecución de la app (al abrir) | — |
| `app_closed` | Cierre normal de la app (`OnExit`) | `session_duration_seconds` (segundos de ejecución) |
| `realistic_session_started` | Cada vez que se activa el modo Realista | `risk_level` (`Low`/`Moderate`/`High`/`Squawk7700`), `has_ofp` |
| `training_scenario_started` | Cada vez que se inicia un escenario de Training | `scenario` (nombre del escenario) |

- **Se envía en todos los eventos**: versión de la app (`app_version`), versión de Windows (`os_version` + `os_bitness`), y `distinct_id` — un **GUID aleatorio** generado en la primera ejecución y persistido en `userprefs.json`. No tiene relación con la persona ni la máquina. Además `$set` (propiedades de persona): `app_version` y `os_version` — el perfil de la instalación muestra siempre su última versión, útil para cohortes por versión.
- **No se envía**: IP (el payload no la incluye), nombre de máquina o usuario, rutas, datos de vuelo, contenido del simulador, coordenadas.
- **Elecciones del usuario que sí viajan**: perfil de riesgo elegido para una sesión realista y nombre del escenario de training elegido — ambas seleccionadas en la UI de la app, anónimas; describen qué usa la gente, no lo que pasó en el vuelo. Cantidad de fallas, sistemas afectados, fases de vuelo y resultados de sesión **no se envían** (candidatos futuros: discutir antes, son zona gris con «datos de vuelo»).
- **País**: PostHog Cloud lo deriva de la IP de la conexión HTTPS al recibir el evento y **no almacena la IP** (comportamiento por defecto de su ingesta; los pings no llevan `$ip`). Es el único dato de procedencia.
- **Dedup**: `app_started`/`app_closed` una vez por proceso; los eventos de sesión/escenario van por ocurrencia (activar el modo dos veces = dos eventos). Ya no existe el dedup diario (`LastTelemetryDate` fue removido de `userprefs.json`).

## Flujo

`App.OnStartup` (tras mostrar la ventana, antes de `base.OnStartup`) llama a `Services/TelemetryService.TrySendStartupPing()`; `App.OnExit` llama a `TrySendShutdownPing()` (antes de detener el host); los ViewModels llaman a `TrySendEvent(...)` en el punto de la acción (`RealisticModeViewModel.ActivateOrStopAsync`, `MainWindowViewModel.StartOrStopTrainingScenarioAsync`).

1. **Gate de configuración**: `Telemetry:Enabled` (default `true`) y `Telemetry:PostHogProjectApiKey` no vacío. Sin clave configurada, no envía nada (así queda el repo y cualquier build sin secreto).
2. **Gate DEBUG**: builds `#if DEBUG` no envían (documentado; para probar telemetría localmente, compilar Release y setear `Telemetry__PostHogProjectApiKey` por variable de entorno o User Secrets).
3. **Gate opt-out**: `UserPreferencesService.TelemetryOptOut` (persistido; el toggle vive en Ajustes → General → «Enviar estadísticas anónimas de uso», como `SettingsWindowViewModel.TelemetryEnabled` invertido al persistir).
4. **Envío**: `POST {PostHogHost}/i/v0/e/` con JSON `{api_key, event, distinct_id, timestamp, properties, $set}`, `HttpClient` propio con **timeout de 5 s**:
   - `app_started` y eventos de sesión/escenario: fire-and-forget, nunca bloquean ni lanzan; todo resultado no exitoso o error de red se registra solo `Debug`.
   - `app_closed`: el proceso está muriendo, un envío sin esperar se perdería — espera acotada a 2 s (`ShutdownSendTimeoutSeconds`) a que termine el POST. Un crash no pasa por `OnExit`: no hay evento de cierre.
5. Nunca lanza: los llamadores envuelven en try/catch de todos modos (la telemetría no puede tirar abajo el arranque ni el cierre).

Servicio singleton en DI (`HostBuilderExtensions`): `services.Configure<TelemetrySettings>(context.Configuration.GetSection("Telemetry"))` + `AddSingleton<TelemetryService>()`.

## Configuración y secreto (el token NO va versionado)

- `RealFenixFailures.UI/appsettings.json` versiona la sección `Telemetry` con `PostHogProjectApiKey: ""` **vacía**: el repo no contiene ninguna clave.
- **En desarrollo (PC del desarrollador)**: la clave vive en **User Secrets** (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, fuera del repo). El csproj de la UI versiona el `UserSecretsId` (no es un secreto: es el nombre de la carpeta), y `App.xaml.cs` lo carga con `AddUserSecrets<TelemetrySettings>(optional: true)` — en máquinas sin ese store se ignora. También sirve la variable de entorno `Telemetry__PostHogProjectApiKey`. Para editar: click derecho en `RealFenixFailures.UI` → **Manage User Secrets** (VS) o `dotnet user-secrets set "Telemetry:PostHogProjectApiKey" "phc_..." --project RealFenixFailures.UI`.
- El workflow **Release** (`.github/workflows/release.yml`, paso «Arm ZIP portable») inyecta el valor del **secreto de GitHub `POSTHOG_PROJECT_API_KEY`** en el `appsettings.json` de la carpeta de stage, antes de comprimir el ZIP. Si el secreto no existe, el ZIP sale con la clave vacía y la app simplemente no envía.
- El token `phc_...` es el **token público de proyecto** de PostHog (por diseño viaja en los clientes, como una clave de Firebase): permite escribir eventos, no leer datos. Si una versión del ZIP se viera comprometida o llegara spam de eventos, se rota desde PostHog (Settings → Project) y se actualiza el secreto. Las **Personal API keys** de PostHog (lectura/admin) no se usan ni deben estar en el repo ni en la app.
- La clave no debe pegarse en chats, issues ni logs de CI (el `env:` del workflow no se imprime).

## Tableros en PostHog

- **Usuarios activos**: Insight de eventos `app_started` (unicidad por `distinct_id`): total y DAU/WAU.
- **Duración de uso**: promedio/distribución de `session_duration_seconds` en `app_closed` (pareo con `app_started` por `distinct_id` y cercanía de timestamps).
- **Países**: la propiedad `$geoip_country_name` (derivada server-side) del evento.
- **Adopción de versiones**: desglose por `app_version`, o cohortes por persona vía `$set`.
- **Modo Realista**: eventos `realistic_session_started` por `risk_level`; % de sesiones con OFP (`has_ofp`).
- **Modo Training**: eventos `training_scenario_started` por `scenario` (escenarios más usados).
- Configurar en el proyecto PostHog: dejar activa la geolocalización por IP (Settings → Project); opcionalmente activar «discard PII» no es necesario para este diseño (el payload ya es anónimo).

## Pendiente / no verificado

- Sin verificación en sesión real: correr la app en **Release** (con el User Secret configurado), abrir y cerrar, iniciar una sesión Realista y un escenario Training, y confirmar los 4 eventos en PostHog (Live events); y validar que el ZIP publicado lleve la clave inyectada.
- La app no usa el SDK de PostHog (payload HTTP directo, sin dependencia nueva): no hay queues ni reintentos — un evento perdido se pierde.
