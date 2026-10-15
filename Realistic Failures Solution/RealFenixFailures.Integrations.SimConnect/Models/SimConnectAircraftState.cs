namespace RealFenixFailures.Integrations.SimConnect.Models;

public class SimConnectAircraftState {
    public bool IsConnected { get; set; }

    // Datos de posición y movimiento
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int AltitudeMSL { get; set; } // Mean Sea Level
    public double IndicatedAltitude { get; set; }
    public int RadioHeight { get; set; }

    public int Heading { get; set; } // grados
    public int VerticalSpeed { get; set; } // pies/minuto

    public int GroundSpeed { get; set; } // nudos
    public int TrueAirspeed { get; set; } // nudos
    public int AirspeedIndicated { get; set; } // nudos

    // Estado de vuelo
    public bool IsOnGround { get; set; }
    public int FlapsHandleIndex { get; set; }



    public int Engine1Combustion { get; internal set; }
    public int Engine2Combustion { get; internal set; }
    public double Engine1N1Percent { get; internal set; }
    public double Engine2N1Percent { get; internal set; }
    public double ThrottlePercent1 { get; set; }
    public double ThrottlePercent2 { get; set; }

    public SimConnectAircraftState() {
        IsConnected = false;
    }

    // Método para clonar el estado (útil para evitar referencias compartidas)
    public SimConnectAircraftState Clone() {
        return new SimConnectAircraftState() {
            IsConnected = IsConnected,
            Latitude = Latitude,
            Longitude = Longitude,
            AltitudeMSL = AltitudeMSL,
            IndicatedAltitude = IndicatedAltitude,
            RadioHeight = RadioHeight,
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