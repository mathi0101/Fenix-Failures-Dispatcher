using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;
using System.ComponentModel;

namespace RealFenixFailures.Application.Interfaces;

public interface IRealisticSessionManager : INotifyPropertyChanged {
    RealisticSession? SessionState { get; }

    /// <summary>Resultado consolidado de la última sesión cerrada (modal de resultados §13); nulo si nunca se cerró una.</summary>
    SessionResultDto? LastSessionResult { get; }

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

    /// <summary>
    /// Vuelo terminado detectado (issue #15): se dispara cuando vence el período de gracia tras la detección
    /// del apagado en gate. La UI muestra el aviso de finalización y restaura la ventana sin robar foco.
    /// </summary>
    event EventHandler<FlightCompletionNoticeDueEventArgs>? FlightCompletionNoticeDue;

    /// <summary>
    /// Vuelo terminado detectado y sesión aún abierta sin decisión: gobierna el banner de finalización
    /// del panel Realistic. Volátil por diseño (sin persistencia): si la app se cierra sin finalizar,
    /// la recuperación de sesiones huérfanas cubre el caso al reabrir.
    /// </summary>
    bool IsFlightCompletionPending { get; }

    /// <summary>
    /// Período de gracia entre la detección del fin de vuelo y el aviso (tiempo real de pared, no tiempo
    /// de simulación). La UI lo alimenta desde las preferencias de usuario (userprefs.json); default 3 min.
    /// </summary>
    TimeSpan FlightCompletionGraceDelay { get; set; }
}

public class FlightCompletionNoticeDueEventArgs : EventArgs {
    public DateTime NoticeAtUtc { get; init; }
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
