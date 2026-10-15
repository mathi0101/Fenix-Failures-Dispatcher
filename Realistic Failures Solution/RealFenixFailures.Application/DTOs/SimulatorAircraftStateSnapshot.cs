using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.DTOs;

public class SimulatorAircraftStateSnapshot {

    public SimpleFlightPhaseEnum FlightPhase { get; init; } = SimpleFlightPhaseEnum.Disconnected;
    public bool HasSimulatorData { get; init; }

    // Datos de posición y movimiento
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    public int AltitudeMSL { get; init; } // Mean Sea Level
    public double IndicatedAltitude { get; init; }
    public int AltitudeAGL { get; init; }

    public int Heading { get; init; } // grados
    public int VerticalSpeed { get; init; } // pies/minuto

    public int GroundSpeed { get; init; } // nudos
    public int TrueAirspeed { get; init; } // nudos
    public int AirspeedIndicated { get; init; } // nudos

    // Estado de vuelo
    public bool IsOnGround { get; init; }
    public int FlapsHandleIndex { get; init; }


    private const double N1_RUNNING_THRESHOLD = 5.0; // Ajustado: el Fenix idle N1 es ~19-21%

    // Motores
    public bool Engine1Combustion { get; init; }
    public bool Engine2Combustion { get; init; }
    public double Engine1N1Percent { get; init; }
    public double Engine2N1Percent { get; init; }
    public double ThrottlePercent1 { get; init; }
    public double ThrottlePercent2 { get; init; }

    public bool Engine1IsRunning => Engine1Combustion && Engine1N1Percent >= N1_RUNNING_THRESHOLD;
    public bool Engine2IsRunning => Engine2Combustion && Engine2N1Percent >= N1_RUNNING_THRESHOLD;
    public bool AreBothEnginesRunning => Engine1IsRunning && Engine2IsRunning;

    public DateTimeOffset ObservedAt { get; init; }


    public SimulatorAircraftStateSnapshot() {
        ObservedAt = DateTimeOffset.UtcNow;
    }

    // Método para clonar el estado (útil para evitar referencias compartidas)
    public SimulatorAircraftStateSnapshot Clone() {
        return new SimulatorAircraftStateSnapshot() {
            HasSimulatorData = HasSimulatorData,
            Latitude = Latitude,
            Longitude = Longitude,
            AltitudeMSL = AltitudeMSL,
            IndicatedAltitude = IndicatedAltitude,
            AltitudeAGL = AltitudeAGL,
            Heading = Heading,
            GroundSpeed = GroundSpeed,
            TrueAirspeed = TrueAirspeed,
            AirspeedIndicated = AirspeedIndicated,
            VerticalSpeed = VerticalSpeed,
            IsOnGround = IsOnGround,
            FlapsHandleIndex = FlapsHandleIndex,
            ThrottlePercent1 = ThrottlePercent1,
            ThrottlePercent2 = ThrottlePercent2,
            Engine1Combustion = Engine1Combustion,
            Engine2Combustion = Engine2Combustion,
            Engine1N1Percent = Engine1N1Percent,
            Engine2N1Percent = Engine2N1Percent
        };
    }
}