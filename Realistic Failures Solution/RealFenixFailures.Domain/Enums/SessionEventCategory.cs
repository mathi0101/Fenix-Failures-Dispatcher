namespace RealFenixFailures.Domain.Enums;

public enum SessionEventCategory {
    PhaseChanged = 1,
    EnginesStarted = 2,
    EnginesStopped = 3,
    AltitudeCrossed = 4,
    FailureTriggered = 5,
    SessionStarted = 6,
    SessionStopped = 7,
    SessionError = 8,
    Pause = 9,
    ResumeOrReconnect = 10,
    FailureStateChanged = 11,
    WearUpdated = 12,
    OfpLoaded = 13,
    SessionRecovered = 14
}
