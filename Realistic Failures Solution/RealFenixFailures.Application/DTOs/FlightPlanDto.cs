using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.DTOs;

/// <summary>Resultado del flujo OFP para la UI (Paso 3) y el motor.</summary>
public sealed record FlightPlanDto {
    public int Id { get; init; }
    public string OriginIcao { get; init; } = string.Empty;
    public string DestinationIcao { get; init; } = string.Empty;
    public string AircraftIcaoType { get; init; } = string.Empty;
    public string? FlightNumber { get; init; }
    public string? Callsign { get; init; }
    public DateTime OffBlockUtc { get; init; }
    public DateTime EstimatedArrivalUtc { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public string? RouteText { get; init; }
    public ValidationState ValidationState { get; init; }
    public string? ValidationMessage { get; init; }
    public string? Source { get; init; }
    public DateTime DownloadedAtUtc { get; init; }

    /// <summary>Indica si el plan quedó apto para arrancar una sesión.</summary>
    public bool IsUsable => ValidationState == ValidationState.Valid;
}
