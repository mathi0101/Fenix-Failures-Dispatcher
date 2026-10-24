using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.ViewModels.Base;
using RealFenixFailures.UI.ViewModels.Extra;
using RealFenixFailures.UI.ViewModels.Realistic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace RealFenixFailures.UI.ViewModels;

public class MainWindowViewModel : ObservableObject, IDisposable {

    #region Fields

    private readonly int _maxLogEntries;

    private readonly IEngineOrchestrator _orchestrator;
    private readonly IPresetService _presetService;
    private readonly IFlightHistoryService _flightHistoryService;
    private readonly IFlightSessionService _flightSessionService;
    private readonly IFlightEventDetector _flightEventDetector;
    private readonly ILogger<MainWindowViewModel> _logger;

    // ViewModel del flujo de 4 pasos del Modo Realista (Paso 1..4).
    public RealisticModeViewModel Realistic { get; }

    // ViewModel del panel de configuración (overlay centrado sobre el contenido).
    public SettingsWindowViewModel Settings { get; }

    // Connection state
    private ComplexFlightPhaseEnum currentComplexFlightPhase = ComplexFlightPhaseEnum.Unknown;
    private bool isSimConnected = false;
    private bool hasAircraftData = false;
    private bool isFenixConnected = false;

    // Panel selection (qué panel muestra la UI para configurar)
    private bool _isRealisticModeSelected;
    private bool _isTrainingModeSelected = true;
    private bool _isCustomModeSelected;

    // Overlay de configuración
    private bool _isSettingsOpen;

    // Training
    private TrainingScenarioViewModel? _selectedTrainingScenario;

    // Custom
    private double _globalProbability = 0.2;
    private int _checkIntervalSeconds = 5;
    #endregion

    #region Constructor

    public MainWindowViewModel(
        IEngineOrchestrator orchestrator,
        IPresetService presetService,
        IFlightHistoryService flightHistoryService,
        IFlightSessionService flightSessionService,
        RealisticModeViewModel realisticModeViewModel,
        IFlightEventDetector flightEventDetector,
        SettingsWindowViewModel settings,
        IOptions<FailureEngineSettings> failureEngineSettings,
        ILogger<MainWindowViewModel> logger) {
        _orchestrator = orchestrator;
        _presetService = presetService;
        _flightHistoryService = flightHistoryService;
        _flightSessionService = flightSessionService;
        _flightEventDetector = flightEventDetector;
        Realistic = realisticModeViewModel;
        Settings = settings;
        _maxLogEntries = failureEngineSettings.Value.MaxLogEntries;
        _logger = logger;

        // Suscribirse a los eventos del orquestador
        _orchestrator.PropertyChanged += Orchestrator_PropertyChanged;
        _flightEventDetector.FlightEventDetected += FlightEventDetector_FlightEventDetected;

        TrainingScenarios = new ObservableCollection<TrainingScenarioViewModel>();
        CustomPresets = new ObservableCollection<CustomPresetViewModel>();
        LogEntries = new ObservableCollection<string>();

        // Commands with canExecute where it makes sense
        ApplySettingsCommand = new RelayCommand(ApplySettings);

        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);

        SelectTrainingScenarioCommand = new RelayCommand<TrainingScenarioViewModel>(SelectTrainingScenario);
        StartTrainingScenarioCommand = new RelayCommand(async () => await StartOrStopTrainingScenarioAsync(), () => CanStartTraining);

        CreateCustomPresetCommand = new RelayCommand(async () => await CreateCustomPresetAsync());
        ActivateCustomPresetCommand = new RelayCommand<CustomPresetViewModel>(async p => await ActivateCustomPresetAsync(p),
            p => CanActivateCustomPreset);
        DeleteCustomPresetCommand = new RelayCommand<CustomPresetViewModel>(async p => await DeleteCustomPresetAsync(p),
            p => true);

        // initial update of command states (in case initial flags differ)
        UpdateCommandStates();
    }
    #endregion

    #region Properties

    #region Commands

    // Commands
    public RelayCommand ApplySettingsCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CloseSettingsCommand { get; }
    public RelayCommand<TrainingScenarioViewModel> SelectTrainingScenarioCommand { get; }
    public RelayCommand StartTrainingScenarioCommand { get; }
    public RelayCommand CreateCustomPresetCommand { get; }
    public RelayCommand<CustomPresetViewModel> ActivateCustomPresetCommand { get; }
    public RelayCommand<CustomPresetViewModel> DeleteCustomPresetCommand { get; }

    #endregion

    #region Observed Properties 

    // Collections
    public ObservableCollection<TrainingScenarioViewModel> TrainingScenarios { get; }
    public ObservableCollection<CustomPresetViewModel> CustomPresets { get; }
    public ObservableCollection<string> LogEntries { get; }

    // Settings overlay (centrado sobre el contenido; el fondo queda opaco y bloqueado)
    public bool IsSettingsOpen {
        get => _isSettingsOpen;
        set {
            if (SetProperty(ref _isSettingsOpen, value)) {
                if (!value) {
                    // Al cerrar el overlay se persisten las preferencias pendientes.
                    Settings.SaveNow();
                }
            }
        }
    }

    // Connection status

    public bool IsSimConnected {
        get => isSimConnected;
        set {
            if (isSimConnected != value) {
                isSimConnected = value;
                OnPropertyChanged(nameof(IsSimConnected));
                OnPropertyChanged(nameof(SimConnectDotColor));
                OnPropertyChanged(nameof(SimConnectStatus));
                OnPropertyChanged(nameof(SimConnectStatusBrush));
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
                OnPropertyChanged(nameof(CanStartEngine));
                OnPropertyChanged(nameof(ShowConnectionWarning));
            }
        }
    }

    public bool HasAircraftData {
        get => hasAircraftData;
        private set {
            if (hasAircraftData != value) {
                hasAircraftData = value;
                OnPropertyChanged(nameof(HasAircraftData));
                OnPropertyChanged(nameof(SimConnectDotColor));
                OnPropertyChanged(nameof(SimConnectStatus));
                OnPropertyChanged(nameof(SimConnectStatusBrush));
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
                OnPropertyChanged(nameof(CanStartEngine));
                OnPropertyChanged(nameof(ShowConnectionWarning));
            }
        }
    }

    public bool IsFenixConnected {
        get => isFenixConnected;
        set {
            if (isFenixConnected != value) {
                isFenixConnected = value;
                OnPropertyChanged(nameof(IsFenixConnected));
                OnPropertyChanged(nameof(FenixDotColor));
                OnPropertyChanged(nameof(FenixStatus));
                OnPropertyChanged(nameof(CanStartEngine));
                OnPropertyChanged(nameof(ShowConnectionWarning));
            }
        }
    }

    public string SimConnectStatus {
        get {
            if (!isSimConnected) return "Disconnected";
            return hasAircraftData ? "Connected · Aircraft data" : "Connected · No aircraft data";
        }
    }
    public string FenixStatus => isFenixConnected ? "Connected" : "Disconnected";


    public ComplexFlightPhaseEnum CurrentComplexFlightPhase {
        get => currentComplexFlightPhase;
        private set {
            if (SetProperty(ref currentComplexFlightPhase, value))
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
        }
    }

    public string CurrentFlightPhaseDisplay {
        get {
            if (!isSimConnected) return "Disconnected";
            if (!hasAircraftData) return "No aircraft data";
            return CurrentComplexFlightPhase switch {
                ComplexFlightPhaseEnum.OnGate => "Cold and Dark",
                ComplexFlightPhaseEnum.Taxi => "Taxing",
                ComplexFlightPhaseEnum.Takeoff => "Takeoff",
                ComplexFlightPhaseEnum.Climb => "Climbing",
                ComplexFlightPhaseEnum.Cruise => "Cruising",
                ComplexFlightPhaseEnum.Descent => "Descending",
                ComplexFlightPhaseEnum.Approach => "Approach",
                ComplexFlightPhaseEnum.Landing => "Landing",
                ComplexFlightPhaseEnum.Parked => "Parked",
                _ => "Unknown"
            };
        }
    }

    // Dot colors (binding antiguo en XAML)
    public Color SimConnectDotColor {
        get {
            if (!isSimConnected) return Color.FromRgb(100, 116, 139);
            return hasAircraftData ? Color.FromRgb(34, 197, 94) : Color.FromRgb(245, 158, 11);
        }
    }
    public Color FenixDotColor => isFenixConnected ? Color.FromRgb(34, 197, 94) : Color.FromRgb(100, 116, 139);

    // Status brushes (puede usarse directamente en XAML)
    public Brush SimConnectStatusBrush {
        get {
            if (!isSimConnected) return new SolidColorBrush(Color.FromRgb(100, 116, 139));
            return hasAircraftData
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                : new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }

    public Brush FenixStatusBrush => isFenixConnected
        ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
        : new SolidColorBrush(Color.FromRgb(100, 116, 139));

    // Si el orchestrator indica que hay conexión en ambos servicios (puede arrancar "engine" realista)
    public bool CanStartEngine => isSimConnected && hasAircraftData && isFenixConnected;

    public bool ShowConnectionWarning => !CanStartEngine && ActiveMode == UserAppMode.Realistic;

    public bool IsEngineActive => _orchestrator.IsEngineActive;

    // Control local del modo activo (se actualiza al activar/desactivar via orchestrator)
    private UserAppMode ActiveMode => _orchestrator.CurrentMode;

    public bool IsAnyModeActive => ActiveMode != UserAppMode.None;
    public bool CanSwitchModes => ActiveMode == UserAppMode.None;

    public bool IsTrainingActive => ActiveMode == UserAppMode.Training;
    public bool IsCustomActive => ActiveMode == UserAppMode.Custom;
    public bool IsRealisticActive => ActiveMode == UserAppMode.Realistic;

    // Derived properties for button availability (used by canExecute)
    public bool CanStartTraining => SelectedTrainingScenario is not null && (!IsAnyModeActive || IsTrainingActive) && IsFenixConnected;
    public bool CanActivateCustomPreset => (!IsAnyModeActive || IsCustomActive) && IsFenixConnected;
    public bool CanToggleRealistic => (!IsAnyModeActive || IsRealisticActive) && CanStartEngine;

    public string TrainingStartText => IsTrainingActive ? "⏹ DETENER ESCENARIO" : "▶ INICIAR ESCENARIO";
    public string RealisticToggleText => IsRealisticActive ? "⏹ DETENER MODO REALISTA" : "▶ ACTIVAR MODO REALISTA";
    public string CustomModeButtonText => IsCustomActive ? "⏹ DETENER PRESET" : "▶ ACTIVAR PRESET";


    // Compatibilidad para bindings antiguos
    public string EngineToggleText {
        get {
            if (IsRealisticActive) return RealisticToggleText;
            if (IsTrainingActive) return TrainingStartText;
            return CustomModeButtonText;
        }
    }

    // Panel selection (no cambia la lógica del orchestrator)
    public bool IsTrainingModeSelected {
        get => _isTrainingModeSelected;
        set {
            if (!CanSwitchModes && value && ActiveMode != UserAppMode.Training) return;
            SetProperty(ref _isTrainingModeSelected, value);
        }
    }

    public bool IsCustomModeSelected {
        get => _isCustomModeSelected;
        set {
            if (!CanSwitchModes && value && ActiveMode != UserAppMode.Custom) return;
            SetProperty(ref _isCustomModeSelected, value);
        }
    }

    public bool IsRealisticModeSelected {
        get => _isRealisticModeSelected;
        set {
            if (!CanSwitchModes && value && ActiveMode != UserAppMode.Realistic) return;
            SetProperty(ref _isRealisticModeSelected, value);
        }
    }

    // Training
    public TrainingScenarioViewModel? SelectedTrainingScenario {
        get => _selectedTrainingScenario;
        set {
            if (SetProperty(ref _selectedTrainingScenario, value)) {
                OnPropertyChanged(nameof(HasSelectedTrainingScenario));
                OnPropertyChanged(nameof(CanStartTraining));
                UpdateCommandStates();
            }
        }
    }

    public bool HasSelectedTrainingScenario => SelectedTrainingScenario is not null;

    // Custom settings
    public double GlobalProbability {
        get => _globalProbability;
        set => SetProperty(ref _globalProbability, value);
    }

    public int CheckIntervalSeconds {
        get => _checkIntervalSeconds;
        set => SetProperty(ref _checkIntervalSeconds, value);
    }

    #endregion

    #endregion

    #region Initialize

    // Initialization
    public async Task InitializeAsync() {
        await LoadTrainingScenarios();
        await Realistic.InitializeAsync();
        // Estado inicial de conexión; el timer automático del orquestador
        // se encarga del autorefresco en todo momento.
        await _orchestrator.UpdateConnection(CancellationToken.None);
        await LoadCustomPresetsAsync();
        await _orchestrator.StartAutomaticTimerAsync(CancellationToken.None);
    }

    #endregion

    #region Recuperación de sesiones sin finalizar (diseño objetivo §6/§13)

    /// <summary>Sesiones sin finalizar detectadas al arrancar (sesión huérfana: la app se cerró con el modo activo).</summary>
    public ObservableCollection<UnfinishedSessionItemViewModel> UnfinishedSessions { get; } = new();

    /// <summary>Resultado de la última sesión huérfana finalizada desde el diálogo de recuperación (para el modal de resultados).</summary>
    public SessionResultDto? LastFinalizedResult { get; private set; }

    /// <summary>El diálogo de recuperación se cierra cuando ya no queda nada por resolver.</summary>
    public event EventHandler? UnfinishedSessionsResolved;

    private bool _isRecoveryBusy;

    public async Task LoadUnfinishedSessionsAsync() {
        try {
            var sessions = await _flightSessionService.GetUnfinishedSessionsAsync(CancellationToken.None);
            UnfinishedSessions.Clear();
            foreach (var session in sessions)
                UnfinishedSessions.Add(new UnfinishedSessionItemViewModel(this, session));
        } catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudieron cargar las sesiones sin finalizar para la recuperación");
        }
    }

    public async Task FinalizeUnfinishedSessionAsync(int sessionId) {
        if (_isRecoveryBusy) return;
        _isRecoveryBusy = true;
        try {
            LastFinalizedResult = await _flightSessionService.FinalizeUnfinishedSessionAsync(sessionId, CancellationToken.None);
            RemoveUnfinishedSession(sessionId);
            _logger.LogInformation("Sesión huérfana {SessionId} finalizada desde la recuperación", sessionId);
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo finalizar la sesión huérfana {SessionId}", sessionId);
        } finally {
            _isRecoveryBusy = false;
        }
    }

    public async Task DiscardUnfinishedSessionAsync(int sessionId) {
        if (_isRecoveryBusy) return;
        _isRecoveryBusy = true;
        try {
            await _flightSessionService.DiscardSessionAsync(sessionId, DateTime.UtcNow, CancellationToken.None);
            RemoveUnfinishedSession(sessionId);
            _logger.LogInformation("Sesión huérfana {SessionId} descartada desde la recuperación", sessionId);
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo descartar la sesión huérfana {SessionId}", sessionId);
        } finally {
            _isRecoveryBusy = false;
        }
    }

    /// <summary>Refresca el panel Realista después del diálogo de recuperación y abre el modal de resultados si se finalizó una huérfana.</summary>
    public async Task RefreshAfterRecoveryAsync() {
        await Realistic.InitializeAsync();
        if (LastFinalizedResult is not null)
            await Realistic.ShowSessionResultByIdAsync(LastFinalizedResult.SessionId);
    }

    private void RemoveUnfinishedSession(int sessionId) {
        var item = UnfinishedSessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (item is not null) UnfinishedSessions.Remove(item);
        if (UnfinishedSessions.Count == 0) UnfinishedSessionsResolved?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Orchestator Property Changed Event

    // Event handler para los cambios en el orquestador
    private async void Orchestrator_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        // Actualizar la UI cuando cambian las propiedades relevantes del orquestador
        if (e.PropertyName == nameof(IEngineOrchestrator.IsEngineActive)) {
            OnPropertyChanged(nameof(IsEngineActive));
            OnPropertyChanged(nameof(CanStartEngine));
            OnPropertyChanged(nameof(ShowConnectionWarning));
            OnPropertyChanged(nameof(EngineToggleText));
            UpdateCommandStates();
        }
        if (e.PropertyName == nameof(IEngineOrchestrator.CurrentMode)) {
            OnPropertyChanged(nameof(ActiveMode));
        }
        if (e.PropertyName == nameof(IEngineOrchestrator.ConnectionStatus)) {
            var status = _orchestrator.ConnectionStatus;
            IsSimConnected = status.IsSimConnectConnected;
            IsFenixConnected = status.IsFenixConnected;
            HasAircraftData = status.HasAircraftData;

            OnPropertyChanged(nameof(SimConnectDotColor));
            OnPropertyChanged(nameof(FenixDotColor));
            OnPropertyChanged(nameof(SimConnectStatusBrush));
            OnPropertyChanged(nameof(FenixStatusBrush));

            OnPropertyChanged(nameof(CanStartEngine));
        }
        if (e.PropertyName == nameof(IEngineOrchestrator.CurrentFlightPhase)) {
            CurrentComplexFlightPhase = _orchestrator.CurrentFlightPhase;
        }
    }

    private void FlightEventDetector_FlightEventDetected(object? sender, FlightEventDto e) {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            LogEntries.Add($"{e.TimestampUtc.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)} UTC  {e.Message}");
            while (LogEntries.Count > _maxLogEntries) {
                LogEntries.RemoveAt(0);
            }
            CurrentComplexFlightPhase = _flightEventDetector.CurrentPhase;
        });
    }

    #endregion

    // --- Private helpers and commands that delegate to orchestrator (no engine logic here) ---

    #region Private


    private async Task LoadTrainingScenarios() {
        TrainingScenarios.Clear();
        var presets = await _presetService.GetTrainingPresetsAsync(CancellationToken.None);
        if (presets == null || presets.Count == 0) return;
        foreach (var p in presets.OrderBy(x => x.Difficulty).ThenBy(x => x.FlightPhase)) {
            TrainingScenarios.Add(new TrainingScenarioViewModel {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                TriggerDescription = p.TriggerDescription,
                Phase = p.FlightPhase.ToString(),
                Difficulty = p.Difficulty.ToString(),
            });
        }
    }

    private void SelectTrainingScenario(TrainingScenarioViewModel? scenario) {
        foreach (var s in TrainingScenarios) s.IsSelected = false;
        if (scenario is not null) scenario.IsSelected = true;
        SelectedTrainingScenario = scenario;
    }

    private async Task StartOrStopTrainingScenarioAsync() {
        if (SelectedTrainingScenario is null) return;

        if (ActiveMode == UserAppMode.Training) {
            // Stop training scenario via orchestrator
            await _orchestrator.StopCurrentModeAsync(CancellationToken.None);
            _logger.LogInformation("Training scenario stopped: {Name}", SelectedTrainingScenario.Name);
            return;
        }

        if (IsAnyModeActive) return;

        // Activate the preset in orchestrator for training mode
        await _orchestrator.StartTrainingPresetAsync(SelectedTrainingScenario.Id, CancellationToken.None);
        _logger.LogInformation("Training scenario started: {Name}", SelectedTrainingScenario.Name);

        // Refresh UI state from orchestrator
        UpdateCommandStates();
    }

    private async Task ActivateCustomPresetAsync(CustomPresetViewModel? preset) {
        if (preset is null) return;

        if (ActiveMode == UserAppMode.Custom) {
            await _orchestrator.StopCurrentModeAsync(CancellationToken.None);
            _logger.LogInformation("Custom preset stopped: {Name}", preset.Name);
            return;
        }

        if (IsAnyModeActive) return;

        // TODO: En modo custom necesitamos decidir si activar inmediatamente o solo armar
        // Por ahora asumimos que activamos inmediatamente
        await _orchestrator.StartCustomModeAsync(preset.Id, true, CancellationToken.None);
        _logger.LogInformation("Custom preset triggered: {Name}", preset.Name);

        UpdateCommandStates();
    }

    private async Task DeleteCustomPresetAsync(CustomPresetViewModel? preset) {
        if (preset is null) return;
        await _presetService.DeletePresetAsync(preset.Id, CancellationToken.None);
        CustomPresets.Remove(preset);
    }


    private void ApplySettings() {
        GlobalProbability = Math.Clamp(GlobalProbability, 0, 1);
        CheckIntervalSeconds = Math.Max(1, CheckIntervalSeconds);

        // Si querés que el orchestrator actualice el intervalo ahora, descomenta:
        // _orchestrator.SetPollingInterval(TimeSpan.FromSeconds(CheckIntervalSeconds));
    }

    private async Task CreateCustomPresetAsync() {
        var newPreset = await _presetService.CreateEmptyCustomPresetAsync(CancellationToken.None); // implement if missing
        CustomPresets.Add(new CustomPresetViewModel {
            Id = newPreset.Id,
            Name = newPreset.Name,
            FailureCount = newPreset.PresetFailureDefinitions.Count,
        });
    }

    private async Task LoadCustomPresetsAsync() {
        var presets = await _presetService.GetCustomPresetsAsync(CancellationToken.None);
        CustomPresets.Clear();
        foreach (var p in presets) {
            CustomPresets.Add(new CustomPresetViewModel {
                Id = p.Id,
                Name = p.Name,
                FailureCount = p.PresetFailureDefinitions.Count
            });
        }
        UpdateCommandStates();
    }
    // ----- Helpers -----
    private void UpdateCommandStates() {
        // Asumo que tu RelayCommand tiene RaiseCanExecuteChanged(); si tu implementación tiene otro nombre, ajustalo.
        StartTrainingScenarioCommand?.RaiseCanExecuteChanged();
        ActivateCustomPresetCommand?.RaiseCanExecuteChanged();
        CreateCustomPresetCommand?.RaiseCanExecuteChanged();
        DeleteCustomPresetCommand?.RaiseCanExecuteChanged();
        ApplySettingsCommand?.RaiseCanExecuteChanged();
    }
    #endregion

    // Liberar recursos
    public void Dispose() {
        _orchestrator.StopAutomaticTimerAsync(CancellationToken.None);
        _orchestrator.PropertyChanged -= Orchestrator_PropertyChanged;
        _flightEventDetector.FlightEventDetected -= FlightEventDetector_FlightEventDetected;
        Realistic.Dispose();
    }
}