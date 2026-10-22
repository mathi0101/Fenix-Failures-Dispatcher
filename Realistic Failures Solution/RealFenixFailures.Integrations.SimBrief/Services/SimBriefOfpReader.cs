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
        var aircraft = doc.Root?.Element("aircraft");
        var origin = doc.Root?.Element("origin");
        var destination = doc.Root?.Element("destination");
        var times = doc.Root?.Element("times");
        var route = doc.Root?.Element("route");

        var aircraftIcao = (aircraft?.Element("icaocode")?.Value ?? general?.Element("aircraft_icao")?.Value)?.Trim() ?? string.Empty;
        var originIcao = origin?.Element("icao_code")?.Value?.Trim() ?? string.Empty;
        var destIcao = destination?.Element("icao_code")?.Value?.Trim() ?? string.Empty;
        var airline = general?.Element("icao_airline")?.Value?.Trim() ?? string.Empty;
        var flightNumber = general?.Element("flight_number")?.Value?.Trim() ?? string.Empty;
        var etmin = int.TryParse(times?.Element("est_time_enroute")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ? seconds / 60 : 0;
        var routeDistanceNM = int.TryParse(general?.Element("route_distance")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var routeDistance) ? routeDistance : 0;

        // out = off-block; off = despegue; on = aterrizaje; in = llegada al gate.
        // orig/dest se conservan para los OFP sintéticos anteriores.
        var offBlock = ParseUtc(times?.Element("sched_out")?.Value ?? times?.Element("sched_off")?.Value ?? times?.Element("orig")?.Value);
        var eta = ParseUtc(times?.Element("est_on")?.Value ?? times?.Element("sched_on")?.Value ?? times?.Element("dest")?.Value);
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
            RouteDistanceNM= routeDistanceNM,
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
        // La API devuelve instantes Unix en segundos (no milisegundos).
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds)) {
            if (unixSeconds < DateTimeOffset.MinValue.ToUnixTimeSeconds() || unixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return default;
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }
        // Compatibilidad con fechas textuales, incluido el mock anterior.
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return default;
    }
}
