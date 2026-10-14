using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Session;
using System.ComponentModel;

namespace RealFenixFailures.Application.Interfaces;

public interface IRealisticSessionManager : INotifyPropertyChanged {
    RealisticSession? SessionState { get; }

    bool IsPaused { get; }

    Task<ServiceResult<string>> StartNewSessionAsync(RealisticModeContext context, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task PauseAsync(CancellationToken ct);
    Task ResumeAsync(CancellationToken ct);

    event EventHandler<FailureTriggeredEventArgs>? FailureTriggered;
    event EventHandler<SessionErrorEventArgs>? SessionError;
}

public class FailureTriggeredEventArgs : EventArgs {
    public required string FenixFailureId { get; init; }
    public required string Description { get; init; }
    public DateTime TriggeredAtUtc { get; init; }
}

public class SessionErrorEventArgs : EventArgs {
    public required string Message { get; init; }
    public required Exception Exception { get; init; }
}
