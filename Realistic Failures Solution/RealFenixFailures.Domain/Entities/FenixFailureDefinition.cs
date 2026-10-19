using RealFenixFailures.Domain.Enums;
using System.Diagnostics;

namespace RealFenixFailures.Domain.Entities;

[DebuggerDisplay("{FenixFailureId} | Severity: {Severity}")]
public class FenixFailureDefinition {
    public string FenixFailureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public required FenixFailureGroup Group { get; set; }
    public FailureSeverity Severity { get; set; }
    public bool CanBeTemporary { get; set; }
    public bool CanBeRepaired { get; set; }

    public ICollection<PresetFailureDefinition> PresetFailureDefinitions { get; set; } = new List<PresetFailureDefinition>();
}
