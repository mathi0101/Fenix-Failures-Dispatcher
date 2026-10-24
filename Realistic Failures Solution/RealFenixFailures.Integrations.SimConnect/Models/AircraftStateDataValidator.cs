namespace RealFenixFailures.Integrations.SimConnect.Models;

public static class AircraftStateDataValidator {
    private const double MinNormalDouble = 2.2250738585072014E-308;
    private const double MaxMslAltitudeFeet = 65000;
    private const double MinMslAltitudeFeet = -2000;
    private const double MaxAglFeet = 65000;
    private const double MinAglFeet = -500;
    private const double MaxGroundSpeedKnots = 800;
    private const double MaxTrueAirspeedKnots = 1000;
    private const double MaxIndicatedAirspeedKnots = 500;
    private const double MaxVerticalSpeedFpm = 10000;
    private const double MaxN1Percent = 110;
    private const double MaxThrottlePercent = 110;
    private const double MaxFlapsHandleIndex = 10;

    public static bool IsValid(AircraftStateData data) {
        if (!IsUsableDouble(data.Latitude) || data.Latitude < -90 || data.Latitude > 90) return false;
        if (!IsUsableDouble(data.Longitude) || data.Longitude < -180 || data.Longitude > 180) return false;
        if (!IsUsableDouble(data.PlaneAltitude) || data.PlaneAltitude < MinMslAltitudeFeet || data.PlaneAltitude > MaxMslAltitudeFeet) return false;
        if (!IsUsableDouble(data.IndicatedAltitude) || data.IndicatedAltitude < MinMslAltitudeFeet || data.IndicatedAltitude > MaxMslAltitudeFeet) return false;
        if (!IsUsableDouble(data.Heading) || data.Heading < 0 || data.Heading > 360) return false;
        if (!IsUsableDouble(data.GroundSpeed) || data.GroundSpeed < 0 || data.GroundSpeed > MaxGroundSpeedKnots) return false;
        if (!IsUsableDouble(data.AirspeedTrue) || data.AirspeedTrue < 0 || data.AirspeedTrue > MaxTrueAirspeedKnots) return false;
        if (!IsUsableDouble(data.AirspeedIndicated) || data.AirspeedIndicated < 0 || data.AirspeedIndicated > MaxIndicatedAirspeedKnots) return false;
        if (!IsUsableDouble(data.VerticalSpeed) || data.VerticalSpeed < -MaxVerticalSpeedFpm || data.VerticalSpeed > MaxVerticalSpeedFpm) return false;
        if (data.IsOnGround != 0 && data.IsOnGround != 1) return false;
        if (!IsUsableDouble(data.FlapsHandleIndex) || data.FlapsHandleIndex < 0 || data.FlapsHandleIndex > MaxFlapsHandleIndex) return false;
        if (data.Engine1Combustion != 0 && data.Engine1Combustion != 1) return false;
        if (data.Engine2Combustion != 0 && data.Engine2Combustion != 1) return false;
        if (!IsUsableDouble(data.Engine1N1Percent) || data.Engine1N1Percent < 0 || data.Engine1N1Percent > MaxN1Percent) return false;
        if (!IsUsableDouble(data.Engine2N1Percent) || data.Engine2N1Percent < 0 || data.Engine2N1Percent > MaxN1Percent) return false;
        if (!IsUsableDouble(data.ThrottlePercent1) || data.ThrottlePercent1 < 0 || data.ThrottlePercent1 > MaxThrottlePercent) return false;
        if (!IsUsableDouble(data.ThrottlePercent2) || data.ThrottlePercent2 < 0 || data.ThrottlePercent2 > MaxThrottlePercent) return false;
        if (!IsUsableDouble(data.RadioHeight) || data.RadioHeight < MinAglFeet || data.RadioHeight > MaxAglFeet) return false;
        if (IsEmptyFrame(data)) return false;
        return true;
    }

    private static bool IsEmptyFrame(AircraftStateData data) =>
        data.Latitude == 0 &&
        data.Longitude == 0 &&
        data.PlaneAltitude == 0 &&
        data.IndicatedAltitude == 0 &&
        data.Heading == 0 &&
        data.GroundSpeed == 0 &&
        data.AirspeedTrue == 0 &&
        data.AirspeedIndicated == 0 &&
        data.VerticalSpeed == 0 &&
        data.IsOnGround == 0 &&
        data.FlapsHandleIndex == 0 &&
        data.Engine1Combustion == 0 &&
        data.Engine2Combustion == 0 &&
        data.Engine1N1Percent == 0 &&
        data.Engine2N1Percent == 0 &&
        data.ThrottlePercent1 == 0 &&
        data.ThrottlePercent2 == 0 &&
        data.RadioHeight == 0;

    private static bool IsUsableDouble(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) return false;
        if (value != 0 && Math.Abs(value) < MinNormalDouble) return false;
        return true;
    }
}
