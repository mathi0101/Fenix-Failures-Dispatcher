namespace RealFenixFailures.Integrations.Fenix.Models;

public sealed record FenixManualFailure(
    string Id,
    string Title,
    FenixFailureCondition? FailureCondition,
    bool Failed,
    int Severity = 1,
    bool Repairable = false
);
