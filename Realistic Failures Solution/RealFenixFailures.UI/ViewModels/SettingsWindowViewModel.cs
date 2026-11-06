using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.Localization;
using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels.Base;
using System.Diagnostics;
using System.Reflection;
using RealFenixFailures.Infrastructure;

namespace RealFenixFailures.UI.ViewModels;

public class SettingsWindowViewModel : ObservableObject {

    public const string DefaultRepositoryUrl = SupportLinks.Repository;

    private readonly UserPreferencesService _preferences;
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<SettingsWindowViewModel> _logger;
    private readonly IRealisticSessionManager? _realisticSessionManager;
    private CancellationTokenSource? _saveDebounceCts;

    private string _simbriefUserId = string.Empty;
    private string _saveStatusText = string.Empty;
    private bool _isCheckingForUpdates;
    private string _updateStatusText = string.Empty;
    private UpdateCheckState _updateState = UpdateCheckState.Idle;
    private bool _hasNewUpdate;
    private string _newUpdateTag = string.Empty;
    private int _realisticCompletionGraceMinutes;

    public SettingsWindowViewModel(
        UserPreferencesService preferences,
        IUpdateChecker updateChecker,
        ILogger<SettingsWindowViewModel> logger,
        IRealisticSessionManager? realisticSessionManager = null) {
        _preferences = preferences;
        _updateChecker = updateChecker;
        _logger = logger;
        _realisticSessionManager = realisticSessionManager;

        _simbriefUserId = preferences.SimbriefUserId;
        _telemetryEnabled = !preferences.TelemetryOptOut;
        _realisticCompletionGraceMinutes = preferences.RealisticCompletionGraceMinutes;

        SetRealisticGraceCommand = new RelayCommand<string>(minutes => {
            if (int.TryParse(minutes, out var value))
                RealisticCompletionGraceMinutes = value;
        });

        AppVersion = GetDisplayVersion();

        OpenRepositoryCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
        OpenIssuesCommand = new RelayCommand(() => OpenUrl(IssuesUrl));
        OpenReleasesCommand = new RelayCommand(() => OpenUrl(ReleasesUrl));
        OpenLogsCommand = new RelayCommand(OpenLogs);
        OpenDonationCommand = new RelayCommand(() => OpenUrl(SupportLinks.Donation));
        CheckForUpdatesCommand = new RelayCommand(() => _ = CheckForUpdatesAsync(), () => !IsCheckingForUpdates);
    }

    public string AppVersion { get; }

    public string RepositoryUrl => DefaultRepositoryUrl;
    public string IssuesUrl => DefaultRepositoryUrl + "/issues";
    public string ReleasesUrl => DefaultRepositoryUrl + "/releases";

    public RelayCommand OpenRepositoryCommand { get; }
    public RelayCommand OpenIssuesCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand OpenDonationCommand { get; }
    public string LogsDirectory => AppPaths.LogsDirectory;
    public string DonationUrl => SupportLinks.Donation;
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

    /// <summary>
    /// Minutos de gracia para el aviso de fin de vuelo del modo Realista (issue #15): tiempo real que pasa
    /// entre el apagado de motores en gate y el aviso para finalizar la sesión.
    /// </summary>
    public int RealisticCompletionGraceMinutes {
        get => _realisticCompletionGraceMinutes;
        set {
            if (SetProperty(ref _realisticCompletionGraceMinutes, Math.Max(0, value))) {
                SaveStatusText = string.Empty;
                ScheduleSave();
            }
        }
    }

    /// <summary>Telemetría anónima activada (opt-out invertido en preferencias al persistir).</summary>
    public bool TelemetryEnabled {
        get => _telemetryEnabled;
        set {
            if (SetProperty(ref _telemetryEnabled, value)) {
                SaveStatusText = string.Empty;
                ScheduleSave();
            }
        }
    }
    private bool _telemetryEnabled;

    /// <summary>
    /// Presets del aviso de fin de vuelo (pestaña Modo Realista): fijan los minutos de gracia con un
    /// clic; escriben la propiedad, así que van por el mismo camino de persistencia y live-update.
    /// </summary>
    public RelayCommand<string> SetRealisticGraceCommand { get; }

    /// <summary>Idiomas disponibles para el selector (nombres invariables: English / Español).</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = LanguageOption.All;

    /// <summary>
    /// Opción del selector de idioma bindeada por <c>SelectedItem</c> (issue #12). A diferencia de
    /// SelectedValue+SelectedValuePath, si el valor actual del getter coincide con un ítem WPF no
    /// empuja null/default al cargar el panel (el bug que mostraba un idioma distinto del real).
    /// Al cambiarlo se aplica en vivo vía Loc (refresca todos los bindings localizados) y se
    /// persiste en userprefs.json de inmediato. La detección automática solo corre en el primer
    /// arranque.
    /// </summary>
    public LanguageOption? SelectedLanguageOption {
        get => LanguageOptions.FirstOrDefault(o => o.Language == Loc.Instance.CurrentLanguage);
        set {
            if (value is null || value.Language == Loc.Instance.CurrentLanguage)
                return;
            Loc.Instance.CurrentLanguage = value.Language;
            try {
                _preferences.Language = LanguageResolver.ToCode(value.Language);
                SaveStatusText = string.Empty;
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "No se pudo persistir el idioma seleccionado.");
                SaveStatusText = Loc.T("Set_SaveStatusFailed");
            }
            OnPropertyChanged();
        }
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
                SaveStatusText = Loc.T("Set_SaveStatusSaved");
        }
        catch (TaskCanceledException) {
        }
    }

    private void Persist() {
        try {
            _preferences.SimbriefUserId = SimbriefUserId?.Trim() ?? string.Empty;
            _preferences.TelemetryOptOut = !TelemetryEnabled;
            _preferences.RealisticCompletionGraceMinutes = RealisticCompletionGraceMinutes;
            // Actualización en vivo: si hay sesión activa, el gracia nuevo aplica al próximo countdown.
            if (_realisticSessionManager is not null)
                _realisticSessionManager.FlightCompletionGraceDelay = TimeSpan.FromMinutes(_preferences.RealisticCompletionGraceMinutes);
            _logger.LogDebug("Preferencias de usuario guardadas.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "No se pudieron guardar las preferencias de usuario.");
            SaveStatusText = Loc.T("Set_SaveStatusFailed");
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

    /// <summary>Chequeo de actualizaciones: manual (pestaña) y automático (arranque de la app).</summary>
    public async Task CheckForUpdatesAsync() {
        if (IsCheckingForUpdates)
            return;
        IsCheckingForUpdates = true;
        HasNewUpdate = false;
        UpdateState = UpdateCheckState.Checking;
        UpdateStatusText = Loc.T("Set_UpdateChecking");

        try {
            var result = await _updateChecker.CheckForUpdatesAsync(CancellationToken.None);

            if (result.IsError) {
                UpdateState = UpdateCheckState.Error;
                UpdateStatusText = result.ErrorMessage!;
                return;
            }

            if (!result.HasReleases) {
                UpdateState = UpdateCheckState.NoReleases;
                UpdateStatusText = Loc.T("Set_UpdateNone");
                return;
            }

            var latest = result.LatestRelease!;
            if (IsNewerThanInstalled(AppVersion, latest.TagName)) {
                NewUpdateTag = latest.TagName;
                var tipo = latest.IsPrerelease ? Loc.T("Set_PrereleaseWord") : Loc.T("Set_StableWord");
                UpdateStatusText = Loc.T("Set_UpdateNewer", tipo, latest.TagName) +
                    (latest.PublishedAt is { } p ? Loc.T("Set_UpdatePublishedOn", p.ToString("dd/MM/yyyy")) : string.Empty) + ".";
                UpdateState = UpdateCheckState.NewVersionAvailable;
                HasNewUpdate = true;
                _logger.LogInformation("Actualización disponible: {Tag}.", latest.TagName);
            }
            else {
                UpdateState = UpdateCheckState.UpToDate;
                UpdateStatusText = Loc.T("Set_UpdateLatest");
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Fallo inesperado en el chequeo de actualizaciones.");
            UpdateState = UpdateCheckState.Error;
            UpdateStatusText = Loc.T("Set_UpdateError");
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

    private void OpenLogs() {
        try {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo(AppPaths.LogsDirectory) { UseShellExecute = true });
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo abrir la carpeta de logs {Path}", AppPaths.LogsDirectory);
        }
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
