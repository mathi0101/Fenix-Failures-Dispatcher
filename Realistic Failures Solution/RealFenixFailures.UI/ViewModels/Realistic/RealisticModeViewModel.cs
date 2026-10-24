using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>
/// ViewModel del flujo completo del Modo Realista (3 pasos):
///   1) Selección/Creación de aeronave
///   2) Dashboard de la aeronave (stats, desgaste, historial)
///   3) Preparación y monitoreo (perfil, requisitos, OFP pendiente, activación)
/// Consume IUserAircraftService (CRUD + desgaste) y IEngineOrchestrator
/// (estado de conexión / fase de vuelo / arranque del motor de fallas).
/// </summary>
public sealed class RealisticModeViewModel : ObservableObject, IDisposable {
    #region Fields

    private const int MaxLiveTriggeredFailures = 50;

    private readonly IUserAircraftService _aircraftService;
    private readonly IEngineOrchestrator _orchestrator;
    private readonly ILogger<RealisticModeViewModel> _logger;
    private readonly IRealisticSessionManager _realisticSessionManager;
    private readonly IFlightSessionService _flightSessionService;
    private readonly ISimbriefOfpService _ofpService;
    private readonly UserPreferencesService _preferences;
    private readonly IRealisticExpectationService? _expectationService;

    private int _currentStep = 1;
    private bool _isLoading;

    private static readonly string[] AllowedIcaoTypes = ["A319", "A320", "A321"];
    private bool _isEngineActive;
    private bool _stopConfirmationVisible;
    private bool _isOperationPending;

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
    private bool _hasAircraftData;
    private bool _isSessionPaused;
    private bool _restoreFailuresOnResume = true;
    private ComplexFlightPhaseEnum currentFlightPhase = ComplexFlightPhaseEnum.Unknown;
    private SimpleFlightPhaseEnum simpleFlightPhase = SimpleFlightPhaseEnum.Disconnected;
    private bool _isVatsimConfirmVisible;
    private TaskCompletionSource<bool>? _vatsimConfirmTcs;
    #endregion

    #region Constructor

    public RealisticModeViewModel(
        IUserAircraftService aircraftService,
        IEngineOrchestrator orchestrator,
        ILogger<RealisticModeViewModel> logger,
        IRealisticSessionManager realisticSessionManager,
        IFlightSessionService flightSessionService,
        ISimbriefOfpService ofpService,
        UserPreferencesService preferences,
        IRealisticExpectationService? expectationService = null) {
        _aircraftService = aircraftService;
        _orchestrator = orchestrator;
        _logger = logger;
        _realisticSessionManager = realisticSessionManager;
        _flightSessionService = flightSessionService;
        _ofpService = ofpService;
        _preferences = preferences;
        _expectationService = expectationService;

        Aircraft = new ObservableCollection<UserAircraftItemViewModel>();
        WearSystems = new ObservableCollection<WearSystemViewModel>();
        Sessions = new ObservableCollection<FlightSessionItemViewModel>();
        LiveTriggeredFailures = new ObservableCollection<LiveFailureEntryViewModel>();

        SelectAircraftCommand = new RelayCommand<UserAircraftItemViewModel>(async a => await SelectAircraftAsync(a), _ => !IsEngineActive && !IsOperationPending);
        DeleteAircraftCommand = new RelayCommand<UserAircraftItemViewModel>(async a => await DeleteAircraftAsync(a));
        ToggleCreateFormCommand = new RelayCommand(() => ShowCreateForm = !ShowCreateForm);
        CreateAircraftCommand = new RelayCommand(async () => await CreateAircraftAsync(), () => CanCreateAircraft);
        RefreshAircraftCommand = new RelayCommand(async () => await LoadAircraftAsync());

        BackToStep1Command = new RelayCommand(() => CurrentStep = 1, () => !IsEngineActive && !IsOperationPending);
        ContinueToPrepCommand = new RelayCommand(() => CurrentStep = 3, () => SelectedAircraft is not null && !IsEngineActive && !IsOperationPending);
        BackToStep2Command = new RelayCommand(() => CurrentStep = 2, () => !IsEngineActive && !IsOperationPending);
        ShowSessionResultFromHistoryCommand = new RelayCommand<int>(async id => await ShowSessionResultFromHistoryAsync(id));
        NavigateToStepCommand = new RelayCommand<string>(NavigateToStep, CanNavigateToStep);

        SelectRiskCommand = new RelayCommand<string>(SelectRisk, _ => !IsEngineActive && !IsOperationPending);
        ConfirmVatsimStartCommand = new RelayCommand(() => _vatsimConfirmTcs?.TrySetResult(true));
        CancelVatsimStartCommand = new RelayCommand(() => _vatsimConfirmTcs?.TrySetResult(false));
        ToggleSessionCommand = new RelayCommand<FlightSessionItemViewModel>(ToggleSession);

        ActivateCommand = new RelayCommand(async () => await ActivateOrStopAsync(), () => (CanActivate || IsEngineActive) && !IsOperationPending);
        ResumeSessionCommand = new RelayCommand(async () => await ResumeSessionAsync(), () => IsSessionPaused && !IsOperationPending);
        PauseSessionCommand = new RelayCommand(async () => await PauseSessionAsync(), () => IsEngineActive && !IsSessionPaused && !IsOperationPending);
        CancelStopCommand = new RelayCommand(() => StopConfirmationVisible = false, () => !IsOperationPending);
        DownloadOfpCommand = new RelayCommand(async () => await DownloadOfpAsync(), () => CanDownloadOfp && !IsOfpDownloading);
        DeleteOfpCommand = new RelayCommand(DeleteOfp, () => CanDeleteOfp);
        DiscardPreparedCommand = new RelayCommand(async () => await DiscardPreparedAsync(), () => IsDiscardPreparedVisible && !IsOperationPending);

        SetSimulatedScaleCommand = new RelayCommand<string>(
            s => { if (double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var scale)) _orchestrator.SetSimulatedTimeScale(scale); });
        AdvanceSimulatedTimeCommand = new RelayCommand<string>(
            s => { if (double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var minutes)) _orchestrator.AdvanceSimulatedTime(TimeSpan.FromMinutes(minutes)); });

        _orchestrator.PropertyChanged += Orchestrator_PropertyChanged;
        _realisticSessionManager.PropertyChanged += RealisticSessionManager_PropertyChanged;
        _realisticSessionManager.FailureTriggered += RealisticSessionManager_FailureTriggered;
        _realisticSessionManager.FailureStateChanged += RealisticSessionManager_FailureStateChanged;
        IsSessionPaused = _realisticSessionManager.IsPaused;
        IsEngineActive = _orchestrator.IsEngineActive;
        SyncConnectionFromOrchestrator();
    }
    #endregion

    #region Commands

    public RelayCommand<UserAircraftItemViewModel> SelectAircraftCommand { get; }
    public RelayCommand<UserAircraftItemViewModel> DeleteAircraftCommand { get; }
    public RelayCommand<string> SetSimulatedScaleCommand { get; }
    public RelayCommand<string> AdvanceSimulatedTimeCommand { get; }
    public RelayCommand ToggleCreateFormCommand { get; }
    public RelayCommand CreateAircraftCommand { get; }
    public RelayCommand RefreshAircraftCommand { get; }
    public RelayCommand BackToStep1Command { get; }
    public RelayCommand ContinueToPrepCommand { get; }
    public RelayCommand BackToStep2Command { get; }
    public RelayCommand<string> NavigateToStepCommand { get; }
    public RelayCommand<string> SelectRiskCommand { get; }
    public RelayCommand<FlightSessionItemViewModel> ToggleSessionCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public RelayCommand ResumeSessionCommand { get; }
    public RelayCommand PauseSessionCommand { get; }
    public RelayCommand CancelStopCommand { get; }
    public RelayCommand DownloadOfpCommand { get; }
    public RelayCommand DiscardPreparedCommand { get; }
    public RelayCommand ConfirmVatsimStartCommand { get; }
    public RelayCommand CancelVatsimStartCommand { get; }

    #endregion

    #region Collections

    public ObservableCollection<UserAircraftItemViewModel> Aircraft { get; }
    public ObservableCollection<WearSystemViewModel> WearSystems { get; }
    public ObservableCollection<FlightSessionItemViewModel> Sessions { get; }
    public ObservableCollection<LiveFailureEntryViewModel> LiveTriggeredFailures { get; }

    #endregion

    #region Step navigation

    public int CurrentStep {
        get => _currentStep;
        set {
            if (SetProperty(ref _currentStep, value)) {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(CanNavigateToStep1));
                OnPropertyChanged(nameof(CanNavigateToStep2));
                OnPropertyChanged(nameof(CanNavigateToStep3));
                NavigateToStepCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Navegación por pastillas (spec §8.5): hacia atrás libre; hacia adelante solo «Continuar».</summary>
    private void NavigateToStep(string? step) {
        if (int.TryParse(step, out var value) && CanNavigateToStep(value)) {
            CurrentStep = value;
        }
    }

    private bool CanNavigateToStep(string? step) =>
        int.TryParse(step, out var value) && CanNavigateToStep(value, out _);

    private bool CanNavigateToStep(int step) => CanNavigateToStep(step, out _);

    private bool CanNavigateToStep(int step, out int resolved) {
        resolved = step;
        return step is >= 1 and <= 3 && step <= CurrentStep && !IsEngineActive && !IsOperationPending;
    }

    public bool CanNavigateToStep1 => CanNavigateToStep(1);
    public bool CanNavigateToStep2 => CanNavigateToStep(2);
    public bool CanNavigateToStep3 => CanNavigateToStep(3);

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

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
                ContinueToPrepCommand.RaiseCanExecuteChanged();
                DownloadOfpCommand.RaiseCanExecuteChanged();
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

    public IReadOnlyList<string> IcaoOptions => AllowedIcaoTypes;

    public bool CanCreateAircraft {
        get {
            var registration = NewRegistration.Trim().ToUpperInvariant();
            return registration.Length is > 0 and <= 10 && AllowedIcaoTypes.Contains(NewIcaoTypeCode);
        }
    }

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
                OnPropertyChanged(nameof(HeaderProfileDisplay));
                RefreshExpectation();
                RaiseActivationState();
            }
        }
    }

    public bool IsRiskLowSelected => SelectedRiskLevel == 1;
    public bool IsRiskModerateSelected => SelectedRiskLevel == 2;
    public bool IsRiskHighSelected => SelectedRiskLevel == 3;
    public bool IsRiskSquawk7700Selected => SelectedRiskLevel == 4;
    public bool HasSelectedRisk => SelectedRiskLevel.HasValue;

    public string SelectedRiskDisplay => SelectedRiskLevel switch {
        1 => "Bajo",
        2 => "Moderado",
        3 => "Alto",
        4 => "7700",
        _ => "—"
    };

    public string HeaderProfileDisplay => SelectedRiskLevel.HasValue ? SelectedRiskDisplay : "Sin perfil";

    public string RiskProfileButtonText => HasSelectedRisk ? "Cambiar perfil" : "Elegir perfil";

    /// <summary>Subtítulo del card-button de perfil (spec §8.4).</summary>
    public string ProfileCardSubtitle => HasSelectedRisk ? "Seleccionado" : "Sin perfil";

    public bool CanChangeProfile => !IsEngineActive && !IsOperationPending;

    public bool IsVatsimConfirmVisible {
        get => _isVatsimConfirmVisible;
        private set => SetProperty(ref _isVatsimConfirmVisible, value);
    }

    #endregion

    #region Step 3 — activación, OFP y validaciones

    public bool IsSimConnected {
        get => _isSimConnected;
        private set {
            if (SetProperty(ref _isSimConnected, value)) {
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
                RaiseActivationState();
            }
        }
    }

    public bool IsFenixConnected {
        get => _isFenixConnected;
        private set {
            if (SetProperty(ref _isFenixConnected, value)) {
                RaiseActivationState();
                RefreshPreparedState();
            }
        }
    }

    public bool HasAircraftData {
        get => _hasAircraftData;
        private set {
            if (SetProperty(ref _hasAircraftData, value)) {
                OnPropertyChanged(nameof(CurrentFlightPhaseDisplay));
                RaiseActivationState();
            }
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
            if (!IsSimConnected) return "Disconnected";
            if (!HasAircraftData) return "No aircraft data";
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

    public bool IsEngineActive {
        get => _isEngineActive;
        private set {
            if (SetProperty(ref _isEngineActive, value)) {
                OnPropertyChanged(nameof(SessionStatusDisplay));
                RaiseActivationState();
            }
        }
    }

    /// <summary>Fallas preparadas en cola mientras Fenix no está disponible (solo la cuenta, sin preview).</summary>
    public int PreparedCount => _realisticSessionManager.PreparedCount;

    public bool IsSessionActiveWithoutFenix => IsEngineActive && !IsFenixConnected;

    public bool IsDiscardPreparedVisible => IsSessionActiveWithoutFenix && PreparedCount > 0;

    public string DiscardPreparedLabel => $"DESCARTAR PREPARADAS ({PreparedCount})";

    private FlightPlanDto? _ofpPlanForUi;
    private bool _isOfpDownloading;

    /// <summary>Userid de SimBrief leído de las preferencias de la app (Ajustes). No se edita desde el modo Realistic.</summary>
    public string SimbriefUserId => _preferences.SimbriefUserId;

    public bool CanDownloadOfp =>
        SelectedAircraft is not null &&
        !IsEngineActive && !IsOperationPending;

    /// <summary>Tooltip del botón Descargar OFP (spec §8.3): explica el bloqueo durante sesión.</summary>
    public string OfpButtonToolTip => IsEngineActive
        ? "Sesión activa: el OFP no puede modificarse. Detené la sesión para cargar otro plan."
        : "Descarga el plan operacional de SimBrief del user, lo valida (ICAO del avión, campos mínimos y ventana UTC) y deja lista la activación.";

    public bool OfpValidated => _ofpPlanForUi?.IsUsable == true;
    public bool IsOfpDownloading {
        get => _isOfpDownloading;
        private set {
            if (SetProperty(ref _isOfpDownloading, value)) {
                RaiseActivationState();
                DownloadOfpCommand.RaiseCanExecuteChanged();
                DeleteOfpCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RouteDisplay {
        get {
            if (_ofpPlanForUi is null) return "No registrada";
            var anyText = !string.IsNullOrWhiteSpace(_ofpPlanForUi.RouteText);
            var ruta = anyText ? _ofpPlanForUi.RouteText! : $"{_ofpPlanForUi.OriginIcao} → {_ofpPlanForUi.DestinationIcao}";
            return _ofpPlanForUi.IsUsable ? ruta : $"{ruta} ({OfpStatusDisplay})";
        }
    }

    public string OfpStatusDisplay {
        get => _ofpPlanForUi switch {
            null => "Pendiente · Sin OFP",
            var p => p.ValidationState switch {
                ValidationState.Valid => "Válido · Listo para activar",
                ValidationState.Pending => "Pendiente de validación",
                ValidationState.Invalid => $"Inválido · {p.ValidationMessage}",
                ValidationState.Expired => $"Fuera de ventana · {p.ValidationMessage}",
                _ => p.ValidationState.ToString()
            }
        };
    }

    #region Detalle del OFP y check de aeronave (spec §8.1/§8.2)

    /// <summary>Hay OFP cargado: muestra las filas de detalle del bloque OFP.</summary>
    public bool HasOfpDetails => _ofpPlanForUi is not null;

    public string FlightNumberDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.FlightNumber) ? "—" : _ofpPlanForUi.FlightNumber!;
    public string CallsignDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.Callsign) ? "—" : _ofpPlanForUi.Callsign!;
    public string OffBlockDisplay => _ofpPlanForUi is null ? "—" : _ofpPlanForUi.OffBlockUtc.ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
    public string ArrivalDisplay => _ofpPlanForUi is null ? "—" : _ofpPlanForUi.EstimatedArrivalUtc.ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
    public string OfpDurationDisplay => _ofpPlanForUi is null || _ofpPlanForUi.EstimatedDurationMinutes <= 0 ? "—" : FormatDuration(_ofpPlanForUi.EstimatedDurationMinutes);
    public string DistanceDisplay => _ofpPlanForUi?.RouteDistanceNM is > 0 ? $"{_ofpPlanForUi.RouteDistanceNM.Value:N0} NM" : "—";
    public string OfpAircraftDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.AircraftIcaoType) ? "—" : _ofpPlanForUi.AircraftIcaoType!;
    public string SourceDisplay => _ofpPlanForUi is null ? "—" : $"{_ofpPlanForUi.Source} · {_ofpPlanForUi.DownloadedAtUtc.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC";

    public string OfpOriginDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.OriginIcao) ? "—" : _ofpPlanForUi.OriginIcao!;
    public string OfpDestinationDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.DestinationIcao) ? "—" : _ofpPlanForUi.DestinationIcao!;

    /// <summary>Texto del route del OFP (spec §8.1); null para ocultar la fila cuando está vacío.</summary>
    public string? OfpRouteTextDisplay => string.IsNullOrWhiteSpace(_ofpPlanForUi?.RouteText) ? null : _ofpPlanForUi.RouteText;

    /// <summary>Hay texto de route cargado en el OFP.</summary>
    public bool HasOfpRouteText => OfpRouteTextDisplay is not null;

    /// <summary>Check «Aeronave del OFP» del panel de requisitos: refleja el resultado de la última descarga/validación.</summary>
    public bool OfpAircraftMatchIsOk => _ofpPlanForUi != null && _ofpPlanForUi?.AircraftIcaoType == _selectedAircraft?.IcaoTypeCode;

    public string OfpAircraftMatchDisplay => _ofpPlanForUi switch {
        null => "✘ Sin OFP",
        var p when p.ValidationState == ValidationState.Pending => "✘ Pendiente",
        var p when p.IsUsable || p.AircraftIcaoType==_selectedAircraft?.IcaoTypeCode => "✔ Cumplido",
        _ => "✘ Inválido"
    };

    /// <summary>Fila «OFP» del panel de requisitos: texto corto; el detalle completo vive en el tooltip (OfpValidationToolTip) y en el bloque OFP.</summary>
    public string OfpRowDisplay => _ofpPlanForUi switch {
        null => "✘ Sin OFP",
        var p when p.IsUsable => "✔ Cumplido",
        var p when p.ValidationState == ValidationState.Pending => "✘ Pendiente",
        var p when p.ValidationState == ValidationState.Expired => "✘ Vencido",
        _ => "✘ Inválido"
    };

    /// <summary>Tooltip de las filas OFP de requisitos: detalle completo de la última validación.</summary>
    public string OfpValidationToolTip => _ofpPlanForUi switch {
        null => "Todavía no hay OFP cargado. Descargalo con el botón «Descargar OFP»; es obligatorio para activar.",
        var p when p.IsUsable => $"OFP válido y listo para activar. {p.ValidationMessage}".Trim(),
        var p when p.ValidationState == ValidationState.Pending => "Pendiente de validación: descargá el OFP de nuevo para completarla.",
        var p => string.IsNullOrWhiteSpace(p.ValidationMessage)
            ? $"Validación sin detalle ({p.ValidationState}). Descargá el OFP de nuevo."
            : $"{p.ValidationState switch { ValidationState.Expired => "Fuera de ventana", ValidationState.Invalid => "Inválido", _ => p.ValidationState.ToString() }}: {p.ValidationMessage}"
    };

    public static string FormatDuration(int minutes) => $"{minutes / 60} h {minutes % 60:00} m";

    #endregion

    /// <summary>Estimación de fallas para la próxima sesión (spec §8.7), junto al desgaste medio.</summary>
    public string ExpectationDisplay {
        get => _expectationDisplay;
        private set => SetProperty(ref _expectationDisplay, value);
    }
    private string _expectationDisplay = "Sin estimación";

    public string ExpectationToolTip =>
        "Estimación de presentación (no cambia el motor): usa la física del modelo con el desgaste medio actual "
        + "y la duración del OFP cargado. Referencia Moderado hasta que elijas un perfil; con perfil, aplica su frecuencia.";

    public void ApplyOfpResult(FlightPlanDto dto) {
        _ofpPlanForUi = dto;
        RaiseOfpState();
        RefreshExpectation();
        RaiseActivationState();
    }

    private void RaiseOfpState() {
        OnPropertyChanged(nameof(OfpValidated));
        OnPropertyChanged(nameof(RouteDisplay));
        OnPropertyChanged(nameof(OfpStatusDisplay));
        OnPropertyChanged(nameof(HasOfpDetails));
        OnPropertyChanged(nameof(FlightNumberDisplay));
        OnPropertyChanged(nameof(CallsignDisplay));
        OnPropertyChanged(nameof(OffBlockDisplay));
        OnPropertyChanged(nameof(ArrivalDisplay));
        OnPropertyChanged(nameof(OfpDurationDisplay));
        OnPropertyChanged(nameof(DistanceDisplay));
        OnPropertyChanged(nameof(OfpAircraftDisplay));
        OnPropertyChanged(nameof(SourceDisplay));
        OnPropertyChanged(nameof(OfpOriginDisplay));
        OnPropertyChanged(nameof(OfpDestinationDisplay));
        OnPropertyChanged(nameof(OfpRouteTextDisplay));
        OnPropertyChanged(nameof(HasOfpRouteText));
        OnPropertyChanged(nameof(OfpAircraftMatchIsOk));
        OnPropertyChanged(nameof(OfpAircraftMatchDisplay));
        OnPropertyChanged(nameof(OfpRowDisplay));
        OnPropertyChanged(nameof(OfpValidationToolTip));
        DeleteOfpCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Botón rojo de descarte del OFP cargado (pedido del usuario): devuelve el panel al estado «Sin OFP».</summary>
    public RelayCommand DeleteOfpCommand { get; }

    private bool CanDeleteOfp => HasOfpDetails && !IsOfpDownloading && !IsEngineActive && !IsOperationPending;

    private void DeleteOfp() {
        if (!CanDeleteOfp) return;
        _ofpPlanForUi = null;
        _logger.LogInformation("OFP descartado por el usuario; el panel vuelve al estado sin OFP.");
        RaiseOfpState();
        RefreshExpectation();
        RaiseActivationState();
    }

    public string SessionStatusDisplay {
        get {
            if (IsSessionPaused) return "Evaluación suspendida";
            if (IsEngineActive) return "Activa";
            return "Sin sesión";
        }
    }

    public bool IsOnGround => SimpleFlightPhase == SimpleFlightPhaseEnum.OnGround;

    public bool StopConfirmationVisible {
        get => _stopConfirmationVisible;
        set => SetProperty(ref _stopConfirmationVisible, value);
    }

    /// <summary>
    /// La sesión realista está pausada (p. ej. por pérdida de datos del simulador); habilita REANUDAR EVALUACIÓN.
    /// Refleja <c>IRealisticSessionManager.IsPaused</c> vía PropertyChanged.
    /// </summary>
    /// <summary>
    /// Multiplicador de tiempo simulado vigente (para el knob de debug del panel).
    /// </summary>
    public string SimulatedTimeScaleDisplay => _orchestrator.SimulatedTimeScale switch {
        1.0 => "1x",
        var s => $"{s:0.#}x",
    };

    public bool IsSessionPaused {
        get => _isSessionPaused;
        private set {
            if (SetProperty(ref _isSessionPaused, value)) {
                ResumeSessionCommand.RaiseCanExecuteChanged();
                RaiseActivationState();
                OnPropertyChanged(nameof(SessionStatusDisplay));
            }
        }
    }

    /// <summary>
    /// Al reanudar una sesión pausada, re-armar las fallas que estaban en curso antes de la pausa.
    /// El diseño objetivo (§6) permite reactivar y restaurar opcionalmente las fallas de la sesión.
    /// </summary>
    public bool RestoreFailuresOnResume {
        get => _restoreFailuresOnResume;
        set => SetProperty(ref _restoreFailuresOnResume, value);
    }

    /// <summary>
    /// El motor puede activarse cuando se cumplen TODAS las condiciones.
    /// Si ya está activo, se muestra el botón de detener (no se re-evalúa esto).
    /// </summary>
    public bool CanActivate =>
        !IsEngineActive &&
        !IsSessionPaused &&
        SelectedAircraft is not null &&
        SelectedRiskLevel.HasValue &&
        OfpValidated &&
        IsSimConnected &&
        HasAircraftData;

    /// <summary>Mensaje explicativo del motivo por el que el botón está deshabilitado.</summary>
    public string ActivationBlockReason {
        get {
            if (IsEngineActive) return string.Empty;
            if (IsSessionPaused) return "La evaluación está pausada. Reanudá la sesión para seguir generando fallas.";
            if (SelectedAircraft is null) return "Seleccioná una aeronave para continuar.";
            if (!SelectedRiskLevel.HasValue) return "Seleccioná un perfil de riesgo.";
            if (!OfpValidated) return "OFP pendiente: el plan de vuelo operacional es obligatorio. Descargalo y validalo antes de activar.";
            if (!IsSimConnected) return "Conectá MSFS 2024 para comenzar el modo Realistic.";
            if (!HasAircraftData) return "MSFS conectado pero sin datos de aeronave disponibles.";
            return string.Empty;
        }
    }

    public bool HasActivationBlockReason => !string.IsNullOrEmpty(ActivationBlockReason);

    public string ActivateButtonText =>
        IsOperationPending ? "PROCESANDO..." :
        IsEngineActive ? "DETENER MODO REALISTA" : "ACTIVAR MODO REALISTA";

    public string ActionToolTip => IsSessionPaused
        ? ActivationBlockReason
        : IsEngineActive
        ? "Termina la sesión e intenta desactivar las fallas en Fenix; confirma en vuelo. Comprobá las fallas manualmente si la API no responde."
        : ActivationBlockReason;

    public bool IsOperationPending {
        get => _isOperationPending;
        private set {
            if (SetProperty(ref _isOperationPending, value)) RaiseActivationState();
        }
    }

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
                    Registration = NewRegistration.Trim().ToUpperInvariant(),
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
            RefreshExpectation();
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo cargar el dashboard de la aeronave {Id}.", aircraftId);
        } finally {
            IsLoading = false;
        }
    }

    private void SelectRisk(string? level) {
        if (int.TryParse(level, out var value) && value is >= 1 and <= 4) {
            SelectedRiskLevel = value;
        }
    }

    /// <summary>Recalcula la expectativa de fallas (spec §8.7) con el desgaste medio, la duración del OFP y el perfil vigente.</summary>
    private void RefreshExpectation() {
        if (_expectationService is null) return;
        try {
            double? avgWear = WearSystems.Count == 0 ? null : WearSystems.Average(w => (double)w.WearPercentage);
            double? hours = _ofpPlanForUi?.EstimatedDurationMinutes is > 0 ? _ofpPlanForUi.EstimatedDurationMinutes / 60.0 : null;
            var dto = _expectationService.Compute(avgWear, hours, (RiskLevel?)SelectedRiskLevel);

            ExpectationDisplay = dto.FlightHours is { } h
                ? $"≈ {dto.ExpectedFailures:0.##} fallas esperadas · P(≥1) = {dto.ProbabilityAtLeastOnePct:0}% para {FormatDuration((int)Math.Round(h * 60))}"
                : $"≈ {dto.RatePerHour:0.##} fallas/h de vuelo (referencia {(SelectedRiskLevel is null ? "Moderado" : SelectedRiskDisplay)})";
        } catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo calcular la expectativa de fallas.");
        }
    }

    private static void ToggleSession(FlightSessionItemViewModel? session) {
        if (session is not null) session.IsExpanded = !session.IsExpanded;
    }

    private async Task DownloadOfpAsync() {
        if (IsOfpDownloading || SelectedAircraft is null) return;

        if (string.IsNullOrWhiteSpace(SimbriefUserId)) {
            ApplyOfpResult(new FlightPlanDto {
                ValidationState = ValidationState.Invalid,
                ValidationMessage = "Configurá tu userid de SimBrief en Ajustes antes de descargar el OFP.",
            });
            _logger.LogWarning("Descarga de OFP sin userid de SimBrief configurado en Ajustes.");
            return;
        }

        IsOfpDownloading = true;
        try {
            var dto = await _ofpService.DownloadAsync(SelectedAircraft.IcaoTypeCode, SimbriefUserId.Trim(), CancellationToken.None);
            ApplyOfpResult(dto);

            if (dto.IsUsable) {
                _logger.LogInformation("OFP válido cargado: {Origin} → {Dest} para {Reg}.",
                    dto.OriginIcao, dto.DestinationIcao, SelectedAircraft.Registration);
            } else {
                _logger.LogWarning("OFP inválido: {State} · {Msg}", dto.ValidationState, dto.ValidationMessage);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al descargar/validar el OFP.");
            ApplyOfpResult(new FlightPlanDto {
                ValidationState = ValidationState.Invalid,
                ValidationMessage = $"Error al descargar: {ex.Message}",
            });
        } finally {
            IsOfpDownloading = false;
        }
    }

    private async Task ActivateOrStopAsync() {
        if (IsOperationPending) return;
        IsOperationPending = true;
        try {
            if (IsEngineActive) {
                if (!IsOnGround && !StopConfirmationVisible) {
                    StopConfirmationVisible = true;
                    return;
                }
                StopConfirmationVisible = false;
                await _orchestrator.StopCurrentModeAsync(CancellationToken.None);
                _logger.LogInformation("Modo Realista detenido.");
                await ShowLastSessionResultAsync();
                return;
            }

            if (!CanActivate || SelectedAircraft is null || !SelectedRiskLevel.HasValue) return;

            if (!await ConfirmVatsimStartAsync()) return;
            if (!CanActivate || SelectedAircraft is null || !SelectedRiskLevel.HasValue) return;

            LiveTriggeredFailures.Clear();
            var risk = (RiskLevel)SelectedRiskLevel.Value;
            await _orchestrator.StartRealisticModeAsync(SelectedAircraft.Id, risk, _ofpPlanForUi!.Id, CancellationToken.None);
            _logger.LogInformation(
                "Modo Realista activado. Aeronave={Registration} Riesgo={Risk}",
                SelectedAircraft.Registration, risk);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al activar/detener el Modo Realista.");
        } finally {
            IsOperationPending = false;
        }
    }

    private async Task<bool> ConfirmVatsimStartAsync() {
        _vatsimConfirmTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IsVatsimConfirmVisible = true;
        try {
            return await _vatsimConfirmTcs.Task;
        } finally {
            IsVatsimConfirmVisible = false;
            _vatsimConfirmTcs = null;
        }
    }

    #region Modal de resultados (diseño objetivo §13)

    /// <summary>Abre el modal de resultados de una sesión finalizada por id (botón «DETENER», historial o recuperación).</summary>
    public async Task ShowSessionResultByIdAsync(int sessionId) {
        try {
            var result = await _flightSessionService.GetResultAsync(sessionId, CancellationToken.None);
            if (result is null) {
                _logger.LogWarning("No hay resultados para la sesión {SessionId}", sessionId);
                return;
            }

            var vm = new SessionResultWindowViewModel(result);
            var showHistory = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                var owner = System.Windows.Application.Current?.MainWindow;
                var window = new Views.SessionResultWindow(vm) { Owner = owner };
                return window.ShowDialog() == true;
            });
            if (showHistory) {
                await LoadAircraftAsync();
                var aircraft = Aircraft.FirstOrDefault(a => a.Id == result.UserAircraftId);
                if (aircraft is not null) {
                    await SelectAircraftAsync(aircraft);
                    var session = Sessions.FirstOrDefault(s => s.Id == sessionId);
                    if (session is not null) session.IsExpanded = true;
                }
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo mostrar el resultado de la sesión {SessionId}", sessionId);
        }
    }

    /// <summary>Abre el resultado de la sesión que acaba de cerrarse (tras pulsar «DETENER»).</summary>
    private async Task ShowLastSessionResultAsync() {
        var result = _realisticSessionManager.LastSessionResult;
        if (result is null) return;
        await LoadDashboardAsync(SelectedAircraft!.Id);
        await ShowSessionResultByIdAsync(result.SessionId);
    }

    /// <summary>Re-abre el resultado de una sesión del historial (paso 2).</summary>
    public RelayCommand<int> ShowSessionResultFromHistoryCommand { get; }

    public async Task ShowSessionResultFromHistoryAsync(int sessionId) =>
        await ShowSessionResultByIdAsync(sessionId);

    #endregion

    private void RealisticSessionManager_FailureTriggered(object? sender, FailureTriggeredEventArgs e) {
        System.Windows.Application.Current?.Dispatcher!.InvokeAsync(() => {
            LiveTriggeredFailures.Insert(0, new LiveFailureEntryViewModel {
                FenixFailureId = e.FenixFailureId,
                TimestampText = $"{e.TriggeredAtUtc.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)} UTC",
                Description = e.Description
            });
            TrimLiveFailures();
        });
    }

    /// <summary>
    /// Una falla del feed cambió de estado confirmado por Fenix (enviada → activa, activa → resuelta).
    /// Actualiza la entrada existente; si la falla no está en el feed (por ejemplo, llegó una transición
    /// de una falla de cuya entrada ya se purgó), la inserta con su estado actual.
    /// </summary>
    private void RealisticSessionManager_FailureStateChanged(object? sender, FailureStateChangedEventArgs e) {
        System.Windows.Application.Current?.Dispatcher!.InvokeAsync(() => {
            var entry = LiveTriggeredFailures.FirstOrDefault(f => f.FenixFailureId == e.FenixFailureId);
            if (entry is not null) {
                entry.ApplyState(e.NewState, e.Severity);
            } else {
                LiveTriggeredFailures.Insert(0, new LiveFailureEntryViewModel {
                    FenixFailureId = e.FenixFailureId,
                    TimestampText = $"{e.OccurredAtUtc.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)} UTC",
                    Description = e.Description
                });
                TrimLiveFailures();
            }
        });
    }

    private void TrimLiveFailures() {
        while (LiveTriggeredFailures.Count > MaxLiveTriggeredFailures) {
            LiveTriggeredFailures.RemoveAt(LiveTriggeredFailures.Count - 1);
        }
    }

    private void RealisticSessionManager_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            if (e.PropertyName == nameof(IRealisticSessionManager.IsPaused)) {
                IsSessionPaused = _realisticSessionManager.IsPaused;
            }
            if (e.PropertyName == nameof(IRealisticSessionManager.PreparedCount)) {
                RefreshPreparedState();
            }
        });
    }

    private async Task DiscardPreparedAsync() {
        if (!IsDiscardPreparedVisible) return;
        try {
            await _realisticSessionManager.DiscardPreparedAsync(CancellationToken.None);
            RefreshPreparedState();
            _logger.LogInformation("Prepared failures discarded by user.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al descartar fallas preparadas.");
        }
    }

    private void RefreshPreparedState() {
        OnPropertyChanged(nameof(PreparedCount));
        OnPropertyChanged(nameof(IsDiscardPreparedVisible));
        OnPropertyChanged(nameof(DiscardPreparedLabel));
        DiscardPreparedCommand.RaiseCanExecuteChanged();
    }

    private async Task ResumeSessionAsync() {
        if (!IsSessionPaused) return;
        try {
            await _realisticSessionManager.ResumeAsync(CancellationToken.None, RestoreFailuresOnResume);
            _logger.LogInformation("Sesión Realista reanudada manualmente{restore}.", RestoreFailuresOnResume ? " (con restauración de fallas)" : string.Empty);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al reanudar la sesión Realista.");
        }
    }

    private async Task PauseSessionAsync() {
        if (!IsEngineActive || IsSessionPaused) return;
        try {
            await _realisticSessionManager.PauseAsync(CancellationToken.None);
            _logger.LogInformation("Sesión Realista pausada manualmente: loop detenido y fallas activas desarmadas.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error al pausar la sesión Realista.");
        }
    }

    private void Orchestrator_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            if (e.PropertyName == nameof(IEngineOrchestrator.ConnectionStatus)) {
                SyncConnectionFromOrchestrator();
            } else if (e.PropertyName == nameof(IEngineOrchestrator.CurrentFlightPhase)) {
                CurrentFlightPhase = _orchestrator.CurrentFlightPhase;
            } else if (e.PropertyName == nameof(IEngineOrchestrator.IsEngineActive) ||
                       e.PropertyName == nameof(IEngineOrchestrator.CurrentMode)) {
                IsEngineActive = _orchestrator.IsEngineActive;
            } else if (e.PropertyName == nameof(IEngineOrchestrator.SimulatedTimeScale)) {
                OnPropertyChanged(nameof(SimulatedTimeScaleDisplay));
            }
        });
    }

    private void SyncConnectionFromOrchestrator() {
        var status = _orchestrator.ConnectionStatus;
        if (status is null) return;
        var phase = _orchestrator.CurrentFlightPhase;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
            IsSimConnected = status.IsSimConnectConnected;
            IsFenixConnected = status.IsFenixConnected;
            HasAircraftData = status.HasAircraftData;
            SimpleFlightPhase = status.CurrentFlightPhase;
            CurrentFlightPhase = phase;
        });
    }

    private void RaiseActivationState() {
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(ActivationBlockReason));
        OnPropertyChanged(nameof(HasActivationBlockReason));
        OnPropertyChanged(nameof(ActivateButtonText));
        OnPropertyChanged(nameof(ActionToolTip));
        OnPropertyChanged(nameof(CanChangeProfile));
        OnPropertyChanged(nameof(RiskProfileButtonText));
        OnPropertyChanged(nameof(ProfileCardSubtitle));
        OnPropertyChanged(nameof(OfpButtonToolTip));
        OnPropertyChanged(nameof(IsEngineActive));
        OnPropertyChanged(nameof(IsOnGround));
        OnPropertyChanged(nameof(CanNavigateToStep1));
        OnPropertyChanged(nameof(CanNavigateToStep2));
        OnPropertyChanged(nameof(CanNavigateToStep3));
        OnPropertyChanged(nameof(IsSessionActiveWithoutFenix));
        OnPropertyChanged(nameof(IsDiscardPreparedVisible));
        ActivateCommand.RaiseCanExecuteChanged();
        BackToStep1Command.RaiseCanExecuteChanged();
        BackToStep2Command.RaiseCanExecuteChanged();
        NavigateToStepCommand.RaiseCanExecuteChanged();
        SelectAircraftCommand.RaiseCanExecuteChanged();
        SelectRiskCommand.RaiseCanExecuteChanged();
        ContinueToPrepCommand.RaiseCanExecuteChanged();
        ResumeSessionCommand.RaiseCanExecuteChanged();
        CancelStopCommand.RaiseCanExecuteChanged();
        DiscardPreparedCommand.RaiseCanExecuteChanged();
        PauseSessionCommand.RaiseCanExecuteChanged();
        DownloadOfpCommand.RaiseCanExecuteChanged();
        DeleteOfpCommand.RaiseCanExecuteChanged();
    }

    #endregion

    public void Dispose() {
        _orchestrator.PropertyChanged -= Orchestrator_PropertyChanged;
        _realisticSessionManager.PropertyChanged -= RealisticSessionManager_PropertyChanged;
        _realisticSessionManager.FailureTriggered -= RealisticSessionManager_FailureTriggered;
        _realisticSessionManager.FailureStateChanged -= RealisticSessionManager_FailureStateChanged;
    }
}
