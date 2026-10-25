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

    /// <summary>Fecha (UTC) del último ping de telemetría enviado; dedup diario.</summary>
    public DateTime? LastTelemetryDate {
        get {
            lock (_lock) return _preferences.LastTelemetryDate;
        }
        set {
            lock (_lock) {
                if (_preferences.LastTelemetryDate == value)
                    return;
                _preferences.LastTelemetryDate = value;
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

    /// <summary>Día (UTC) del último ping enviado; evita más de uno por día.</summary>
    public DateTime? LastTelemetryDate { get; set; }
}
