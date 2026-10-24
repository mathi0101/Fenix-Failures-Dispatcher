using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Entities;

public class FlightPlan {
    public int Id { get; set; }
    public required string OriginIcao { get; set; }
    public required string DestinationIcao { get; set; }
    public required string AircraftIcaoType { get; set; }
    public string? FlightNumber { get; set; }
    public string? Callsign { get; set; }
    public DateTime OffBlockUtc { get; set; }
    public DateTime EstimatedArrivalUtc { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string? RouteText { get; set; }
    public int RouteDistanceNM { get; set; }
    public string? RawData { get; set; }
    public required string Source { get; set; }
    public DateTime DownloadedAtUtc { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidUntilUtc { get; set; }
    public ValidationState ValidationState { get; set; }
    public string? ValidationMessage { get; set; }

    public ICollection<FlightSession> Sessions { get; set; } = new List<FlightSession>();
}
