using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels.Base;
using System.Diagnostics;
using System.Reflection;

namespace RealFenixFailures.UI.ViewModels;

public class SettingsWindowViewModel : ObservableObject {

    public const string DefaultRepositoryUrl = "https://github.com/mathi0101/Fenix-Failures-Dispatcher";
    public const string SimbriefApiDocsUrl = "https://www.simbrief.com/api/";

    private readonly UserPreferencesService _preferences;
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<SettingsWindowViewModel> _logger;
    private CancellationTokenSource? _saveDebounceCts;

    private string _simbriefUserId = string.Empty;
    private string _saveStatusText = string.Empty;
    private bool _isCheckingForUpdates;
    private string _updateStatusText = string.Empty;
    private UpdateCheckState _updateState = UpdateCheckState.Idle;
    private bool _hasNewUpdate;
    private string _newUpdateTag = string.Empty;

    public SettingsWindowViewModel(
        UserPreferencesService preferences,
        IUpdateChecker updateChecker,
        ILogger<SettingsWindowViewModel> logger) {
        _preferences = preferences;
        _updateChecker = updateChecker;
        _logger = logger;

        _simbriefUserId = preferences.SimbriefUserId;

        AppVersion = GetDisplayVersion();

        OpenRepositoryCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
        OpenIssuesCommand = new RelayCommand(() => OpenUrl(IssuesUrl));
        OpenReleasesCommand = new RelayCommand(() => OpenUrl(ReleasesUrl));
        OpenSimbriefDocsCommand = new RelayCommand(() => OpenUrl(SimbriefApiDocsUrl));
        CheckForUpdatesCommand = new RelayCommand(() => _ = CheckForUpdatesAsync(), () => !IsCheckingForUpdates);
    }

    public string AppVersion { get; }

    public string RepositoryUrl => DefaultRepositoryUrl;
    public string IssuesUrl => DefaultRepositoryUrl + "/issues";
    public string ReleasesUrl => DefaultRepositoryUrl + "/releases";

    public RelayCommand OpenRepositoryCommand { get; }
    public RelayCommand OpenIssuesCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public RelayCommand OpenSimbriefDocsCommand { get; }
    public RelayCommand CheckForUpdatesCommand { get; }

    /// <summary>True mientras el chequeo de actualizaciones está consultando GitHub.</summary>
    public bool IsCheckingForUpdates {
        get => _isCheckingForUpdates;
        private set {
            if (SetProperty(ref _isCheckingForUpdates, value))
                CheckForUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Texto de estado del chequeo (resultado o explicación del error).</summary>
    public string UpdateStatusText {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    /// <summary>Estado del chequeo; la UI le asigna color con DataTriggers.</summary>
    public UpdateCheckState UpdateState {
        get => _updateState;
        private set => SetProperty(ref _updateState, value);
    }

    /// <summary>True si existe una release más nueva que la instalada.</summary>
    public bool HasNewUpdate {
        get => _hasNewUpdate;
        private set => SetProperty(ref _hasNewUpdate, value);
    }

    /// <summary>Tag de la nueva versión disponible (p. ej. v0.1.0-pre.1).</summary>
    public string NewUpdateTag {
        get => _newUpdateTag;
        private set => SetProperty(ref _newUpdateTag, value);
    }

    /// <summary>ID de usuario de SimBrief (alias). Se persiste con debounce al escribir.</summary>
    public string SimbriefUserId {
        get => _simbriefUserId;
        set {
            if (SetProperty(ref _simbriefUserId, value)) {
                SaveStatusText = string.Empty;
                ScheduleSave();
            }
        }
    }

    /// <summary>Feedback visible: "✓ Guardado" tras persistir el valor.</summary>
    public string SaveStatusText {
        get => _saveStatusText;
        private set => SetProperty(ref _saveStatusText, value);
    }

    public bool HasSimbriefId => !string.IsNullOrWhiteSpace(SimbriefUserId);

    /// <summary>Persiste las preferencias de inmediato (cierre de ventana).</summary>
    public void SaveNow() {
        _saveDebounceCts?.Cancel();
        Persist();
    }

    private void ScheduleSave() {
        _saveDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _saveDebounceCts = cts;
        _ = DelayAndPersistAsync(cts);
    }

    private async Task DelayAndPersistAsync(CancellationTokenSource cts) {
        try {
            await Task.Delay(400, cts.Token);
            Persist();
            if (!cts.IsCancellationRequested && cts == _saveDebounceCts)
                SaveStatusText = "✓ Guardado";
        }
        catch (TaskCanceledException) {
        }
    }

    private void Persist() {
        try {
            _preferences.SimbriefUserId = SimbriefUserId?.Trim() ?? string.Empty;
            _logger.LogDebug("Preferencias de usuario guardadas (SimbriefUserId establecido).");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "No se pudieron guardar las preferencias de usuario.");
            SaveStatusText = "⚠ No se pudo guardar";
        }
    }

    /// <summary>Versión visible: InformationalVersion (conserva el sufijo, p. ej. 0.1.0-dev.0).</summary>
    private static string GetDisplayVersion() {
        try {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrEmpty(informational)) {
                // Los builds deterministas pueden anexar "+sha"; se muestra solo el semver.
                var plus = informational.IndexOf('+');
                return plus > 0 ? informational[..plus] : informational;
            }
        }
        catch {
            // fall-through al nombre del ensamblado
        }
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private async Task CheckForUpdatesAsync() {
        if (IsCheckingForUpdates)
            return;

        IsCheckingForUpdates = true;
        HasNewUpdate = false;
        UpdateState = UpdateCheckState.Checking;
        UpdateStatusText = "Consultando GitHub Releases...";

        try {
            var result = await _updateChecker.CheckForUpdatesAsync(CancellationToken.None);

            if (result.IsError) {
                UpdateState = UpdateCheckState.Error;
                UpdateStatusText = result.ErrorMessage!;
                return;
            }

            if (!result.HasReleases) {
                UpdateState = UpdateCheckState.NoReleases;
                UpdateStatusText = "Todavía no hay releases publicadas. ¡Pronto habrá novedades!";
                return;
            }

            var latest = result.LatestRelease!;
            if (IsNewerThanInstalled(AppVersion, latest.TagName)) {
                NewUpdateTag = latest.TagName;
                var tipo = latest.IsPrerelease ? "pre-release" : "versión estable";
                UpdateStatusText = $"Hay una {tipo} más reciente: {latest.TagName}" +
                    (latest.PublishedAt is { } p ? $" (publicada el {p:dd/MM/yyyy})" : string.Empty) + ".";
                UpdateState = UpdateCheckState.NewVersionAvailable;
                HasNewUpdate = true;
                _logger.LogInformation("Actualización disponible: {Tag}.", latest.TagName);
            }
            else {
                UpdateState = UpdateCheckState.UpToDate;
                UpdateStatusText = "Estás usando la versión más reciente publicada.";
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Fallo inesperado en el chequeo de actualizaciones.");
            UpdateState = UpdateCheckState.Error;
            UpdateStatusText = "Ocurrió un error inesperado al buscar actualizaciones.";
        }
        finally {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>
    /// ¿El tag publicado es más nuevo que la versión instalada? Comparación SemVer
    /// simple; ante el mismo número principal, una versión de desarrollo (-dev)
    /// queda obsoleta cuando se publica un release (aunque sea pre-release).
    /// </summary>
    private static bool IsNewerThanInstalled(string installed, string releaseTag) {
        var current = TryParseSemVer(installed);
        var candidate = TryParseSemVer(releaseTag);
        if (current is null || candidate is null)
            return !string.Equals(installed.TrimStart('v'), releaseTag.TrimStart('v'), StringComparison.OrdinalIgnoreCase);

        if (candidate.Value.Major != current.Value.Major) return candidate.Value.Major > current.Value.Major;
        if (candidate.Value.Minor != current.Value.Minor) return candidate.Value.Minor > current.Value.Minor;
        if (candidate.Value.Patch != current.Value.Patch) return candidate.Value.Patch > current.Value.Patch;

        return current.Value.Prerelease?.StartsWith("dev", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>Parseo SemVer mínimo de "vX.Y.Z-prefijo" (tolera partes faltantes).</summary>
    private static (int Major, int Minor, int Patch, string? Prerelease)? TryParseSemVer(string raw) {
        var s = raw.Trim().TrimStart('v');
        var dash = s.IndexOf('-');
        var core = dash >= 0 ? s[..dash] : s;
        var pre = dash >= 0 ? s[(dash + 1)..] : null;
        var parts = core.Split('.');
        if (parts.Length < 1 || parts.Length > 3 || !int.TryParse(parts[0], out var major))
            return null;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return (major, minor, patch, pre);
    }

    private void OpenUrl(string url) {
        try {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo abrir la URL {Url}", url);
        }
    }
}
