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
    /// <summary>
    /// Baja lógica del catálogo: el sincronizador nunca borra definiciones físicamente porque el
    /// historial (TriggeredFailure, PresetFailureDefinition) las referencia por FK Restrict. Una
    /// falla ausente del JSON vigente queda inactiva y los candidatos del motor la filtran.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<PresetFailureDefinition> PresetFailureDefinitions { get; set; } = new List<PresetFailureDefinition>();
}
