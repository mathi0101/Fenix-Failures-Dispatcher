using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SettingsWindowViewModel> _logger;
    private CancellationTokenSource? _saveDebounceCts;

    private string _simbriefUserId = string.Empty;
    private string _saveStatusText = string.Empty;

    public SettingsWindowViewModel(
        UserPreferencesService preferences,
        ILogger<SettingsWindowViewModel> logger) {
        _preferences = preferences;
        _logger = logger;

        _simbriefUserId = preferences.SimbriefUserId;

        AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        OpenRepositoryCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
        OpenIssuesCommand = new RelayCommand(() => OpenUrl(IssuesUrl));
        OpenReleasesCommand = new RelayCommand(() => OpenUrl(ReleasesUrl));
        OpenSimbriefDocsCommand = new RelayCommand(() => OpenUrl(SimbriefApiDocsUrl));
    }

    public string AppVersion { get; }

    public string RepositoryUrl => DefaultRepositoryUrl;
    public string IssuesUrl => DefaultRepositoryUrl + "/issues";
    public string ReleasesUrl => DefaultRepositoryUrl + "/releases";

    public RelayCommand OpenRepositoryCommand { get; }
    public RelayCommand OpenIssuesCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public RelayCommand OpenSimbriefDocsCommand { get; }

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

    private void OpenUrl(string url) {
        try {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo abrir la URL {Url}", url);
        }
    }
}
