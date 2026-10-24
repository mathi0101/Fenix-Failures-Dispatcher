using RealFenixFailures.Integrations.SimConnect.Models;

namespace RealFenixFailures.TestUnits.Integrations;

public class AircraftStateDataValidatorTests {
    private static AircraftStateData ParkedAircraft() => new() {
        Latitude = -34.8384,
        Longitude = -56.0308,
        PlaneAltitude = 105,
        IndicatedAltitude = 105,
        Heading = 70,
        GroundSpeed = 0,
        AirspeedTrue = 0,
        AirspeedIndicated = 0,
        VerticalSpeed = 0,
        IsOnGround = 1,
        FlapsHandleIndex = 0,
        Engine1Combustion = 0,
        Engine2Combustion = 0,
        Engine1N1Percent = 0,
        Engine2N1Percent = 0,
        ThrottlePercent1 = 0,
        ThrottlePercent2 = 0,
        RadioHeight = 0,
    };

    [Fact]
    public void IsValid_StationaryParkedAircraft_IsValid() {
        Assert.True(AircraftStateDataValidator.IsValid(ParkedAircraft()));
    }

    [Fact]
    public void IsValid_ParkedAircraftAtZeroAltitude_IsValid() {
        var frame = ParkedAircraft();
        frame.PlaneAltitude = 0;
        frame.IndicatedAltitude = 0;

        Assert.True(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_EmptyFrame_IsInvalid() {
        Assert.False(AircraftStateDataValidator.IsValid(new AircraftStateData()));
    }

    [Fact]
    public void IsValid_LogShapedFrame_IsInvalid() {
        var frame = new AircraftStateData {
            Latitude = -33.396,
            Longitude = 6.8056673096445E+38,
            PlaneAltitude = 0,
            IndicatedAltitude = 1.27319747695E-313,
            Heading = 0,
            GroundSpeed = 0,
            AirspeedTrue = 0,
            AirspeedIndicated = 0,
            VerticalSpeed = -33,
            IsOnGround = 0,
            FlapsHandleIndex = 2147483647,
            Engine1Combustion = -2013192960,
            Engine2Combustion = -992642256,
            Engine1N1Percent = 0,
            Engine2N1Percent = -272885760,
            ThrottlePercent1 = 0,
            ThrottlePercent2 = 0,
            RadioHeight = -2147483648,
        };

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_BoolOutsideZeroOrOne_IsInvalid() {
        var frame = ParkedAircraft();
        frame.Engine1Combustion = 2;

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_OnGroundFlagOutsideZeroOrOne_IsInvalid() {
        var frame = ParkedAircraft();
        frame.IsOnGround = 2;

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_InvalidRadioHeight_IsInvalid() {
        var frame = ParkedAircraft();
        frame.RadioHeight = -2147483648;

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_InvalidLongitude_IsInvalid() {
        var frame = ParkedAircraft();
        frame.Longitude = 200;

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }

    [Fact]
    public void IsValid_NaNAndInfinity_AreInvalid() {
        var nanFrame = ParkedAircraft();
        nanFrame.Latitude = double.NaN;

        var infinityFrame = ParkedAircraft();
        infinityFrame.Longitude = double.PositiveInfinity;

        Assert.False(AircraftStateDataValidator.IsValid(nanFrame));
        Assert.False(AircraftStateDataValidator.IsValid(infinityFrame));
    }

    [Fact]
    public void IsValid_SubnormalDouble_IsInvalid() {
        var frame = ParkedAircraft();
        frame.IndicatedAltitude = 1.27319747695E-313;

        Assert.False(AircraftStateDataValidator.IsValid(frame));
    }
}
