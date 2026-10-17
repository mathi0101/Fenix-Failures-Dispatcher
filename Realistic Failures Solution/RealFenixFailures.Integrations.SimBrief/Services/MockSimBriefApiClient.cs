using Microsoft.Extensions.Logging;
using RealFenixFailures.Integrations.SimBrief.Interfaces;
using System.Text;

namespace RealFenixFailures.Integrations.SimBrief.Services;

/// <summary>Mock de <see cref="ISimBriefApiClient"/> para DEBUG: genera un OFP sintético.</summary>
public sealed class MockSimBriefApiClient : ISimBriefApiClient {
    private readonly ILogger<MockSimBriefApiClient> _logger;

    public MockSimBriefApiClient(ILogger<MockSimBriefApiClient> logger) {
        _logger = logger;
    }

    public Task<Stream> FetchOfpXmlAsync(string simbriefUserId, CancellationToken ct) {
        _logger.LogInformation("SimBrief mock: generando OFP sintético para userid {UserId}.", simbriefUserId);
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <OFP>
              <general>
                <icao_airline>EZY</icao_airline>
                <flight_number>123</flight_number>
                <aircraft_icao>A320</aircraft_icao>
                <speed>450</speed>
                <level>A330</level>
                <est_time_enroute_minutes>95</est_time_enroute_minutes>
              </general>
              <origin>
                <icao_code>LFBD</icao_code>
                <name>Bordeaux Mérignac</name>
              </origin>
              <destination>
                <icao_code>LIRF</icao_code>
                <name>Roma Fiumicino</name>
              </destination>
              <times>
                <orig>2026-10-17 06:30:00</orig>
                <dest>2026-10-17 08:05:00</dest>
                <etd>2026-10-17 06:30:00</etd>
                <eta>2026-10-17 08:05:00</eta>
              </times>
              <route>
                <text>LFBD SID/STAR BORDX LIRF</text>
              </route>
            </OFP>
            """;
        return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }
}
