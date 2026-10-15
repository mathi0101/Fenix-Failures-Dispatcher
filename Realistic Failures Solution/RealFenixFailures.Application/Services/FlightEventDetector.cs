using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Services;

/// <inheritdoc cref="IFlightEventDetector"/>
public class FlightEventDetector : IFlightEventDetector {
    #region Constants
    /// <summary>Altitude bands (feet MSL) that trigger an "altitude crossed" event while climbing or descending.</summary>
    private static readonly int[] AltitudeThresholdsFt = [10000, 18000, 24000, 35000];
    #endregion

    // Debe coincidir con SerilogExtensions.SimConnectDiagnosticsSourceContext (Infrastructure),
    // que rutea esta categoría al log de diagnóstico cuando "SimConnectDiagnostics:Enabled" es true.
    private const string DiagnosticsSourceContext = "SimConnect.Diagnostics";

    #region Fields
    private readonly ILogger<FlightEventDetector> _logger;
    private readonly ILogger _diagnosticsLogger;
    private readonly object _sync = new();

    private ComplexFlightPhaseEnum _previousPhase = ComplexFlightPhaseEnum.Unknown;
    private bool _enginesWereRunning;
    private bool _hasLandedThisFlight;
    private int? _lastAltitudeMSL;
    private int _generation;
    private ComplexFlightPhaseEnum _currentPhase = ComplexFlightPhaseEnum.Unknown;
    private ComplexFlightPhaseEnum? _pendingDebouncePhase;
    private DateTimeOffset? _pendingDebounceSince;
    #endregion

    public ComplexFlightPhaseEnum CurrentPhase {
        get {
            lock (_sync) return _currentPhase;
        }
    }

    public event EventHandler<FlightEventDto>? FlightEventDetected;

    public FlightEventDetector(ILogger<FlightEventDetector> logger, ILoggerFactory loggerFactory) {
        _logger = logger;
        _diagnosticsLogger = loggerFactory.CreateLogger(DiagnosticsSourceContext);
    }

    public ComplexFlightPhaseEnum ProcessSnapshot(SimulatorAircraftStateSnapshot snapshot) {
        List<FlightEventDto> events = [];
        int generation;
        ComplexFlightPhaseEnum newPhase;
        lock (_sync) {
            generation = _generation;
            if (!snapshot.HasSimulatorData) {
                _pendingDebouncePhase = null;
                _pendingDebounceSince = null;
                return _currentPhase;
            }

            DetectEngineTransition(snapshot, events);
            newPhase = DeterminePhase(snapshot);
            newPhase = ApplyPhaseDebounce(snapshot.ObservedAt, newPhase);
            DetectPhaseTransition(newPhase, events);
            DetectAltitudeCrossings(snapshot, events);

            _lastAltitudeMSL = snapshot.AltitudeMSL;
            _currentPhase = newPhase;
        }

        _diagnosticsLogger.LogDebug(
            "Computed state: Phase={Phase} AltMSL={AltitudeMSL} AltAGL={AltitudeAGL} VS={VerticalSpeed} GS={GroundSpeed} TAS={TrueAirspeed} IAS={AirspeedIndicated} " +
            "OnGround={IsOnGround} Eng1Running={Engine1IsRunning} Eng2Running={Engine2IsRunning} BothEnginesRunning={AreBothEnginesRunning}",
            newPhase, snapshot.AltitudeMSL, snapshot.AltitudeAGL, snapshot.VerticalSpeed, snapshot.GroundSpeed, snapshot.TrueAirspeed,
            snapshot.AirspeedIndicated, snapshot.IsOnGround, snapshot.Engine1IsRunning, snapshot.Engine2IsRunning, snapshot.AreBothEnginesRunning);

        if (generation == _generation) {
            foreach (var flightEvent in events) {
                Raise(flightEvent);
            }
        }
        return newPhase;
    }

    public void Reset() {
        lock (_sync) {
            _generation++;
            _previousPhase = ComplexFlightPhaseEnum.Unknown;
            _enginesWereRunning = false;
            _hasLandedThisFlight = false;
            _lastAltitudeMSL = null;
            _currentPhase = ComplexFlightPhaseEnum.Unknown;
            _pendingDebouncePhase = null;
            _pendingDebounceSince = null;
        }
    }

    #region Private

    private ComplexFlightPhaseEnum DeterminePhase(SimulatorAircraftStateSnapshot state) {
        if (state.IsOnGround) {
            if (IsAirbornePhase(_previousPhase))
                return ComplexFlightPhaseEnum.Landing;

            if (state.Engine1IsRunning || state.Engine2IsRunning) {
                if ((state.ThrottlePercent1 > 80 || state.ThrottlePercent2 > 80) && state.GroundSpeed > 50)
                    return ComplexFlightPhaseEnum.Takeoff;
                if (state.GroundSpeed > 5)
                    return ComplexFlightPhaseEnum.Taxi;
                return ComplexFlightPhaseEnum.Parked;
            }
            return ComplexFlightPhaseEnum.OnGate;
        }

        if (_hasLandedThisFlight)
            return _previousPhase;

        if (state.AltitudeAGL < 5000) {
            if (state.VerticalSpeed > 200) return ComplexFlightPhaseEnum.Climb;
            if (state.VerticalSpeed < -200) return ComplexFlightPhaseEnum.Approach;
            return ComplexFlightPhaseEnum.Cruise;
        }

        if (state.VerticalSpeed > 200) return ComplexFlightPhaseEnum.Climb;
        if (state.VerticalSpeed < -200) return ComplexFlightPhaseEnum.Descent;
        return ComplexFlightPhaseEnum.Cruise;
    }

    private ComplexFlightPhaseEnum ApplyPhaseDebounce(DateTimeOffset observedAt, ComplexFlightPhaseEnum newPhase) {
        if (!IsDebouncedPhase(newPhase)) {
            _pendingDebouncePhase = null;
            _pendingDebounceSince = null;
            return newPhase;
        }

        if (_previousPhase == ComplexFlightPhaseEnum.Unknown) {
            _pendingDebouncePhase = null;
            _pendingDebounceSince = null;
            return newPhase;
        }

        if (newPhase == _previousPhase) {
            _pendingDebouncePhase = null;
            _pendingDebounceSince = null;
            return newPhase;
        }

        var debounce = DebounceFor(newPhase);
        if (_pendingDebouncePhase == newPhase && _pendingDebounceSince is DateTimeOffset since) {
            if (observedAt - since >= debounce) {
                _pendingDebouncePhase = null;
                _pendingDebounceSince = null;
                return newPhase;
            }
            return _previousPhase;
        }

        _pendingDebouncePhase = newPhase;
        _pendingDebounceSince = observedAt;
        return _previousPhase;
    }

    private static bool IsDebouncedPhase(ComplexFlightPhaseEnum phase) =>
        phase is ComplexFlightPhaseEnum.Climb
            or ComplexFlightPhaseEnum.Cruise
            or ComplexFlightPhaseEnum.Descent
            or ComplexFlightPhaseEnum.Approach;

    private static TimeSpan DebounceFor(ComplexFlightPhaseEnum phase) => phase switch {
        ComplexFlightPhaseEnum.Cruise => TimeSpan.FromSeconds(60),
        ComplexFlightPhaseEnum.Descent => TimeSpan.FromSeconds(45),
        ComplexFlightPhaseEnum.Climb => TimeSpan.FromSeconds(10),
        ComplexFlightPhaseEnum.Approach => TimeSpan.FromSeconds(10),
        _ => TimeSpan.Zero
    };

    private static bool IsAirbornePhase(ComplexFlightPhaseEnum phase) =>
        phase is ComplexFlightPhaseEnum.Climb
            or ComplexFlightPhaseEnum.Cruise
            or ComplexFlightPhaseEnum.Descent
            or ComplexFlightPhaseEnum.Approach;

    private void DetectEngineTransition(SimulatorAircraftStateSnapshot state, List<FlightEventDto> events) {
        var anyEngineRunning = state.Engine1IsRunning || state.Engine2IsRunning;
        if (anyEngineRunning && !_enginesWereRunning) {
            events.Add(new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStarted, "Motores encendidos"));
        } else if (!anyEngineRunning && _enginesWereRunning) {
            events.Add(new FlightEventDto(DateTime.UtcNow, FlightEventKind.EnginesStopped, "Motores apagados"));
        }
        _enginesWereRunning = anyEngineRunning;
    }

    private void DetectPhaseTransition(ComplexFlightPhaseEnum newPhase, List<FlightEventDto> events) {
        if (newPhase == _previousPhase) return;

        if (_previousPhase != ComplexFlightPhaseEnum.Unknown) {
            var message = DescribePhase(newPhase);
            if (message != null) events.Add(new FlightEventDto(DateTime.UtcNow, FlightEventKind.PhaseChanged, message));
        }

        if (newPhase == ComplexFlightPhaseEnum.Landing) _hasLandedThisFlight = true;
        if (newPhase == ComplexFlightPhaseEnum.Takeoff) _hasLandedThisFlight = false;
        if (newPhase == ComplexFlightPhaseEnum.OnGate) _hasLandedThisFlight = false;

        _previousPhase = newPhase;
    }

    private string? DescribePhase(ComplexFlightPhaseEnum phase) => phase switch {
        ComplexFlightPhaseEnum.Taxi => _hasLandedThisFlight ? "Taxi final hacia el gate" : "Iniciando rodaje (taxi)",
        ComplexFlightPhaseEnum.Takeoff => "Despegue",
        ComplexFlightPhaseEnum.Climb => "Ascenso",
        ComplexFlightPhaseEnum.Cruise => "Crucero alcanzado",
        ComplexFlightPhaseEnum.Descent => "Iniciando descenso",
        ComplexFlightPhaseEnum.Approach => "Aproximación",
        ComplexFlightPhaseEnum.Landing => "Aterrizaje",
        ComplexFlightPhaseEnum.Parked => "Aeronave parqueada",
        ComplexFlightPhaseEnum.OnGate => "Motores apagados en gate",
        _ => null
    };

    private void DetectAltitudeCrossings(SimulatorAircraftStateSnapshot state, List<FlightEventDto> events) {
        if (_lastAltitudeMSL is not int previousAltitude) return;

        foreach (var threshold in AltitudeThresholdsFt) {
            if (previousAltitude < threshold && state.AltitudeMSL >= threshold) {
                events.Add(new FlightEventDto(DateTime.UtcNow, FlightEventKind.AltitudeCrossed, $"Cruzando FL{threshold / 100} en ascenso"));
            } else if (previousAltitude > threshold && state.AltitudeMSL <= threshold) {
                events.Add(new FlightEventDto(DateTime.UtcNow, FlightEventKind.AltitudeCrossed, $"Cruzando FL{threshold / 100} en descenso"));
            }
        }
    }

    private void Raise(FlightEventDto flightEvent) {
        _logger.LogInformation("Flight event detected: {Kind} - {Message}", flightEvent.Kind, flightEvent.Message);
        FlightEventDetected?.Invoke(this, flightEvent);
    }

    #endregion
}
