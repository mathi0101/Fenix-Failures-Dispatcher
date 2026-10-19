using RealFenixFailures.Domain.Enums;
using System.Windows;
using System.Windows.Media;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>
/// Una entrada del feed de fallas de la sesión realista. Refleja el ciclo de vida de la falla según
/// la confirmación real de Fenix: enviada → activa confirmada → resuelta (o se queda en enviada).
/// Colores por estado (diseño objetivo §14): enviada amarillo, activa Minor/Major naranja,
/// activa Critical rojo, resuelta verde.
/// </summary>
public sealed class LiveFailureEntryViewModel {
    public required string FenixFailureId { get; init; }
    public required string TimestampText { get; init; }
    public required string Description { get; init; }

    private string _stateLabel = "Enviada — esperando confirmación";
    public string StateLabel {
        get => _stateLabel;
        private set {
            _stateLabel = value;
            DisplayText = $"{TimestampText}  {value}  ·  {Description}";
        }
    }

    private string _displayText;
    public string DisplayText {
        get => _displayText;
        private set => _displayText = value;
    }

    private Brush _stateBrush = ResolveBrush("Brush.Accent.Amber.Primary");
    public Brush StateBrush {
        get => _stateBrush;
        private set => _stateBrush = value;
    }

    public LiveFailureEntryViewModel() {
        _displayText = $"{TimestampText}  {_stateLabel}  ·  {Description}";
    }

    /// <summary>Actualiza la entrada cuando Fenix confirma una transición del ciclo de vida.</summary>
    public void ApplyState(FailureLifecycleState newState, FailureSeverity? severity) {
        StateLabel = newState switch {
            FailureLifecycleState.Active when severity == FailureSeverity.Critical => "Activa (Critical)",
            FailureLifecycleState.Active => severity switch {
                FailureSeverity.Minor => "Activa (Minor)",
                FailureSeverity.Major => "Activa (Major)",
                _ => "Activa"
            },
            FailureLifecycleState.ResolvedByPilot => "Resuelta por el piloto",
            FailureLifecycleState.AutoResolved => "Auto-resuelta por el engine",
            _ => StateLabel
        };
        StateBrush = newState switch {
            FailureLifecycleState.Active when severity == FailureSeverity.Critical => ResolveBrush("Brush.Accent.Red.Primary"),
            FailureLifecycleState.Active => ResolveBrush("Brush.Accent.Orange.Primary"),
            FailureLifecycleState.ResolvedByPilot or FailureLifecycleState.AutoResolved => ResolveBrush("Brush.Accent.Green.Primary"),
            _ => ResolveBrush("Brush.Accent.Amber.Primary")
        };
    }

    private static Brush ResolveBrush(string resourceKey) =>
        System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
}
