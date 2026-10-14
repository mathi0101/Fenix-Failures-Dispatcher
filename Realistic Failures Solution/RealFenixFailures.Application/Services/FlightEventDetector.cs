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

    #region Fields
    private readonly ILogger<FlightEventDetector> _logger;
    private readonly object _sync = new();

    private ComplexFlightPhaseEnum _previousPhase = ComplexFlightPhaseEnum.Unknown;
    private bool _enginesWereRunning;
    private bool _hasLandedThisFlight;
    private int? _lastAltitudeMSL;
    private int _generation;
    private ComplexFlightPhaseEnum _currentPhase = ComplexFlightPhaseEnum.Unknown;
    #endregion

    public ComplexFlightPhaseEnum CurrentPhase {
        get {
            lock (_sync) return _currentPhase;
        }
    }

    public event EventHandler<FlightEventDto>? FlightEventDetected;

    public FlightEventDetector(ILogger<FlightEventDetector> logger) {
        _logger = logger;
    }

    public ComplexFlightPhaseEnum ProcessSnapshot(SimulatorAircraftStateSnapshot snapshot) {
        List<FlightEventDto> events = [];
        int generation;
        ComplexFlightPhaseEnum newPhase;
        lock (_sync) {
            generation = _generation;
            DetectEngineTransition(snapshot, events);
            newPhase = DeterminePhase(snapshot);
            DetectPhaseTransition(newPhase, events);
            DetectAltitudeCrossings(snapshot, events);

            _lastAltitudeMSL = snapshot.AltitudeMSL;
            _currentPhase = newPhase;
        }

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
        }
    }

    #region Private

    private ComplexFlightPhaseEnum DeterminePhase(SimulatorAircraftStateSnapshot state) {
        if (state.IsOnGround) {
            if (IsAirbornePhase(_previousPhase))
                return ComplexFlightPhaseEnum.Landing;

            if (state.Engine1IsRunning || state.Engine2IsRunning) {
                if (state.GroundSpeed > 5)
                    return ComplexFlightPhaseEnum.Taxi;
                if (state.ThrottlePercent1 > 80 || state.ThrottlePercent2 > 80)
                    return ComplexFlightPhaseEnum.Takeoff;
                return ComplexFlightPhaseEnum.Parked;
            }
            return ComplexFlightPhaseEnum.OnGate;
        }

        if (state.AltitudeAGL < 5000) {
            if (state.VerticalSpeed > 500) return ComplexFlightPhaseEnum.Climb;
            if (state.VerticalSpeed < -500) return ComplexFlightPhaseEnum.Approach;
            return ComplexFlightPhaseEnum.Cruise;
        }

        if (Math.Abs(state.VerticalSpeed) < 100) return ComplexFlightPhaseEnum.Cruise;
        if (state.VerticalSpeed > 200) return ComplexFlightPhaseEnum.Climb;
        return ComplexFlightPhaseEnum.Descent;
    }

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
