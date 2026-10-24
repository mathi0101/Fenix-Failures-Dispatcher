namespace RealFenixFailures.Domain.Enums;

public enum FailureLifecycleState {
    Preparing = 0,
    Queued = 1,
    Sent = 2,
    Active = 3,
    ResolvedByPilot = 4,
    AutoResolved = 5,
    NotResolved = 6,
    Rejected = 7
}
