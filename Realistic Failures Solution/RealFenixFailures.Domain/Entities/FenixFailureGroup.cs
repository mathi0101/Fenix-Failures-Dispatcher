namespace RealFenixFailures.Domain.Entities;

public class FenixFailureGroup {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SystemId { get; set; }
    public required FenixFailureSystem System { get; set; }

    public ICollection<FenixFailureDefinition> FailureDefinitions { get; set; } = new List<FenixFailureDefinition>();
}
