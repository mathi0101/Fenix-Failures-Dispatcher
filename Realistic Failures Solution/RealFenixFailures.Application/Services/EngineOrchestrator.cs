using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using System.ComponentModel;

namespace RealFenixFailures.Application.Services;

public class EngineOrchestrator : IEngineOrchestrator, IDisposable {

    #region Fields

    private readonly IPresetService _presetService;
    private readonly ISimulatorConnectionService _simulatorConnectionService;
    private readonly IUserAircraftService _userAircraftService;
    private readonly IRealisticSessionManager _realisticSessionManager;
    private readonly FailureEngineSettings _settings;
    private readonly IFailureTrigger _failureTrigger;
    private readonly IFlightEventDetector _flightEventDetector;
    private readonly IFlightSimulationController _flightSimulationController;
    private readonly ILogger<EngineOrchestrator> _logger;

    private readonly object _recentLogsLock = new();
    private readonly Queue<FailureTriggerLogDto> _recentLogs = new();
    private const int MaxRecentLogs = 200;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private FailurePreset? _activePreset;

    private readonly ScaledTimeProvider _clock;
    private DateTimeOffset? _simulatedStartedAt;
    private CancellationTokenSource? _automaticTimerCts;


    private List<string> _activeScenarioFailures = new();
    #endregion

    #region TrackedProperties


    private bool isEngineActive;
    private bool isTimerRunning;
    private UserAppMode currentMode = UserAppMode.None;
    private ConnectionStatusDto connectionStatus = new(false, false, SimpleFlightPhaseEnum.OnGround);

    public bool IsEngineActive {
        get => isEngineActive;
        private set {
            if (isEngineActive != value) {
                isEngineActive = value;
                OnPropertyChanged(nameof(IsEngineActive));
            }
        }
    }

    public UserAppMode CurrentMode {
        get => currentMode;
        private set {
            if (currentMode != value) {
                currentMode = value;
                OnPropertyChanged(nameof(CurrentMode));
            }
        }
    }

    public bool IsTimerRunning {
        get => isTimerRunning;
        private set {
            if (isTimerRunning != value) {
                isTimerRunning = value;
                OnPropertyChanged(nameof(IsTimerRunning));
            }
        }
    }
    public ConnectionStatusDto ConnectionStatus {
        get => connectionStatus;
        private set {
            if (connectionStatus != value) {
                connectionStatus = value;
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }
    }

    public ComplexFlightPhaseEnum CurrentFlightPhase => _flightEventDetector.CurrentPhase;

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #endregion

    #region Constructor

    public EngineOrchestrator(
        IPresetService presetService,
        IFailureTrigger failureTrigger,
        ISimulatorConnectionService simulatorService,
        IUserAircraftService userAircraftService,
        IRealisticSessionManager realisticSessionManager,
        IFlightEventDetector flightEventDetector,
        IFlightSimulationController flightSimulationController,
        IOptions<FailureEngineSettings> settings,
        ILogger<EngineOrchestrator> logger,
        ScaledTimeProvider? simulatedClock = null) {
        _presetService = presetService;
        _simulatorConnectionService = simulatorService;
        _userAircraftService = userAircraftService;
        _realisticSessionManager = realisticSessionManager;
        _failureTrigger = failureTrigger;
        _flightEventDetector = flightEventDetector;
        _flightSimulationController = flightSimulationController;
        _settings = settings.Value;
        _logger = logger;
        _clock = simulatedClock ?? new ScaledTimeProvider();
    }
    #endregion

    #region Public API

    #region UpdaterTimer

    public async Task StartAutomaticTimerAsync(CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (IsTimerRunning)
                return;

            _automaticTimerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            IsTimerRunning = true;

            _ = Task.Run(() => RunAutomaticTimerLoopAsync(_automaticTimerCts.Token), _automaticTimerCts.Token);
        } finally {
            _operationLock.Release();
        }
    }
    public async Task UpdateConnection(CancellationToken ct) {
        ConnectionStatus = await _simulatorConnectionService.GetConnectionStatusAsync(ct);
    }
    public async Task StopAutomaticTimerAsync(CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (!IsTimerRunning)
                return;

            _automaticTimerCts?.Cancel();
            _automaticTimerCts = null;
            IsTimerRunning = false;
        } finally {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Tick del timer automático sobre el reloj de simulación: cada tick espera el intervalo en tiempo
    /// de escenario (<see cref="ScaledTimeProvider"/>). Con escala &gt; 1 los ticks se suceden más rápido
    /// en tiempo real; cambios de escala y saltos (<c>Advance</c>) se reflejan en el próximo tick.
    /// </summary>
    private async Task WaitForNextAutomaticTickAsync(CancellationToken ct) {
        var interval = TimeSpan.FromSeconds(_settings.CheckConnectionIntervalSeconds);
        var due = _clock.GetUtcNow() + interval;
        while (true) {
            var remaining = due - _clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return;

            var wallChunkMs = remaining.TotalMilliseconds / Math.Max(_clock.Scale, ScaledTimeProvider.MinScale);
            var chunk = TimeSpan.FromMilliseconds(Math.Clamp(wallChunkMs, 15, 250));
            await Task.Delay(chunk, ct);
        }
    }

    private async Task RunAutomaticTimerLoopAsync(CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                await WaitForNextAutomaticTickAsync(ct);
                if (ct.IsCancellationRequested)
                    break;

                ConnectionStatus = await _simulatorConnectionService.GetConnectionStatusAsync(ct);

                // El modo Realista ya alimenta el detector con su propio loop de evaluación (30s);
                // en los demás modos (o sin modo activo) lo alimentamos aquí para no perder eventos de vuelo.
                if ((CurrentMode != UserAppMode.Realistic || _realisticSessionManager.IsPaused) && ConnectionStatus.IsSimConnectConnected) {
                    var data = await _simulatorConnectionService.GetSimulatorData(ct);
                    if (data.IsSuccess && data.Value != null) {
                        _flightEventDetector.ProcessSnapshot(data.Value);
                        // Notificar siempre, aunque la transición no haya generado un evento de log
                        // (p. ej. la primera clasificación tras Unknown): la UI necesita la fase vigente.
                        OnPropertyChanged(nameof(CurrentFlightPhase));
                    }
                }
            }
        } catch (OperationCanceledException) {
            _logger.LogInformation("Automatic timer loop canceled.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error in automatic timer loop.");
        }
    }

    #endregion

    #region Reloj de simulación (tiempo escalado)

    /// <summary>Multiplicador de velocidad del tiempo simulado (1 = tiempo real).</summary>
    public double SimulatedTimeScale => _clock.Scale;

    public void SetSimulatedTimeScale(double scale) {
        _clock.SetScale(scale);
        _logger.LogInformation("Escala de tiempo simulado cambiada a {Scale}x", _clock.Scale);
        OnPropertyChanged(nameof(SimulatedTimeScale));
    }

    /// <summary>Salta el tiempo de escenario hacia adelante (p. ej. ir directo al descenso).</summary>
    public void AdvanceSimulatedTime(TimeSpan scenarioTime) {
        _clock.Advance(scenarioTime);
        var elapsed = _simulatedStartedAt is DateTimeOffset started
            ? $"T+{(_clock.GetUtcNow() - started):hh\\:mm\\:ss}"
            : "sin escenario iniciado";
        _logger.LogInformation("Tiempo de escenario adelantado {Delta}. Posición: {Elapsed} (escala {Scale}x)", scenarioTime, elapsed, _clock.Scale);
        OnPropertyChanged(nameof(SimulatedTimeScale));
    }

    private void MarkSimulatedStart() {
        _simulatedStartedAt = _clock.GetUtcNow();
        _logger.LogInformation("Escenario de simulación iniciado en T0 (escala {Scale}x).", _clock.Scale);
    }

    #endregion

    #region Realistic Mode

    public async Task StartRealisticModeAsync(int aircraftId, RiskLevel risk, int flightPlanId, CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (!ConnectionStatus.IsSimConnectConnected) {
                _logger.LogInformation("Tried to start Realistic mode but SimConnect is disconnected: {@state}", connectionStatus);
                return;
            }
            if (IsEngineActive && CurrentMode != UserAppMode.None) {
                _logger.LogInformation("Tried to start Realistic mode but another mode is already running");
                return;
            }

            var aircraft = await _userAircraftService.GetByIdAsync(aircraftId, ct);
            if (aircraft == null) {
                _logger.LogWarning("User Aircraft id: {a} Not Found", aircraftId);
                return;
            }

            //var systemWears = await _userAircraftService.GetSystemWearsAsync(aircraft.Id, ct);
            var context = new RealisticModeContext(
                risk,
                aircraft,
                flightPlanId
            );


            var sessionResult = await _realisticSessionManager.StartNewSessionAsync(context, ct);
            if (!sessionResult.IsSuccess) {
                _logger.LogWarning(sessionResult.Error, "Realistic mode could not start: {ErrorMessage}", sessionResult.ErrorMessage);
                return;
            }

            CurrentMode = UserAppMode.Realistic;
            IsEngineActive = true;
            _flightSimulationController.StartFlightSimulation();
            MarkSimulatedStart();

            _logger.LogInformation("Realistic mode started for aircraft {Registration}. RealisticSessionManager will select from available presets.",
                aircraft.Registration);
        } finally {
            _operationLock.Release();
        }
    }

    #endregion

    #region Training

    public async Task StartTrainingPresetAsync(int presetId, CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (!ConnectionStatus.IsFenixConnected) {
                _logger.LogInformation("Tried to start Training preset but Fenix is disconnected: {@state}", connectionStatus);
                return;
            }
            if (IsEngineActive && CurrentMode != UserAppMode.None) {
                _logger.LogWarning("Cannot start training preset: another mode is already running");
                return;
            }

            _activePreset = await _presetService.GetByIdAsync(presetId, ct);
            if (_activePreset == null) {
                _logger.LogError("Failed to load preset with ID {PresetId}", presetId);
                return;
            }

            var armedFailures = await _simulatorConnectionService.ExecutePresetAsync(_activePreset, ct);

            if (!armedFailures.IsSuccess) {
                _logger.LogError("Failed to arm training preset {PresetId} ({PresetName})", presetId, _activePreset.Name);
                AddRecentLog(new FailureTriggerLogDto(_clock.GetUtcNow().UtcDateTime, "", "Error al activar preset", _activePreset.FlightPhase, _activePreset.Name));
                return;
            }

            foreach (var def in armedFailures.Value!) {
                var log = new FailureTriggerLogDto(
                    _clock.GetUtcNow().UtcDateTime,
                    def.FenixFailureId,
                    def.FenixFailure!.Name,
                    def.Preset!.FlightPhase,
                    _activePreset.Name);
                AddRecentLog(log);
            }

            CurrentMode = UserAppMode.Training;
            IsEngineActive = true;
            _flightSimulationController.StartFlightSimulation();
            MarkSimulatedStart();

            _logger.LogInformation("Training preset {PresetId} loaded: {PresetName}",
                presetId, _activePreset.Name);
        } finally {
            _operationLock.Release();
        }
    }

    #endregion

    #region CustomPresets

    public async Task StartCustomModeAsync(int presetId, bool activateImmediately, CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (!ConnectionStatus.IsSimConnectConnected || !ConnectionStatus.IsFenixConnected) {
                _logger.LogInformation("Tried to start Custom Preset but there is a system disconnected: {@state}", connectionStatus);
                return;
            }
            if (IsEngineActive && CurrentMode != UserAppMode.None) {
                _logger.LogWarning("Cannot start custom mode: another mode is already running");
                return;
            }

            _activePreset = await _presetService.GetByIdAsync(presetId, ct);
            if (_activePreset == null) {
                _logger.LogError("Failed to load preset with ID {PresetId}", presetId);
                return;
            }

            if (activateImmediately) {
                foreach (var def in _activePreset.PresetFailureDefinitions) {
                    await _simulatorConnectionService.ExecuteFailureAsync(def, ct);
                }

            } else {
                var armedFailures = await _simulatorConnectionService.ExecutePresetAsync(_activePreset, ct);

                if (!armedFailures.IsSuccess) {
                    _logger.LogError("Failed to arm custom preset {PresetId} ({PresetName})", presetId, _activePreset.Name);
                    AddRecentLog(new FailureTriggerLogDto(_clock.GetUtcNow().UtcDateTime, "", "Error al activar preset", _activePreset.FlightPhase, _activePreset.Name));
                    return;
                }

                foreach (var def in armedFailures.Value!) {
                    var log = new FailureTriggerLogDto(
                        _clock.GetUtcNow().UtcDateTime,
                        def.FenixFailureId,
                        def.FenixFailure!.Name,
                        def.Preset!.FlightPhase,
                        _activePreset.Name);
                    AddRecentLog(log);
                }
            }

            CurrentMode = UserAppMode.Custom;
            IsEngineActive = true;
            _flightSimulationController.StartFlightSimulation();
            MarkSimulatedStart();

            _logger.LogInformation("Custom mode started with {Count} failures. Activate immediately: {Activate}",
                _activePreset.PresetFailureDefinitions.Count, activateImmediately);
        } finally {
            _operationLock.Release();
        }
    }

    #endregion

    public async Task StopCurrentModeAsync(CancellationToken ct) {
        await _operationLock.WaitAsync(ct);
        try {
            if (!IsEngineActive || CurrentMode == UserAppMode.None) {
                return;
            }

            try {
                if (CurrentMode == UserAppMode.Realistic) {
                    await _realisticSessionManager.StopAsync(ct);
                }

                await _simulatorConnectionService.ResetAllFailuresAsync(ct);
            } finally {
                _flightSimulationController.StopFlightSimulation();
                _activePreset = null;
                _activeScenarioFailures.Clear();
                _flightEventDetector.Reset();
                _simulatedStartedAt = null;

                _logger.LogInformation("Mode {Mode} stopped.", CurrentMode);

                CurrentMode = UserAppMode.None;
                IsEngineActive = false;
            }
        } finally {
            _operationLock.Release();
        }
    }

    public Task<List<FailureTriggerLogDto>> GetRecentFailuresAsync(CancellationToken ct) {
        lock (_recentLogsLock) {
            return Task.FromResult(_recentLogs.OrderByDescending(x => x.TriggeredAtUtc).Take(100).ToList());
        }
    }

    private void AddRecentLog(FailureTriggerLogDto log) {
        lock (_recentLogsLock) {
            _recentLogs.Enqueue(log);
            while (_recentLogs.Count > MaxRecentLogs) {
                _recentLogs.Dequeue();
            }
        }
    }

    #endregion

    #region Internal Logic



    #endregion

    #region Dispose

    public void Dispose() {
        _operationLock.Dispose();
    }
    #endregion
}