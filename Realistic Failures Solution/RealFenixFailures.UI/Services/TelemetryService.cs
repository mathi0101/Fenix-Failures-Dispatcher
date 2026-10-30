using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RealFenixFailures.UI.Services;

/// <summary>Configuración de telemetría (sección <c>Telemetry</c> de appsettings).</summary>
public sealed class TelemetrySettings {
    /// <summary>Interruptor general (default <c>true</c>; el usuario puede desactivar el envío desde Ajustes).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Token público de ingesta de PostHog (<c>phc_...</c>). Vacío = telemetría apagada.</summary>
    public string? PostHogProjectApiKey { get; set; }

    /// <summary>Host de ingesta de PostHog.</summary>
    public string PostHogHost { get; set; } = "https://us.i.posthog.com";
}

/// <summary>
/// Eventos anónimos de uso en PostHog (ingesta HTTP directa, sin SDK):
/// <c>app_started</c> al abrir la app y <c>app_closed</c> al cerrarla (con duración),
/// uno por ejecución; y eventos de uso por ocurrencia (<c>realistic_session_started</c>,
/// <c>training_scenario_started</c>). Envían versión de la app, versión de Windows y un
/// GUID aleatorio persistido; el país lo deduce PostHog desde la IP de conexión y la
/// descarta. Nunca envía datos de vuelo, máquina ni usuario (ver docs/ui/telemetria.md).
/// Respeta el opt-out de <see cref="UserPreferencesService"/>, se apaga en builds DEBUG
/// y nunca lanza ni bloquea (salvo la espera acotada del cierre, documentada).
/// </summary>
public sealed class TelemetryService {

    private const int ShutdownSendTimeoutSeconds = 2;

    private readonly TelemetrySettings _settings;
    private readonly UserPreferencesService _preferences;
    private readonly ILogger<TelemetryService> _logger;
    private readonly HttpClient _http;
#if !DEBUG
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private int _sentStartupThisProcess;
    private int _sentShutdownThisProcess;
#endif

    public TelemetryService(TelemetrySettings settings, UserPreferencesService preferences, ILogger<TelemetryService> logger) {
        _settings = settings;
        _preferences = preferences;
        _logger = logger;
        _http = new HttpClient {
            BaseAddress = new Uri(settings.PostHogHost.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("RealFenixFailures-telemetry/1.0");
    }

    /// <summary>Evento <c>app_started</c> por ejecución de la app; best-effort, sin bloquear el arranque.</summary>
    public void TrySendStartupPing() {
#if DEBUG
        // En DEBUG la app no telemetriza; deja solo la constancia en el log.
        _logger.LogDebug("Telemetría omitida: build DEBUG no envía eventos.");
#else
        try {
            if (Interlocked.Exchange(ref _sentStartupThisProcess, 1) != 0)
                return;

            TrySendEvent("app_started");
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "El ping de telemetría no pudo prepararse.");
        }
#endif
    }

    /// <summary>
    /// Evento <c>app_closed</c> con la duración de la ejecución. Al morir el proceso el
    /// envío fire-and-forget se perdería: espera acotada (<see cref="ShutdownSendTimeoutSeconds"/>
    /// s) a que termine el POST. Llamar desde <c>OnExit</c>; un crash no lo emite.
    /// </summary>
    public void TrySendShutdownPing() {
#if DEBUG
        _logger.LogDebug("Telemetría omitida: build DEBUG no envía eventos.");
#else
        try {
            if (!PassesGates())
                return;

            if (Interlocked.Exchange(ref _sentShutdownThisProcess, 1) != 0)
                return;

            var payload = BuildPayload("app_closed", new Dictionary<string, object?> {
                ["session_duration_minutes"] = Math.Round((DateTime.UtcNow - _startedAtUtc).TotalMinutes, 1)
            });
            SendAsync(payload).Wait(TimeSpan.FromSeconds(ShutdownSendTimeoutSeconds));
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "El ping de cierre no pudo enviarse.");
        }
#endif
    }

    /// <summary>
    /// Evento de uso por ocurrencia (no dedup): fire-and-forget, nunca lanza ni bloquea.
    /// Las propiedades extra son agregados elegidos por el usuario en la app (perfil de
    /// riesgo, nombre de escenario); nunca datos del vuelo ni del simulador.
    /// </summary>
    public void TrySendEvent(string eventName, Dictionary<string, object?>? extraProperties = null) {
#if DEBUG
        _logger.LogDebug("Telemetría omitida (DEBUG): {Event}.", eventName);
#else
        try {
            if (!PassesGates())
                return;

            _ = SendAsync(BuildPayload(eventName, extraProperties));
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "El evento {Event} no pudo prepararse.", eventName);
        }
#endif
    }

#if !DEBUG
    private bool PassesGates() {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.PostHogProjectApiKey))
            return false;

        if (_preferences.TelemetryOptOut)
            return false;

        return true;
    }

    private Dictionary<string, object?> BuildPayload(string eventName, Dictionary<string, object?>? extraProperties) {
        var properties = new Dictionary<string, object?> {
            ["app_version"] = AppVersionInfo.Current,
            ["os_version"] = Environment.OSVersion.VersionString + " " + (Environment.Is64BitOperatingSystem ? "x64" : "otro"),
            ["$browser"] = "Real Fenix Failures (desktop)"
        };
        if (extraProperties is not null) {
            foreach (var (key, value) in extraProperties)
                properties[key] = value;
        }

        return new Dictionary<string, object?> {
            ["api_key"] = _settings.PostHogProjectApiKey,
            ["event"] = eventName,
            ["distinct_id"] = _preferences.GetOrCreateAnonymousId(),
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["properties"] = properties,
            // Propiedades de persona: el perfil de la instalación muestra siempre su
            // última versión y OS (útil para cohortes por versión en PostHog).
            ["$set"] = new Dictionary<string, object?> {
                ["app_version"] = AppVersionInfo.Current,
                ["os_version"] = Environment.OSVersion.VersionString
            }
        };
    }
#endif

    private async Task SendAsync(Dictionary<string, object?> payload) {
        try {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("i/v0/e/", content);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("PostHog rechazó el evento con status {StatusCode}.", (int)response.StatusCode);
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "No se pudo enviar el evento de telemetría.");
        }
    }
}
