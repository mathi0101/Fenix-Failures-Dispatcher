using System.Runtime.InteropServices;

namespace RealFenixFailures.Integrations.SimConnect.Models;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct AircraftStateData {
    public double Latitude;
    public double Longitude;
    public double PlaneAltitude;
    public double IndicatedAltitude;
    public double Heading;
    public double GroundSpeed;
    public double AirspeedTrue;
    public double AirspeedIndicated;
    public double VerticalSpeed;
    public int IsOnGround;
    public double FlapsHandleIndex;
    public int Engine1Combustion;
    public int Engine2Combustion;
    public double Engine1N1Percent;
    public double Engine2N1Percent;
    public double ThrottlePercent1;
    public double ThrottlePercent2;
    public double RadioHeight;
}