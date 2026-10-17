namespace RealFenixFailures.Domain.Entities;

public class AircraftWearableSystem {
    public int Id { get; set; }
    public int AtaId { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<AircraftSystemWear> Wears { get; set; } = new List<AircraftSystemWear>();
}
