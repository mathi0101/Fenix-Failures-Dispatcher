using RealFenixFailures.Integrations.SimBrief.Services;
using System.Text;

namespace RealFenixFailures.TestUnits.Integrations;

public class SimBriefOfpReaderTests {
    private const string SampleXml = """
        <OFP>
          <general>
            <icao_airline>EZY</icao_airline>
            <flight_number>123</flight_number>
            <aircraft_icao>A320</aircraft_icao>
            <est_time_enroute_minutes>95</est_time_enroute_minutes>
          </general>
          <origin><icao_code>LFBD</icao_code><name>Bordeaux</name></origin>
          <destination><icao_code>LIRF</icao_code><name>Roma</name></destination>
          <times>
            <orig>2026-10-17 06:30:00</orig>
            <dest>2026-10-17 08:05:00</dest>
            <etd>2026-10-17 06:30:00</etd>
            <eta>2026-10-17 08:05:00</eta>
          </times>
          <route><text>LFBD SID BORDX LIRF</text></route>
        </OFP>
        """;

    [Fact]
    public async Task ReadAsync_MapsFieldOfp_ToFlightPlan() {
        var reader = new SimBriefOfpReader();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleXml));

        var plan = await reader.ReadAsync(stream, CancellationToken.None);

        Assert.Equal("A320", plan.AircraftIcaoType);
        Assert.Equal("LFBD", plan.OriginIcao);
        Assert.Equal("LIRF", plan.DestinationIcao);
        Assert.Equal("123", plan.FlightNumber);
        Assert.Equal("EZY123", plan.Callsign);
        Assert.Equal(95, plan.EstimatedDurationMinutes);
        Assert.Equal("LFBD SID BORDX LIRF", plan.RouteText);
        Assert.Equal("SimBrief", plan.Source);
        Assert.Equal(DateTimeKind.Utc, plan.OffBlockUtc.Kind);
    }
}
