using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.ViewModels.Base;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Falla disparada mostrada en el resumen expandido de una sesión (Paso 2).</summary>
public sealed class TriggeredFailureItemViewModel : ObservableObject {
    public TriggeredFailureItemViewModel(TriggeredFailureDto dto) {
        FailureName = dto.FailureName;
        FlightPhaseName = dto.FlightPhase.ToString();
        TriggeredAt = dto.TriggeredAt;
    }

    public string FailureName { get; }
    public string FlightPhaseName { get; }
    public DateTime TriggeredAt { get; }

    public string TimeDisplay => TriggeredAt.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
}
