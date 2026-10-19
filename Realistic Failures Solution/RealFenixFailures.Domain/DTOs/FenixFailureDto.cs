using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.DTOs;

public sealed record FenixFailureDto(
    string FenixId,
    string Description,
    bool Failed,
    FenixFailureConditionDto? FailureCondition
) {
    public bool IsArmed => FailureCondition != null;
    public FailureSeverity Severity { get; init; } = FailureSeverity.Minor;
    public bool Repairable { get; init; }
};
