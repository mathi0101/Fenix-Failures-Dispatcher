using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.Localization;
using RealFenixFailures.UI.ViewModels.Base;
using System.Windows.Media;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>
/// Una entrada del feed de fallas de la sesión realista. Refleja el ciclo de vida de la falla según
/// la confirmación real de Fenix: enviada → activa confirmada → resuelta (o se queda en enviada).
/// Colores por estado (diseño objetivo §14): enviada amarillo, activa Minor/Major naranja,
/// activa Critical rojo, resuelta verde.
/// </summary>
public sealed class LiveFailureEntryViewModel : LocalizedViewModelBase {
    public required string FenixFailureId { get; init; }
    public required string TimestampText { get; init; }
    public required string Description { get; init; }

    private FailureLifecycleState? _currentState;
    private FailureSeverity? _currentSeverity;

    private string _stateLabel = Loc.T("Lf_SentAwaiting");
    public string StateLabel {
        get => _stateLabel;
        private set => SetProperty(ref _stateLabel, value);
    }

    private string _displayText = string.Empty;
    public string DisplayText {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    private Brush _stateBrush = ResolveBrush("Brush.Accent.Amber.Primary");
    public Brush StateBrush {
        get => _stateBrush;
        private set => SetProperty(ref _stateBrush, value);
    }

    public LiveFailureEntryViewModel() {
        _displayText = $"{TimestampText}  {_stateLabel}  ·  {Description}";
    }

    /// <summary>Actualiza la entrada cuando Fenix confirma una transición del ciclo de vida.</summary>
    public void ApplyState(FailureLifecycleState newState, FailureSeverity? severity) {
        _currentState = newState;
        _currentSeverity = severity;

        StateLabel = StateLabelText(newState, severity);
        DisplayText = $"{TimestampText}  {StateLabel}  ·  {Description}";
        StateBrush = newState switch {
            FailureLifecycleState.Active when severity == FailureSeverity.Critical => ResolveBrush("Brush.Accent.Red.Primary"),
            FailureLifecycleState.Active => ResolveBrush("Brush.Accent.Orange.Primary"),
            FailureLifecycleState.ResolvedByPilot or FailureLifecycleState.AutoResolved => ResolveBrush("Brush.Accent.Green.Primary"),
            _ => ResolveBrush("Brush.Accent.Amber.Primary")
        };
    }

    private static string StateLabelText(FailureLifecycleState newState, FailureSeverity? severity)
        => newState switch {
            FailureLifecycleState.Active when severity == FailureSeverity.Critical => Loc.T("Lf_ActiveCritical"),
            FailureLifecycleState.Active => severity switch {
                FailureSeverity.Minor => Loc.T("Lf_ActiveMinor"),
                FailureSeverity.Major => Loc.T("Lf_ActiveMajor"),
                _ => Loc.T("Lf_Active")
            },
            FailureLifecycleState.ResolvedByPilot => Loc.T("Lf_ResolvedByPilot"),
            FailureLifecycleState.AutoResolved => Loc.T("Lf_AutoResolved"),
            _ => Loc.T("Lf_SentAwaiting")
        };

    protected override void OnLanguageChanged() {
        _stateLabel = Loc.T("Lf_SentAwaiting");
        if (_currentState.HasValue)
            StateLabel = StateLabelText(_currentState.Value, _currentSeverity);
        DisplayText = $"{TimestampText}  {StateLabel}  ·  {Description}";
    }

    private static Brush ResolveBrush(string resourceKey) =>
        System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? System.Windows.Media.Brushes.Gray;
}
