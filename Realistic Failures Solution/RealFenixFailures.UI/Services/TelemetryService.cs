using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
/// Ping anónimo de uso: un solo evento <c>app_started</c> por instalación y día al abrir la app.
/// Envía versión de la app, versión de Windows y un GUID aleatorio persistido; el país lo deduce
/// PostHog desde la IP de conexión y la descarta. Nunca envía datos de vuelo, máquina ni usuario.
/// Respeta el opt-out de <see cref="UserPreferencesService"/>, se apaga en builds DEBUG y nunca lanza.
/// </summary>
public sealed class TelemetryService {

    private readonly TelemetrySettings _settings;
    private readonly UserPreferencesService _preferences;
    private readonly ILogger<TelemetryService> _logger;
    private readonly HttpClient _http;
#if !DEBUG
    private int _sentThisProcess;
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

    /// <summary>Intenta enviar el ping diario; best-effort, sin excepciones ni bloqueo del arranque.</summary>
    public void TrySendStartupPing() {
#if DEBUG
        // En DEBUG la app no telemetriza; deja solo la constancia en el log.
        _logger.LogDebug("Telemetría omitida: build DEBUG no envía pings.");
#else
        try {
            if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.PostHogProjectApiKey))
                return;

            if (_preferences.TelemetryOptOut)
                return;

            if (Interlocked.Exchange(ref _sentThisProcess, 1) != 0)
                return;

            var today = DateTime.UtcNow.Date;
            if (_preferences.LastTelemetryDate == today)
                return;
            _preferences.LastTelemetryDate = today;

            var payload = new Dictionary<string, object?> {
                ["api_key"] = _settings.PostHogProjectApiKey,
                ["event"] = "app_started",
                ["distinct_id"] = _preferences.GetOrCreateAnonymousId(),
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = new Dictionary<string, object?> {
                    ["app_version"] = AppVersionInfo.Current,
                    ["os_version"] = Environment.OSVersion.VersionString,
                    ["os_bitness"] = Environment.Is64BitOperatingSystem ? "x64" : "otro",
                    ["$os"] = "Windows",
                    ["$browser"] = "Real Fenix Failures (desktop)"
                }
            };
            _ = SendAsync(payload);
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "El ping de telemetría no pudo prepararse.");
        }
#endif
    }

    private async Task SendAsync(Dictionary<string, object?> payload) {
        try {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync("i/v0/e/", content);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("PostHog rechazó el ping con status {StatusCode}.", (int)response.StatusCode);
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "No se pudo enviar el ping de telemetría.");
        }
    }
}
