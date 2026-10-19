using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;
using System.ComponentModel;

namespace RealFenixFailures.Application.Interfaces;

public interface IRealisticSessionManager : INotifyPropertyChanged {
    RealisticSession? SessionState { get; }

    bool IsPaused { get; }

    int PreparedCount { get; }

    Task<ServiceResult<string>> StartNewSessionAsync(RealisticModeContext context, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task PauseAsync(CancellationToken ct);
    Task ResumeAsync(CancellationToken ct, bool restoreFailures = false);
    Task<int> DiscardPreparedAsync(CancellationToken ct);

    event EventHandler<FailureTriggeredEventArgs>? FailureTriggered;
    event EventHandler<FailureStateChangedEventArgs>? FailureStateChanged;
    event EventHandler<SessionErrorEventArgs>? SessionError;
}

public class FailureTriggeredEventArgs : EventArgs {
    public required string FenixFailureId { get; init; }
    public required string Description { get; init; }
    public DateTime TriggeredAtUtc { get; init; }
}

/// <summary>
/// A lifecycle transition of a failure was confirmed against Fenix (Sent→Active, Active→ResolvedByPilot, ...).
/// Raised once per observed transition on the tick that detected it.
/// </summary>
public class FailureStateChangedEventArgs : EventArgs {
    public required string FenixFailureId { get; init; }
    public required string Description { get; init; }
    public FailureLifecycleState NewState { get; init; }
    public FailureSeverity? Severity { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}

public class SessionErrorEventArgs : EventArgs {
    public required string Message { get; init; }
    public required Exception Exception { get; init; }
}
