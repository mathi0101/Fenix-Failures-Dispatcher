using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.Localization;
using RealFenixFailures.UI.ViewModels.Base;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Sesión sin finalizar mostrada en el diálogo de recuperación al arrancar la app (diseño objetivo §6/§13).</summary>
public sealed class UnfinishedSessionItemViewModel : LocalizedViewModelBase {
    private readonly MainWindowViewModel _owner;
    private readonly FlightSessionDto _session;

    public UnfinishedSessionItemViewModel(MainWindowViewModel owner, FlightSessionDto session) {
        _owner = owner;
        _session = session;
        SessionId = session.Id;

        FinalizeCommand = new RelayCommand(() => _ = _owner.FinalizeUnfinishedSessionAsync(SessionId));
        DiscardCommand = new RelayCommand(() => _ = _owner.DiscardUnfinishedSessionAsync(SessionId));
        ComputeDisplays();
    }

    private void ComputeDisplays() {
        var s = _session;
        var route = s.OriginIcao is null && s.DestinationIcao is null
            ? Loc.T("Unf_NoOfp")
            : $"{s.OriginIcao ?? "????"} → {s.DestinationIcao ?? "????"}";
        Title = Loc.T("Unf_SessionTitle", s.Id, route);
        StartedDisplay = Loc.T("Unf_Started", s.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        FailureCountDisplay = s.FailureCount == 1
            ? Loc.T("Unf_FailuresOne", s.FailureCount)
            : Loc.T("Unf_FailuresMany", s.FailureCount);
        EtaDisplay = s.EstimatedArrivalUtc.HasValue
            ? Loc.T("Unf_Eta", s.EstimatedArrivalUtc.Value.ToString("dd/MM HH:mm"))
            : Loc.T("Unf_NoEta");
    }

    public int SessionId { get; }
    public string Title { get; private set; } = string.Empty;
    public string StartedDisplay { get; private set; } = string.Empty;
    public string FailureCountDisplay { get; private set; } = string.Empty;
    public string EtaDisplay { get; private set; } = string.Empty;

    public RelayCommand FinalizeCommand { get; }
    public RelayCommand DiscardCommand { get; }

    protected override void OnLanguageChanged() {
        ComputeDisplays();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StartedDisplay));
        OnPropertyChanged(nameof(FailureCountDisplay));
        OnPropertyChanged(nameof(EtaDisplay));
    }
}
