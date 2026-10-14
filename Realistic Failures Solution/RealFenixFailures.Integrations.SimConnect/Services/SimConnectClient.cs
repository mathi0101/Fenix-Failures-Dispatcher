using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Models;

namespace RealFenixFailures.Integrations.SimConnect.Services;

public class SimConnectClient : ISimConnectClient, IFlightSimulationController, IDisposable {
    private Microsoft.FlightSimulator.SimConnect.SimConnect? _simConnect;
    private readonly ILogger<SimConnectClient> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ReaderWriterLockSlim _stateReadWriteLock = new();

    private CancellationTokenSource? _messagePumpCts;
    private Task? _messagePumpTask;

    private CancellationTokenSource? _dataRequestCts;
    private Task? _dataRequestTask;

    private bool _isConnected = false;
    private SimConnectAircraftState _lastKnownState = new();
    private DateTime _lastDataReceivedUtc = DateTime.MinValue;

    private const int MESSAGE_PUMP_INTERVAL_MS = 10;
    private const int DATA_REQUEST_INTERVAL_MS = 100;

    public SimConnectClient(ILogger<SimConnectClient> logger) {
        _logger = logger;
    }

    public event Action<SimConnectAircraftState>? OnAircraftStateChanged;
    public event Action<bool>? OnConnectionStateChanged;

    private enum DATA_DEFINITION {
        AircraftState
    }

    private enum DATA_REQUEST {
        RequestAircraftState
    }

    #region Public Methods

    public void StartFlightSimulation() {
    }

    public void StopFlightSimulation() {
    }

    public async Task<bool> ConnectAsync(CancellationToken ct) {
        await _connectionLock.WaitAsync(ct);
        try {
            if (_isConnected) return true;

            try {
                // IntPtr.Zero indica que no hay ventana (app de consola/background)
                _simConnect = new Microsoft.FlightSimulator.SimConnect.SimConnect(
                    "RealFenixFailures",
                    IntPtr.Zero,
                    0,
                    null,
                    0);

                RegisterSimConnectHandlers();
                RegisterDataDefinitions();

                _isConnected = true;
                _logger.LogInformation("Successfully connected to SimConnect");

                StartMessagePump();
                StartDataRequestLoop();

                OnConnectionStateChanged?.Invoke(true);
                return true;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error connecting to SimConnect");
                Disconnect();
                return false;
            }
        } finally {
            _connectionLock.Release();
        }
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct) {
        if (!_isConnected) return await ConnectAsync(ct);

        var timeSinceLastData = DateTime.UtcNow - _lastDataReceivedUtc;
        if (timeSinceLastData > TimeSpan.FromSeconds(10)) {
            _logger.LogWarning("Timeout: Reconnecting SimConnect");
            Disconnect();
            return await ConnectAsync(ct);
        }

        return _isConnected;
    }

    public Task<SimConnectAircraftState> GetAircraftStateAsync(CancellationToken ct) {
        _stateReadWriteLock.EnterReadLock();
        try {
            return Task.FromResult(_lastKnownState.Clone());
        } finally {
            _stateReadWriteLock.ExitReadLock();
        }
    }

    public void Disconnect() {
        if (!_isConnected) return;

        _isConnected = false;

        _messagePumpCts?.Cancel();
        _dataRequestCts?.Cancel();

        try {
            Task.WaitAll(new[] {
                _messagePumpTask ?? Task.CompletedTask,
                _dataRequestTask ?? Task.CompletedTask
            }, TimeSpan.FromSeconds(2));
        } catch { /* Ignorar errores de cancelación */ }

        _messagePumpCts?.Dispose();
        _dataRequestCts?.Dispose();
        _messagePumpCts = null;
        _dataRequestCts = null;

        _simConnect?.Dispose();
        _simConnect = null;

        _logger.LogInformation("Disconnected from SimConnect");
        OnConnectionStateChanged?.Invoke(false);
    }

    public void Dispose() {
        Disconnect();
        _connectionLock.Dispose();
        _stateReadWriteLock.Dispose();
    }

    #endregion

    #region Message Pump

    private void StartMessagePump() {
        _messagePumpCts = new CancellationTokenSource();
        _messagePumpTask = Task.Run(() => RunMessagePumpAsync(_messagePumpCts.Token));
    }

    private async Task RunMessagePumpAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _isConnected) {
            try {
                _simConnect?.ReceiveMessage();
            } catch (Exception ex) {
                _logger.LogError(ex, "Exception in SimConnect Message Pump");
                _isConnected = false;
            }
            await Task.Delay(MESSAGE_PUMP_INTERVAL_MS, ct);
        }
    }

    #endregion

    #region Data Request Loop

    private void StartDataRequestLoop() {
        _dataRequestCts = new CancellationTokenSource();
        _dataRequestTask = Task.Run(() => RunDataRequestLoopAsync(_dataRequestCts.Token));
    }

    private async Task RunDataRequestLoopAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _isConnected) {
            try {
                _simConnect?.RequestDataOnSimObjectType(
                    DATA_REQUEST.RequestAircraftState,
                    DATA_DEFINITION.AircraftState,
                    0,
                    SIMCONNECT_SIMOBJECT_TYPE.USER);
            } catch (Exception ex) {
                _logger.LogDebug(ex, "Error requesting data");
            }
            await Task.Delay(DATA_REQUEST_INTERVAL_MS, ct);
        }
    }

    #endregion

    #region SimConnect Handlers

    private void RegisterSimConnectHandlers() {
        if (_simConnect == null) return;
        _simConnect.OnRecvOpen += (s, d) => _logger.LogInformation("SimConnect Open");
        _simConnect.OnRecvQuit += (s, d) => _isConnected = false;
        _simConnect.OnRecvException += (s, d) => _logger.LogWarning("SimConnect Exception: {Id}", d.dwException);
        _simConnect.OnRecvSimobjectDataBytype += SimConnect_OnRecvSimobjectDataBytype;
    }

    private void SimConnect_OnRecvSimobjectDataBytype(Microsoft.FlightSimulator.SimConnect.SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data) {
        if (data.dwRequestID != (uint)DATA_REQUEST.RequestAircraftState) return;

        try {
            var aircraftData = (AircraftStateData)data.dwData[0];
            var newState = MapToAircraftState(aircraftData);

            _stateReadWriteLock.EnterWriteLock();
            try {
                _lastKnownState = newState;
                _lastDataReceivedUtc = DateTime.UtcNow;
            } finally {
                _stateReadWriteLock.ExitWriteLock();
            }

            OnAircraftStateChanged?.Invoke(newState.Clone());
        } catch (Exception ex) {
            _logger.LogError(ex, "Error processing SimConnect data");
        }
    }

    #endregion

    #region Data Definition and Mapping

    private void RegisterDataDefinitions() {
        if (_simConnect == null) return;

        // El ORDEN debe ser IDÉNTICO al AircraftStateData struct
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "PRESSURE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "INDICATED ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "GROUND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "AIRSPEED TRUE", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "VERTICAL SPEED", "feet per minute", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.INT32, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "FLAPS HANDLE INDEX", "number", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "GENERAL ENG COMBUSTION:1", "bool", SIMCONNECT_DATATYPE.INT32, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "GENERAL ENG COMBUSTION:2", "bool", SIMCONNECT_DATATYPE.INT32, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "TURB ENG N1:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "TURB ENG N1:2", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "GENERAL ENG THROTTLE LEVER POSITION:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "GENERAL ENG THROTTLE LEVER POSITION:2", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);
        _simConnect.AddToDataDefinition(DATA_DEFINITION.AircraftState, "PLANE ALT ABOVE GROUND", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, 0);

        _simConnect.RegisterDataDefineStruct<AircraftStateData>(DATA_DEFINITION.AircraftState);
    }

    private SimConnectAircraftState MapToAircraftState(AircraftStateData data) {
        return new SimConnectAircraftState {
            IsConnected = true,
            Latitude = data.Latitude,
            Longitude = data.Longitude,

            // Altitudes
            AltitudeMSL = (int)data.PressureAltitude, // Alias para compatibilidad
            IndicatedAltitude = data.IndicatedAltitude,
            RadioHeight = (int)data.RadioHeight,

            Heading = (int)data.Heading,
            GroundSpeed = (int)data.GroundSpeed,
            TrueAirspeed = (int)data.AirspeedTrue,
            VerticalSpeed = (int)data.VerticalSpeed,
            IsOnGround = data.IsOnGround == 1,
            FlapsHandleIndex = (int)data.FlapsHandleIndex,

            // Motores
            Engine1Combustion = data.Engine1Combustion,
            Engine2Combustion = data.Engine2Combustion,
            Engine1N1Percent = data.Engine1N1Percent,
            Engine2N1Percent = data.Engine2N1Percent,

            ThrottlePercent1 = data.ThrottlePercent1,
            ThrottlePercent2 = data.ThrottlePercent2
        };
    }
    #endregion
}