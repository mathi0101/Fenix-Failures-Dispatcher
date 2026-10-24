using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Integrations.Fenix.Models;

namespace RealFenixFailures.Integrations.Fenix.Mappers;

internal static class FenixMappers {
    internal static AllFenixFailuresResponseDto FenixJsonFailuresToDto(FenixManualFailuresResponse response) {
        return new AllFenixFailuresResponseDto {
            MajorGroups =
            [..response.Atas.Select(
                block => new FenixMajorSystemGroupDto(block.Id, block.Title, block.ShortTitle, block.Groups.Select(
                    group => new FenixSystemGroupDto(group.GroupName, group.Failures.Select(
                        failure => new FenixFailureDto(failure.Id, failure.Title, failure.Failed,
                            failure.FailureCondition is FenixFailureCondition fc ?
                            new FenixFailureConditionDto(fc.Id, fc.Ias, fc.Alt, fc.Altb, fc.Time, fc.AfterEvent, fc.AfterEventSeconds) : null ) {
                            Severity = (FailureSeverity)failure.Severity,
                            Repairable = failure.Repairable
                        }
                        ).ToList())
                    ).ToList())
                )]
        };
    }
    internal static FenixSaveManualRequest ToFenixSaveManualRequest(this FenixArmFailureRequest rq) {
        return new FenixSaveManualRequest(rq.Id, rq.Failed, rq.FailureCondition?.ToFenixFailureConditionRequest());
    }
    internal static FenixFailureConditionRequest ToFenixFailureConditionRequest(this FenixArmFailureConditionRequest rq) {
        return new FenixFailureConditionRequest { Ias = rq.Ias, Alt = rq.Alt, Altb = rq.Altb, Time = rq.Time, AfterEvent = rq.AfterEvent, AfterEventSeconds = rq.AfterEventSeconds };
    }
}
