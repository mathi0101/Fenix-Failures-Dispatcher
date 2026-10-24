using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Integrations.SimConnect.Interfaces;

namespace RealFenixFailures.Integrations.SimConnect.Services;

public class SimulatorFlightDataProvider : ISimFlightDataProvider {
    // Debe coincidir con SerilogExtensions.SimConnectDiagnosticsSourceContext (Infrastructure),
    // que rutea esta categoría al log de diagnóstico cuando "SimConnectDiagnostics:Enabled" es true.
    private const string DiagnosticsSourceContext = "SimConnect.Diagnostics";

    private readonly ISimConnectClient _client;
    private readonly ILogger<SimulatorFlightDataProvider> _logger;
    private readonly ILogger _diagnosticsLogger;
    private readonly SemaphoreSlim _healthLock = new(1, 1);

    private DateTime _lastHealthCheckAtUtc = DateTime.MinValue;
    private bool _lastHealthCheckResult;
    private bool _hasEverConnected;

    public SimulatorFlightDataProvider(ISimConnectClient client, ILogger<SimulatorFlightDataProvider> logger, ILoggerFactory loggerFactory) {
        _client = client;
        _logger = logger;
        _diagnosticsLogger = loggerFactory.CreateLogger(DiagnosticsSourceContext);
        _logger.LogDebug("SimulatorFlightDataProvider initialized");
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct) {
        var intervalSeconds = 10;
        var now = DateTime.UtcNow;

        if (now - _lastHealthCheckAtUtc < TimeSpan.FromSeconds(intervalSeconds)) {
            //_logger.LogDebug("Returning cached health check result: {Result}", _lastHealthCheckResult);
            return _lastHealthCheckResult;
        }

        await _healthLock.WaitAsync(ct);
        try {
            now = DateTime.UtcNow;
            if (now - _lastHealthCheckAtUtc < TimeSpan.FromSeconds(intervalSeconds)) {
                return _lastHealthCheckResult;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var isAlive = await _client.IsConnectedAsync(cts.Token);
            UpdateHealthState(isAlive);

            return _lastHealthCheckResult;
        } catch (Exception ex) {
            // Una sonda fallida es falta de disponibilidad: no interrumpe el flujo de la app.
            _logger.LogDebug(ex, "Error during health check");
            UpdateHealthState(false);
            return false;
        } finally {
            _healthLock.Release();
        }
    }

    public async Task<SimulatorAircraftStateSnapshot> GetAircraftRawData(CancellationToken ct) {
        var s = await _client.GetAircraftStateAsync(ct);

        _diagnosticsLogger.LogDebug(
            "SimConnect raw data: Lat={Latitude} Lon={Longitude} AltMSL={AltitudeMSL} IndAlt={IndicatedAltitude} AltAGL={RadioHeight} " +
            "Heading={Heading} GS={GroundSpeed} TAS={TrueAirspeed} IAS={AirspeedIndicated} VS={VerticalSpeed} OnGround={IsOnGround} Flaps={FlapsHandleIndex} " +
            "Eng1Comb={Engine1Combustion} Eng1N1={Engine1N1Percent} Eng1Thr={ThrottlePercent1} Eng2Comb={Engine2Combustion} Eng2N1={Engine2N1Percent} Eng2Thr={ThrottlePercent2}",
            Math.Round(s.Latitude, 5), Math.Round(s.Longitude, 5), s.AltitudeMSL, s.IndicatedAltitude, s.RadioHeight,
            s.Heading, s.GroundSpeed, s.TrueAirspeed, s.AirspeedIndicated, s.VerticalSpeed, s.IsOnGround, s.FlapsHandleIndex,
            s.Engine1Combustion, Math.Round(s.Engine1N1Percent, 1), Math.Round(s.ThrottlePercent1, 1), s.Engine2Combustion, Math.Round(s.Engine2N1Percent, 1), Math.Round(s.ThrottlePercent2, 1));

        return new SimulatorAircraftStateSnapshot() {
            FlightPhase = s.IsOnGround ? SimpleFlightPhaseEnum.OnGround : SimpleFlightPhaseEnum.Flying,
            HasSimulatorData = s.IsConnected,
            IsOnGround = s.IsOnGround,
            AltitudeMSL = s.AltitudeMSL,
            IndicatedAltitude = s.IndicatedAltitude,
            GroundSpeed = s.GroundSpeed,
            VerticalSpeed = s.VerticalSpeed,
            Engine1Combustion = s.Engine1Combustion == 1,
            Engine2Combustion = s.Engine2Combustion == 1,
            Engine1N1Percent = s.Engine1N1Percent,
            Engine2N1Percent = s.Engine2N1Percent,
            TrueAirspeed = s.TrueAirspeed,
            AirspeedIndicated = s.AirspeedIndicated,
            FlapsHandleIndex = s.FlapsHandleIndex,
            Heading = s.Heading,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            AltitudeAGL = s.RadioHeight,
            ThrottlePercent1 = s.ThrottlePercent1,
            ThrottlePercent2 = s.ThrottlePercent2,
        };
    }



    private void UpdateHealthState(bool isAvailable) {
        var previous = _lastHealthCheckResult;
        _lastHealthCheckResult = isAvailable;
        _lastHealthCheckAtUtc = DateTime.UtcNow;

        if (isAvailable) {
            if (!previous) {
                _hasEverConnected = true;
                _logger.LogInformation("SimConnect conectado");
            }
        } else if (previous && _hasEverConnected) {
            _logger.LogInformation("SimConnect desconectado");
        }
    }


}
