using RealFenixFailures.Integrations.SimBrief.Services;
using System.Text;
using System.Xml.Linq;

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
        Assert.Equal(new DateTime(2026, 10, 17, 6, 30, 0, DateTimeKind.Utc), plan.OffBlockUtc);
        Assert.Equal(new DateTime(2026, 10, 17, 8, 5, 0, DateTimeKind.Utc), plan.EstimatedArrivalUtc);
    }

    private const string UnixTimes = """
        <times>
          <est_time_enroute>7808</est_time_enroute>
          <sched_time_enroute>7020</sched_time_enroute>
          <sched_out>1792335900</sched_out>
          <sched_off>1792337100</sched_off>
          <sched_on>1792344120</sched_on>
          <sched_in>1792344600</sched_in>
          <sched_block>8700</sched_block>
          <est_out>1792335900</est_out>
          <est_off>1792337100</est_off>
          <est_on>1792344908</est_on>
          <est_in>1792345388</est_in>
          <est_block>9488</est_block>
          <orig_timezone>-3</orig_timezone>
          <dest_timezone>-3</dest_timezone>
          <taxi_out>1200</taxi_out>
          <taxi_in>480</taxi_in>
          <reserve_time>1800</reserve_time>
          <endurance>13643</endurance>
          <contfuel_time>420</contfuel_time>
          <etopsfuel_time>0</etopsfuel_time>
          <extrafuel_time>1200</extrafuel_time>
        </times>
        """;

    [Fact]
    public async Task ReadAsync_UnixTimes_MapsUtcInstantsAndDuration() {
        var plan = await ReadTimesAsync(XElement.Parse(UnixTimes));

        Assert.Equal(new DateTime(2026, 10, 18, 15, 5, 0, DateTimeKind.Utc), plan.OffBlockUtc);
        Assert.Equal(new DateTime(2026, 10, 18, 17, 35, 8, DateTimeKind.Utc), plan.EstimatedArrivalUtc);
        Assert.Equal(130, plan.EstimatedDurationMinutes);
        Assert.Equal(plan.OffBlockUtc, plan.ValidFromUtc);
        Assert.Equal(plan.EstimatedArrivalUtc, plan.ValidUntilUtc);
        Assert.Equal(DateTimeKind.Utc, plan.ValidFromUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, plan.ValidUntilUtc.Kind);
    }

    [Fact]
    public async Task ReadAsync_WithoutEstimatedArrival_UsesScheduledLanding() {
        var times = XElement.Parse(UnixTimes);
        times.Element("est_on")!.Remove();

        var plan = await ReadTimesAsync(times);

        Assert.Equal(new DateTime(2026, 10, 18, 17, 22, 0, DateTimeKind.Utc), plan.EstimatedArrivalUtc);
    }

    [Fact]
    public async Task ReadAsync_WithoutDuration_DerivesMinutesFromTimestamps() {
        var times = XElement.Parse(UnixTimes);
        times.Element("est_time_enroute")!.Remove();

        var plan = await ReadTimesAsync(times);

        Assert.Equal(150, plan.EstimatedDurationMinutes);
    }

    [Theory]
    [InlineData("0", 1970, 1, 1, 0, 0, 0)]
    [InlineData("-1", 1969, 12, 31, 23, 59, 59)]
    [InlineData(" 1792335900 ", 2026, 10, 18, 15, 5, 0)]
    [InlineData("2026-10-18 15:05:00", 2026, 10, 18, 15, 5, 0)]
    [InlineData("2026-10-18T12:05:00-03:00", 2026, 10, 18, 15, 5, 0)]
    public async Task ReadAsync_SupportedTimestampFormats_ProduceUtc(string value, int year, int month, int day, int hour, int minute, int second) {
        var plan = await ReadTimesAsync(new XElement("times", new XElement("sched_out", value)));

        Assert.Equal(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc), plan.OffBlockUtc);
        Assert.Equal(DateTimeKind.Utc, plan.OffBlockUtc.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("253402300800")]
    [InlineData("-62135596801")]
    [InlineData("9223372036854775807")]
    [InlineData("9223372036854775808")]
    public async Task ReadAsync_InvalidTimestamps_KeepExistingFallbackWithoutThrowing(string? value) {
        var before = DateTime.UtcNow;
        var plan = await ReadTimesAsync(new XElement("times",
            value is null ? null : new XElement("sched_out", value),
            value is null ? null : new XElement("est_on", value)));
        var after = DateTime.UtcNow;

        Assert.Equal(default, plan.ValidFromUtc);
        Assert.Equal(default, plan.ValidUntilUtc);
        Assert.InRange(plan.OffBlockUtc, before, after);
        Assert.InRange(plan.EstimatedArrivalUtc, before.AddMinutes(30), after.AddMinutes(30));
    }

    private static async Task<RealFenixFailures.Domain.Entities.FlightPlan> ReadTimesAsync(XElement times) {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new XElement("OFP", times).ToString()));
        return await new SimBriefOfpReader().ReadAsync(stream, CancellationToken.None);
    }
}
