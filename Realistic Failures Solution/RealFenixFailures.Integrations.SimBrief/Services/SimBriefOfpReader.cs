using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces;
using System.Globalization;
using System.Xml.Linq;

namespace RealFenixFailures.Integrations.SimBrief.Services;

/// <summary>
/// Parsea el XML del OFP de SimBrief a un <see cref="FlightPlan"/>.
/// No valida (esa es responsabilidad de la capa de aplicación): sólo mapea.
/// El formato es el de la API de SimBrief (https://www.simbrief.com/api/xml.fetcher.php).
/// </summary>
public sealed class SimBriefOfpReader : ISimBriefOfpReader {
    public async Task<FlightPlan> ReadAsync(Stream ofpStream, CancellationToken ct) {
        using var reader = new StreamReader(ofpStream);
        var content = await reader.ReadToEndAsync(ct);
        var doc = XDocument.Parse(content);

        var general = doc.Root?.Element("general");
        var origin = doc.Root?.Element("origin");
        var destination = doc.Root?.Element("destination");
        var times = doc.Root?.Element("times");
        var route = doc.Root?.Element("route");

        var aircraftIcao = general?.Element("aircraft_icao")?.Value?.Trim() ?? string.Empty;
        var originIcao = origin?.Element("icao_code")?.Value?.Trim() ?? string.Empty;
        var destIcao = destination?.Element("icao_code")?.Value?.Trim() ?? string.Empty;
        var airline = general?.Element("icao_airline")?.Value?.Trim() ?? string.Empty;
        var flightNumber = general?.Element("flight_number")?.Value?.Trim() ?? string.Empty;
        var etmin = int.TryParse(general?.Element("est_time_enroute_minutes")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ? minutes : 0;

        var offBlock = ParseUtc(times?.Element("orig")?.Value);
        var eta = ParseUtc(times?.Element("dest")?.Value);
        if (etmin <= 0 && offBlock != default && eta != default) {
            etmin = (int)(eta - offBlock).TotalMinutes;
        }

        var callsign = string.IsNullOrEmpty(flightNumber) ? null : string.IsNullOrEmpty(airline) ? flightNumber : $"{airline}{flightNumber}";

        return new FlightPlan {
            AircraftIcaoType = aircraftIcao,
            OriginIcao = originIcao,
            DestinationIcao = destIcao,
            FlightNumber = string.IsNullOrEmpty(flightNumber) ? null : flightNumber,
            Callsign = callsign,
            OffBlockUtc = offBlock == default ? DateTime.UtcNow : offBlock,
            EstimatedArrivalUtc = eta == default ? DateTime.UtcNow.AddMinutes(Math.Max(etmin, 30)) : eta,
            EstimatedDurationMinutes = etmin,
            RouteText = route?.Element("text")?.Value?.Trim(),
            RawData = content,
            Source = "SimBrief",
            DownloadedAtUtc = DateTime.UtcNow,
            ValidFromUtc = offBlock,
            ValidUntilUtc = eta,
            ValidationState = RealFenixFailures.Domain.Enums.ValidationState.Pending,
            ValidationMessage = null,
        };
    }

    private static DateTime ParseUtc(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return default;
        // SimBrief suele devolver "YYYY-MM-DD HH:MM:SS" en UTC.
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return default;
    }
}
