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

    private ComplexFlightPhaseEnum _previousPhase = ComplexFlightPhaseEnum.Unknown;
    private bool _enginesWereRunning;
    private bool _hasLandedThisFlight;
    private int? _lastAltitudeMSL;
    #endregion

    public ComplexFlightPhaseEnum CurrentPhase { get; private set; } = ComplexFlightPhaseEnum.Unknown;

    public event EventHandler<FlightEventDto>? FlightEventDetected;

    public FlightEventDetector(ILogger<FlightEventDetector> logger) {
        _logger = logger;
    }

    public ComplexFlightPhaseEnum ProcessSnapshot(SimulatorAircraftStateSnapshot snapshot) {
        DetectEngineTransition(snapshot);
        var newPhase = DeterminePhase(snapshot);
        DetectPhaseTransition(newPhase);
        DetectAltitudeCrossings(snapshot);

        _lastAltitudeMSL = snapshot.AltitudeMSL;
        CurrentPhase = newPhase;
        return newPhase;
    }

    public void Reset() {
        _previousPhase = ComplexFlightPhaseEnum.Unknown;
        _enginesWereRunning = false;
        _hasLandedThisFlight = false;
        _lastAltitudeMSL = null;
        CurrentPhase = ComplexFlightPhaseEnum.Unknown;
    }

    #region Private

    private static ComplexFlightPhaseEnum DeterminePhase(SimulatorAircraftStateSnapshot state) {
        if (state.IsOnGround) {
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

    private void DetectEngineTransition(SimulatorAircraftStateSnapshot state) {
        var anyEngineRunning = state.Engine1IsRunning || state.Engine2IsRunning;
        if (anyEngineRunning && !_enginesWereRunning) {
            Raise(FlightEventKind.EnginesStarted, "Motores encendidos");
        } else if (!anyEngineRunning && _enginesWereRunning) {
            Raise(FlightEventKind.EnginesStopped, "Motores apagados");
        }
        _enginesWereRunning = anyEngineRunning;
    }

    private void DetectPhaseTransition(ComplexFlightPhaseEnum newPhase) {
        if (newPhase == _previousPhase) return;

        if (_previousPhase != ComplexFlightPhaseEnum.Unknown) {
            var message = DescribePhase(newPhase);
            if (message != null) Raise(FlightEventKind.PhaseChanged, message);
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

    private void DetectAltitudeCrossings(SimulatorAircraftStateSnapshot state) {
        if (_lastAltitudeMSL is not int previousAltitude) return;

        foreach (var threshold in AltitudeThresholdsFt) {
            if (previousAltitude < threshold && state.AltitudeMSL >= threshold) {
                Raise(FlightEventKind.AltitudeCrossed, $"Cruzando FL{threshold / 100} en ascenso");
            } else if (previousAltitude > threshold && state.AltitudeMSL <= threshold) {
                Raise(FlightEventKind.AltitudeCrossed, $"Cruzando FL{threshold / 100} en descenso");
            }
        }
    }

    private void Raise(FlightEventKind kind, string message) {
        var timestamp = DateTime.UtcNow;
        _logger.LogInformation("Flight event detected: {Kind} - {Message}", kind, message);
        FlightEventDetected?.Invoke(this, new FlightEventDto(timestamp, kind, message));
    }

    #endregion
}
