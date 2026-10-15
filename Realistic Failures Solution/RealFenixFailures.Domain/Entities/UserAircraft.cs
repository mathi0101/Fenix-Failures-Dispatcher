namespace RealFenixFailures.Domain.Entities;

public class UserAircraft {
    public int Id { get; set; }
    public required string Registration { get; set; }
    public required string IcaoTypeCode { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AircraftSystemWear> SystemWears { get; set; } = new List<AircraftSystemWear>();
    public ICollection<FlightSession> FlightSessions { get; set; } = new List<FlightSession>();
}
