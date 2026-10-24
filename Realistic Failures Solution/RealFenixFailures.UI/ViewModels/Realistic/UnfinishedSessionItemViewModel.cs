using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.Commands;
using RealFenixFailures.UI.ViewModels.Base;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Sesión sin finalizar mostrada en el diálogo de recuperación al arrancar la app (diseño objetivo §6/§13).</summary>
public sealed class UnfinishedSessionItemViewModel : ObservableObject {
    private readonly MainWindowViewModel _owner;

    public UnfinishedSessionItemViewModel(MainWindowViewModel owner, FlightSessionDto session) {
        _owner = owner;
        SessionId = session.Id;

        var route = session.OriginIcao is null && session.DestinationIcao is null
            ? "sin OFP"
            : $"{session.OriginIcao ?? "????"} → {session.DestinationIcao ?? "????"}";
        Title = $"Sesión #{session.Id} · {route}";
        StartedDisplay = $"Iniciada {session.StartedAt:dd/MM/yyyy HH:mm:ss} UTC";
        FailureCountDisplay = session.FailureCount == 1 ? "1 falla lanzada" : $"{session.FailureCount} fallas lanzadas";
        EtaDisplay = session.EstimatedArrivalUtc.HasValue
            ? $"Llegada estimada (OFP): {session.EstimatedArrivalUtc.Value:dd/MM HH:mm} UTC"
            : "Sin ETA del OFP: la finalización no aplicará desgaste";

        FinalizeCommand = new RelayCommand(() => _ = _owner.FinalizeUnfinishedSessionAsync(SessionId));
        DiscardCommand = new RelayCommand(() => _ = _owner.DiscardUnfinishedSessionAsync(SessionId));
    }

    public int SessionId { get; }
    public string Title { get; }
    public string StartedDisplay { get; }
    public string FailureCountDisplay { get; }
    public string EtaDisplay { get; }

    public RelayCommand FinalizeCommand { get; }
    public RelayCommand DiscardCommand { get; }
}
