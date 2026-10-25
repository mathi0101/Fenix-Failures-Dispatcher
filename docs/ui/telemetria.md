# UI: telemetría anónima (ping de uso con PostHog)

Documento de la telemetría de `RealFenixFailures.UI`: qué se envía, cómo, dónde se configura y qué hace falta para que funcione en producción. Fuente única del tema.

## Qué se envía (y qué no)

Un único evento **`app_started`** por instalación y por día (UTC), al abrir la app:

- **Se envía**: versión de la app (`app_version`), versión de Windows (`os_version` + bitness), y `distinct_id` — un **GUID aleatorio** generado en la primera ejecución y persistido en `userprefs.json`. No tiene relación con la persona ni la máquina.
- **No se envía**: IP (el payload no la incluye), nombre de máquina o usuario, rutas, datos de vuelo, contenido del simulador, coordenadas, configuración.
- **País**: PostHog Cloud lo deriva de la IP de la conexión HTTPS al recibir el evento y **no almacena la IP** (comportamiento por defecto de su ingesta; el ping no lleva `$ip`). Es el único dato de procedencia.
- **Estado del simulador/Fenix**: irrelevante para el ping; se envía aunque no haya simulador conectado (mide uso de la app, no de vuelos).

## Flujo

`App.OnStartup` (tras mostrar la ventana, antes de `base.OnStartup`) llama a `Services/TelemetryService.TrySendStartupPing()`:

1. **Gate de configuración**: `Telemetry:Enabled` (default `true`) y `Telemetry:PostHogProjectApiKey` no vacío. Sin clave configurada, no envía nada (así queda el repo y cualquier build sin secreto).
2. **Gate DEBUG**: builds `#if DEBUG` no envían (documentado; para probar telemetría localmente, compilar Release y setear `Telemetry__PostHogProjectApiKey` por variable de entorno).
3. **Gate opt-out**: `UserPreferencesService.TelemetryOptOut` (persistido; el toggle vive en Ajustes → General → «Enviar estadísticas anónimas de uso», como `SettingsWindowViewModel.TelemetryEnabled` invertido al persistir).
4. **Dedup**: por proceso (`Interlocked`, nunca más de un ping por ejecución) y por día (`UserPreferences.LastTelemetryDate`, fecha UTC; se marca **antes** de enviar).
5. **Envío**: `POST {PostHogHost}/i/v0/e/` con JSON `{api_key, event, distinct_id, timestamp, properties}`, `HttpClient` propio con **timeout de 5 s**, fire-and-forget (`_ = SendAsync`), todo resultado no exitoso o error de red se registra solo `Debug`. Nunca lanza ni bloquea el arranque.

Servicio singleton en DI (`HostBuilderExtensions`): `services.Configure<TelemetrySettings>(context.Configuration.GetSection("Telemetry"))` + `AddSingleton<TelemetryService>()`.

## Configuración y secreto (el token NO va versionado)

- `RealFenixFailures.UI/appsettings.json` versiona la sección `Telemetry` con `PostHogProjectApiKey: ""` **vacía**: el repo no contiene ninguna clave.
- **En desarrollo (PC del desarrollador)**: la clave vive en **User Secrets** (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, fuera del repo). El csproj de la UI versiona el `UserSecretsId` (no es un secreto: es el nombre de la carpeta), y `App.xaml.cs` lo carga con `AddUserSecrets<TelemetrySettings>(optional: true)` — en máquinas sin ese store se ignora. También sirve la variable de entorno `Telemetry__PostHogProjectApiKey`. Para editar: click derecho en `RealFenixFailures.UI` → **Manage User Secrets** (VS) o `dotnet user-secrets set "Telemetry:PostHogProjectApiKey" "phc_..." --project RealFenixFailures.UI`.
- El workflow **Release** (`.github/workflows/release.yml`, paso «Arm ZIP portable») inyecta el valor del **secreto de GitHub `POSTHOG_PROJECT_API_KEY`** en el `appsettings.json` de la carpeta de stage, antes de comprimir el ZIP. Si el secreto no existe, el ZIP sale con la clave vacía y la app simplemente no envía.
- El token `phc_...` es el **token público de proyecto** de PostHog (por diseño viaja en los clientes, como una clave de Firebase): permite escribir eventos, no leer datos. Si una versión del ZIP se viera comprometida o llegara spam de eventos, se rota desde PostHog (Settings → Project) y se actualiza el secreto. Las **Personal API keys** de PostHog (lectura/admin) no se usan ni deben estar en el repo ni en la app.
- La clave no debe pegarse en chats, issues ni logs de CI (el `env:` del workflow no se imprime).

## Tableros en PostHog

- **Usuarios activos**: Insight de eventos `app_started` (unicidad por `distinct_id`): total y DAU/WAU.
- **Países**: la propiedad `$geoip_country_name` (derivada server-side) del evento.
- **Adopción de versiones**: desglose por `app_version`.
- Configurar en el proyecto PostHog: dejar activa la geolocalización por IP (Settings → Project); opcionalmente activar «discard PII» no es necesario para este diseño (el payload ya es anónimo).

## Pendiente / no verificado

- Sin verificación en sesión real: correr la app en **Release** en la PC del desarrollador (con el User Secret configurado) y confirmar el evento `app_started` en el tablero de PostHog; y validar que el ZIP publicado lleve la clave inyectada.
- La app no usa el SDK de PostHog (payload HTTP directo, sin dependencia nueva): no hay queues ni reintentos — un ping perdido se compensa con el del día siguiente.
