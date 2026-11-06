using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace RealFenixFailures.UI.Services;

using RealFenixFailures.Infrastructure;

/// <summary>
/// Preferencias de usuario de la app, persistidas en
/// %AppData%\RealFenixFailures\userprefs.json (misma carpeta base que la BD y
/// los logs; ver <see cref="AppPaths"/>).
/// </summary>
public class UserPreferencesService {

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<UserPreferencesService> _logger;
    private UserPreferences _preferences;

    public UserPreferencesService(ILogger<UserPreferencesService> logger) {
        _logger = logger;
        _filePath = Path.Combine(AppPaths.AppDataDirectory, "userprefs.json");
        _preferences = Load();
    }

    /// <summary>ID de usuario de SimBrief (alias) usado por la app.</summary>
    public string SimbriefUserId {
        get {
            lock (_lock) return _preferences.SimbriefUserId ?? string.Empty;
        }
        set {
            lock (_lock) {
                if (_preferences.SimbriefUserId == value)
                    return;
                _preferences.SimbriefUserId = value;
            }
            Save();
        }
    }

    /// <summary>GUID anónimo y persistente de esta instalación (telemetría); no identifica al usuario.</summary>
    public string GetOrCreateAnonymousId() {
        lock (_lock) {
            if (!string.IsNullOrWhiteSpace(_preferences.AnonymousId))
                return _preferences.AnonymousId!;
            _preferences.AnonymousId = Guid.NewGuid().ToString("N");
        }
        Save();
        return _preferences.AnonymousId!;
    }

    /// <summary>Opt-out de la telemetría anónima (default: activada).</summary>
    public bool TelemetryOptOut {
        get {
            lock (_lock) return _preferences.TelemetryOptOut;
        }
        set {
            lock (_lock) {
                if (_preferences.TelemetryOptOut == value)
                    return;
                _preferences.TelemetryOptOut = value;
            }
            Save();
        }
    }

    /// <summary>
    /// Idioma de la interfaz persistido ("es"/"en", issue #12); nulo = sin elección
    /// (el arranque lo detecta del sistema y lo persiste).
    /// </summary>
    public string? Language {
        get {
            lock (_lock) return _preferences.Language;
        }
        set {
            lock (_lock) {
                if (_preferences.Language == value)
                    return;
                _preferences.Language = value;
            }
            Save();
        }
    }

    /// <summary>
    /// Minutos de gracia entre la detección del fin de vuelo (apagado de motores en gate) y el aviso de
    /// finalización de la sesión (default 3). Se cuenta en tiempo real de pared, no en tiempo de simulación.
    /// </summary>
    public int RealisticCompletionGraceMinutes {
        get {
            lock (_lock) return _preferences.RealisticCompletionGraceMinutes ?? UserPreferences.DefaultRealisticCompletionGraceMinutes;
        }
        set {
            lock (_lock) {
                var clamped = Math.Max(0, value);
                if (_preferences.RealisticCompletionGraceMinutes == clamped)
                    return;
                _preferences.RealisticCompletionGraceMinutes = clamped;
            }
            Save();
        }
    }

    private UserPreferences Load() {
        try {
            if (!File.Exists(_filePath))
                return new UserPreferences();

            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<UserPreferences>(json);
            return loaded ?? new UserPreferences();
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudieron cargar las preferencias de usuario desde {Path}; se usan valores por defecto.", _filePath);
            return new UserPreferences();
        }
    }

    private void Save() {
        try {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            lock (_lock) {
                var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudieron guardar las preferencias de usuario en {Path}.", _filePath);
        }
    }
}

public class UserPreferences {
    public string? SimbriefUserId { get; set; }

    /// <summary>Identificador aleatorio persistente de la instalación (telemetría anónima).</summary>
    public string? AnonymousId { get; set; }

    /// <summary>True si el usuario desactivó el ping anónimo de uso.</summary>
    public bool TelemetryOptOut { get; set; }

    /// <summary>Idioma de la interfaz ("es"/"en", issue #12); nulo = sin elegir (primer arranque detecta del sistema).</summary>
    public string? Language { get; set; }

    /// <summary>Minutos de gracia para el aviso de fin de vuelo (issue #15); nulo → default de fábrica.</summary>
    public int? RealisticCompletionGraceMinutes { get; set; }

    /// <summary>Default de fábrica del gracia de fin de vuelo: 1 minutos.</summary>
    public const int DefaultRealisticCompletionGraceMinutes = 1;
}
