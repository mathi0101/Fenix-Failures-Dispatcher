namespace RealFenixFailures.Domain.Entities;

public class AircraftWearableSystem {
    public int Id { get; set; }
    public int AtaId { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Desgaste que acumula el sistema por hora de vuelo de sesión (%/h); la acumulación ocurre al cerrar la sesión.</summary>
    public double WearPerFlightHour { get; set; }

    public ICollection<AircraftSystemWear> Wears { get; set; } = new List<AircraftSystemWear>();
}
