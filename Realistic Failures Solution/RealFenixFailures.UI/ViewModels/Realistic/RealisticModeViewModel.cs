using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.ViewModels.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>
/// ViewModel del flujo completo del Modo Realista (4 pasos):
///   1) Selección/Creación de aeronave
///   2) Dashboard de la aeronave (stats, desgaste, historial)
///   3) Selección de nivel de riesgo
///   4) Activación con validaciones reactivas
/// Consume IUserAircraftService (CRUD + desgaste) y IEngineOrchestrator
/// (estado de conexión / fase de vuelo / arranque del motor de fallas).
/// </summary>
public sealed class RealisticModeViewModel : ObservableObject, IDisposable {
    #region Fields

    private const int MaxLiveTriggeredFailures = 200;

    private readonly IUserAircraftService _aircraftService;
    private readonly IEngineOrchestrator _orchestrator;
    private readonly ILogger<RealisticModeViewModel> _logger;
    private readonly IRealisticSessionManager _realisticSessionManager;

    private int _currentStep = 1;
    private bool _isLoading;

    // Paso 1
    private UserAircraftItemViewModel? _selectedAircraft;
    private bool _showCreateForm;
    private string _newRegistration = string.Empty;
    private string _newIcaoTypeCode = string.Empty;

    // Paso 2
    private int _dashTotalFlights;
    private double _dashTotalFlightHours;
    private int _dashTotalFailures;

    // Paso 3
    private int? _selectedRiskLevel;

    // Conexión / estado (reflejo del orchestrator)
    private bool _isSimConnected;
    private bool _isFenixConnected;
    private bool _isSessionPaused;
    private ComplexFlightPhaseEnum currentFlightPhase = ComplexFlightPhaseEnum.Unknown;
    private SimpleFlightPhaseEnum simpleFlightPhase = SimpleFlightPhaseEnum.Disconnected;
    #endregion

    #region Constructor

    public RealisticModeViewModel(
        IUserAircraftService aircraftService,
        IEngineOrchestrator orchestrator,
        ILogger<RealisticModeViewModel> logger,
        IRealisticSessionManager realisticSessionManager) {
        _aircraftService = aircraftService;
        _orchestrator = orchestrator;
        _logger = logger;
        _realisticSessionManager = realisticSessionManager;

        Aircraft = new ObservableCollection<UserAircraftItemViewModel>();
        WearSystems = new ObservableCollection<WearSystemViewModel>();
        Sessions = new ObservableCollection<FlightSessionItemViewModel>();
        LiveTriggeredFailures = new ObservableCollection<string>();

        SelectAircraftCommand = new RelayCommand<UserAircraftItemViewModel>(async a => await SelectAircraftAsync(a));
        DeleteAircraftCommand = new RelayCommand<UserAircraftItemViewModel>(async a => await DeleteAircraftAsync(a));
        ToggleCreateFormCommand = new RelayCommand(() => ShowCreateForm = !ShowCreateForm);
        CreateAircraftCommand = new RelayCommand(async () => await CreateAircraftAsync(), () => CanCreateAircraft);
        RefreshAircraftCommand = new RelayCommand(async () => await LoadAircraftAsync());

        BackToStep1Command = new RelayCommand(() => CurrentStep = 1);
        ContinueToRiskCommand = new RelayCommand(() => CurrentStep = 3, () => SelectedAircraft is not null);
        BackToStep2Command = new RelayCommand(() => CurrentStep = 2);
        ContinueToActivateCommand = new RelayCommand(() => CurrentStep = 4, () => SelectedRiskLevel.HasValue);
        BackToStep3Command = new RelayCommand(() => CurrentStep = 3);

        SelectRiskCommand = new RelayCommand<string>(SelectRisk);
        ToggleSessionCommand = new RelayCommand<FlightSessionItemViewModel>(ToggleSession);

        ActivateCommand = new RelayCommand(async () => await ActivateOrStopAsync(), () => CanActivate || IsEngineActive);
        ResumeSessionCommand = new RelayCommand(async () => await ResumeSessionAsync(), () => IsSessionPaused);

        _orchestrator.PropertyChanged += Orchestrator_PropertyChanged;
        _realisticSessionManager.PropertyChanged += RealisticSessionManager_PropertyChanged;
        _realisticSessionManager.FailureTriggered += RealisticSessionManager_FailureTriggered;
        IsSessionPaused = _realisticSessionManager.IsPaused;
        SyncConnectionFromOrchestrator();
    }
    #endregion

    #region Commands

    public RelayCommand<UserAircraftItemViewModel> SelectAircraftCommand { get; }
    public RelayCommand<UserAircraftItemViewModel> DeleteAircraftCommand { get; }
    public RelayCommand ToggleCreateFormCommand { get; }
    public RelayCommand CreateAircraftCommand { get; }
    public RelayCommand RefreshAircraftCommand { get; }
    public RelayCommand BackToStep1Command { get; }
    public RelayCommand ContinueToRiskCommand { get; }
    public RelayCommand BackToStep2Command { get; }
    public RelayCommand ContinueToActivateCommand { get; }
    public RelayCommand BackToStep3Command { get; }
    public RelayCommand<string> SelectRiskCommand { get; }
    public RelayCommand<FlightSessionItemViewModel> ToggleSessionCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public RelayCommand ResumeSessionCommand { get; }

    #endregion

    #region Collections

    public ObservableCollection<UserAircraftItemViewModel> Aircraft { get; }
    public ObservableCollection<WearSystemViewModel> WearSystems { get; }
    public ObservableCollection<FlightSessionItemViewModel> Sessions { get; }
    public ObservableCollection<string> LiveTriggeredFailures { get; }

    #endregion

    #region Step navigation

    public int CurrentStep {
        get => _currentStep;
        set {
            if (SetProperty(ref _currentStep, value)) {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(IsStep4));
            }
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;

    public bool IsLoading {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    #endregion

    #region Step 1 — aircraft selection / creation

    public UserAircraftItemViewModel? SelectedAircraft {
        get => _selectedAircraft;
        private set {
            if (SetProperty(ref _selectedAircraft, value)) {
                OnPropertyChanged(nameof(HasSelectedAircraft));
                OnPropertyChanged(nameof(SelectedAircraftTitle));
                ContinueToRiskCommand.RaiseCanExecuteChanged();
                RaiseActivationState();
            }
        }
    }

    public bool HasSelectedAircraft => SelectedAircraft is not null;
    public string SelectedAircraftTitle => SelectedAircraft is null
        ? "—"
        : $"{SelectedAircraft.Registration} · {SelectedAircraft.IcaoTypeCode}";

    public bool HasAircraft => Aircraft.Count > 0;

    public bool ShowCreateForm {
        get => _showCreateForm;
        set => SetProperty(ref _showCreateForm, value);
    }

    public string NewRegistration {
        get => _newRegistration;
        set {
            if (SetProperty(ref _newRegistration, value))
                CreateAircraftCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewIcaoTypeCode {
        get => _newIcaoTypeCode;
        set {
            if (SetProperty(ref _newIcaoTypeCode, value))
                CreateAircraftCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanCreateAircraft =>
        !string.IsNullOrWhiteSpace(NewRegistration) &&
        !string.IsNullOrWhiteSpace(NewIcaoTypeCode);

    #endregion

    #region Step 2 — dashboard

    public int DashTotalFlights { get => _dashTotalFlights; private set => SetProperty(ref _dashTotalFlights, value); }
    public double DashTotalFlightHours { get => _dashTotalFlightHours; private set => SetProperty(ref _dashTotalFlightHours, value); }
    public int DashTotalFailures { get => _dashTotalFailures; private set => SetProperty(ref _dashTotalFailures, value); }

    public bool HasSessions => Sessions.Count > 0;

    #endregion

    #region Step 3 — risk level

    public int? SelectedRiskLevel {
        get => _selectedRiskLevel;
        private set {
            if (SetProperty(ref _selectedRiskLevel, value)) {
                OnPropertyChanged(nameof(IsRiskLowSelected));
                OnPropertyChanged(nameof(IsRiskModerateSelected));
                OnPropertyChanged(nameof(IsRiskHighSelected));
                OnPropertyChanged(nameof(HasSelectedRisk));
                OnPropertyChanged(nameof(SelectedRiskDisplay));
                ContinueToActivateCommand.RaiseCanExecuteChanged();
                RaiseActivationState();
            }
        }
    }

    public bool IsRiskLowSelected => SelectedRiskLevel == 1;
    public bool IsRiskModerateSelected => SelectedRiskLevel == 2;
    public bool IsRiskHighSelected => SelectedRiskLevel == 3;
    public bool HasSelectedRisk => SelectedRiskLevel.HasValue;

    public string SelectedRiskDisplay => SelectedRiskLevel switch {
        1 => "Bajo",
        2 => "Moderado",
        3 => "Alto",
        _ => "—"
    };

    #endregion

    #region Step 4 — activation & validations

    public bool IsSimConnected {
        get => _isSimConnected;
        private set {
            if (SetProperty(ref _isSimConnected, value)) RaiseActivationState();
        }
    }

    public bool IsFenixConnected {
        get => _isFenixConnected;
        private set {
            if (SetProperty(ref _isFenixConnected, value)) RaiseActivationState();
        }
    }
    public SimpleFlightPhaseEnum SimpleFlightPhase {
        get => simpleFlightPhase;
        private set {
            if (SetProperty(ref simpleFlightPhase, value)) {
                RaiseActivationState();
            }
        }
    }
    public ComplexFlightPhaseEnum CurrentFlightPhase {
        get => currentFlightPhase;
        private set {
            if (SetProperty(ref currentFlightPhase, value)) {
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
                RaiseActivationState();
            }
        }
    }

    public string CurrentFlightPhaseDisplay {
        get {
            return CurrentFlightPhase switch {
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

    public bool IsEngineActive => _orchestrator.IsEngineActive;

    public bool IsOnGround => SimpleFlightPhase == SimpleFlightPhaseEnum.OnGround;

    /// <summary>
    /// La sesión realista está pausada (p. ej. por pérdida de datos del simulador); habilita REANUDAR SESIÓN.
    /// Refleja <c>IRealisticSessionManager.IsPaused</c> vía PropertyChanged.
    /// </summary>
    public bool IsSessionPaused {
        get => _isSessionPaused;
        private set {
            if (SetProperty(ref _isSessionPaused, value))
                ResumeSessionCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// El motor puede activarse cuando se cumplen TODAS las condiciones.
    /// Si ya está activo, se muestra el botón de detener (no se re-evalúa esto).
    /// </summary>
    public bool CanActivate =>
        !IsEngineActive &&
        SelectedAircraft is not null &&
        SelectedRiskLevel.HasValue &&
        IsSimConnected &&
        IsFenixConnected &&
        IsOnGround;

    /// <summary>Mensaje explicativo del motivo por el que el botón está deshabilitado.</summary>
    public string ActivationBlockReason {
        get {
            if (IsEngineActive) return string.Empty;
            if (SelectedAircraft is null) return "Seleccioná una aeronave para continuar.";
            if (!SelectedRiskLevel.HasValue) return "Seleccioná un nivel de riesgo.";
            if (!IsSimConnected || !IsFenixConnected) return "Conectá MSFS 2024 y el sistema Fenix A320.";
            if (!IsOnGround) return $"Solo se puede activar en tierra (fase actual: {CurrentFlightPhaseDisplay}).";
            return string.Empty;
        }
    }

    public bool HasActivationBlockReason => !string.IsNullOrEmpty(ActivationBlockReason);

    public string ActivateButtonText => IsEngineActive ? "DETENER MODO REALISTA" : "ACTIVAR MODO REALISTA";

    #endregion

    #region Public API

    /// <summary>Carga inicial: aeronaves del usuario.</summary>
    public async Task InitializeAsync() {
        await LoadAircraftAsync();
    }

    #endregion

    #region Private helpers

    private async Task LoadAircraftAsync() {
        try {
            IsLoading = true;
            var list = await _aircraftService.GetAllAsync(CancellationToken.None);
            Aircraft.Clear();
            foreach (var dto in list) {
                Aircraft.Add(new UserAircraftItemViewModel(dto));
            }
            OnPropertyChanged(nameof(HasAircraft));
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo cargar la lista de aeronaves.");
        } finally {
            IsLoading = false;
        }
    }

    private async Task SelectAircraftAsync(UserAircraftItemViewModel? item) {
        if (item is null) return;

        foreach (var a in Aircraft) a.IsSelected = a.Id == item.Id;
        SelectedAircraft = item;

        await LoadDashboardAsync(item.Id);
        CurrentStep = 2;
    }

    private async Task DeleteAircraftAsync(UserAircraftItemViewModel? item) {
        if (item is null) return;
        try {
            await _aircraftService.DeleteAsync(item.Id, CancellationToken.None);
            Aircraft.Remove(item);
            if (SelectedAircraft?.Id == item.Id) SelectedAircraft = null;
            OnPropertyChanged(nameof(HasAircraft));
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo eliminar la aeronave {Id}.", item.Id);
        }
    }

    private async Task CreateAircraftAsync() {
        if (!CanCreateAircraft) return;
        try {
            var created = await _aircraftService.CreateAsync(
                new CreateUserAircraftRequest {
                    Registration = NewRegistration,
                    IcaoTypeCode = NewIcaoTypeCode,
                },
                CancellationToken.None);

            var vm = new UserAircraftItemViewModel(created);
            Aircraft.Insert(0, vm);
            OnPropertyChanged(nameof(HasAircraft));

            NewRegistration = string.Empty;
            NewIcaoTypeCode = string.Empty;
            ShowCreateForm = false;

            await SelectAircraftAsync(vm);
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo crear la aeronave.");
        }
    }

    private async Task LoadDashboardAsync(int aircraftId) {
        try {
            IsLoading = true;
            var dash = await _aircraftService.GetDashboardAsync(aircraftId, CancellationToken.None);

            var validSessions = dash.Aircraft.FlightSessions.Where(x => x.Duration.HasValue).ToList();

            DashTotalFlights = validSessions.Count;
            DashTotalFlightHours = Math.Round(validSessions.Sum(x => x.Duration!.Value.TotalHours), 1);
            DashTotalFailures = validSessions.Sum(x => x.TriggeredFailures.Count);

            WearSystems.Clear();
            foreach (var w in dash.SystemWears.OrderBy(x => x.SystemWear.DisplayOrder)) {
                WearSystems.Add(new WearSystemViewModel(w));
            }

            Sessions.Clear();
            foreach (var s in dash.Aircraft.FlightSessions) {
                Sessions.Add(new FlightSessionItemViewModel(s));
            }
            OnPropertyChanged(nameof(HasSessions));
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo cargar el dashboard de la aeronave {Id}.", aircraftId);
        } finally {
            IsLoading = false;
        }
    }

    private void SelectRisk(string? level) {
        if (int.TryParse(level, out var value) && value is >= 1 and <= 3) {
            SelectedRiskLevel = value;
        }
    }

    private static void ToggleSession(FlightSessionItemViewModel? session) {
        if (session is not null) session.IsExpanded = !session.IsExpanded;
    }

    private async Task ActivateOrStopAsync() {
        try {
            if (IsEngineActive) {
                await _orchestrator.StopCurrentModeAsync(CancellationToken.None);
                _logger.LogInformation("Modo Realista detenido.");
                return;
            }

            if (!CanActivate || SelectedAircraft is null || !SelectedRiskLevel.HasValue) return;

            LiveTriggeredFailures.Clear();
            var risk = (RiskLevel)SelectedRiskLevel.Value;
            await _orchestrator.StartRealisticModeAsync(SelectedAircraft.Id, risk, CancellationToken.None);
            _logger.LogInformation(
                "Modo Realista activado. Aeronave={Registration} Riesgo={Risk}",
                SelectedAircraft.Registration, risk);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al activar/detener el Modo Realista.");
        } finally {
            RaiseActivationState();
        }
    }

    private void RealisticSessionManager_FailureTriggered(object? sender, FailureTriggeredEventArgs e) {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            LiveTriggeredFailures.Insert(0, $"{e.TriggeredAtUtc.ToLocalTime():HH:mm:ss}  {e.Description}");
            while (LiveTriggeredFailures.Count > MaxLiveTriggeredFailures) {
                LiveTriggeredFailures.RemoveAt(LiveTriggeredFailures.Count - 1);
            }
        });
    }

    private void RealisticSessionManager_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(IRealisticSessionManager.IsPaused)) {
            IsSessionPaused = _realisticSessionManager.IsPaused;
        }
    }

    private async Task ResumeSessionAsync() {
        if (!IsSessionPaused) return;
        try {
            await _realisticSessionManager.ResumeAsync(CancellationToken.None);
            _logger.LogInformation("Sesión Realista reanudada manualmente.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al reanudar la sesión Realista.");
        }
    }

    private void Orchestrator_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(IEngineOrchestrator.ConnectionStatus)) {
            SyncConnectionFromOrchestrator();
        } else if (e.PropertyName == nameof(IEngineOrchestrator.CurrentFlightPhase)) {
            var phase = _orchestrator.CurrentFlightPhase;
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
                CurrentFlightPhase = phase;
            });
        } else if (e.PropertyName == nameof(IEngineOrchestrator.IsEngineActive) ||
                   e.PropertyName == nameof(IEngineOrchestrator.CurrentMode)) {
            OnPropertyChanged(nameof(IsEngineActive));
            RaiseActivationState();
        }
    }

    private void SyncConnectionFromOrchestrator() {
        var status = _orchestrator.ConnectionStatus;
        if (status is null) return;
        var phase = _orchestrator.CurrentFlightPhase;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            IsSimConnected = status.IsSimConnectConnected;
            IsFenixConnected = status.IsFenixConnected;
            SimpleFlightPhase = status.CurrentFlightPhase;
            CurrentFlightPhase = phase;
        });
    }

    private void RaiseActivationState() {
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(ActivationBlockReason));
        OnPropertyChanged(nameof(HasActivationBlockReason));
        OnPropertyChanged(nameof(ActivateButtonText));
        OnPropertyChanged(nameof(IsEngineActive));
        OnPropertyChanged(nameof(IsOnGround));
        ActivateCommand.RaiseCanExecuteChanged();
    }

    #endregion

    public void Dispose() {
        _orchestrator.PropertyChanged -= Orchestrator_PropertyChanged;
        _realisticSessionManager.PropertyChanged -= RealisticSessionManager_PropertyChanged;
        _realisticSessionManager.FailureTriggered -= RealisticSessionManager_FailureTriggered;
    }
}
