using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.ViewModels.Realistic;

namespace RealFenixFailures.TestUnits.Presentation;

public class SessionResultWindowViewModelTests {
    [Fact]
    public void Result_UsesOwnMetadata_SortsEventsAndSeparatesRejectedFailures() {
        var started = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var result = new SessionResultDto {
            SessionId = 42, UserAircraftId = 7, Registration = "LV-FNX", AircraftIcaoType = "A320",
            FlightNumber = "SAB1704", OfpSource = "SimBrief", OriginIcao = "SUMU", DestinationIcao = "SAEZ",
            StartedAt = started, FinishedAt = started.AddHours(2),
            EventLogs = [new() { Id = 2, OccurredAt = started.AddMinutes(1), Category = SessionEventCategory.EnginesStarted },
                new() { Id = 1, OccurredAt = started, Category = SessionEventCategory.SessionStarted }],
            Failures = [new() { Id = 1, FenixFailureId = "A", State = FailureLifecycleState.Rejected },
                new() { Id = 2, FenixFailureId = "B", State = FailureLifecycleState.NotResolved }],
            WearDeltas = [new() { AtaId = 24, SystemName = "Elec", DeltaPercentage = 1, TotalPercentage = 81 },
                new() { AtaId = 29, SystemName = "Hyd", DeltaPercentage = 3, TotalPercentage = 20 }]
        };
        var vm = new SessionResultWindowViewModel(result);
        Assert.Contains("LV-FNX", vm.RegistrationDisplay);
        Assert.Contains("A320", vm.RegistrationDisplay);
        Assert.Contains("SAB1704", vm.OfpDisplay);
        Assert.StartsWith("12:00:00", vm.Timeline[0].TimeDisplay);
        Assert.Equal("#22C55E", vm.Timeline[1].Color);
        Assert.Single(vm.Failures);
        Assert.Single(vm.RejectedFailures);
        Assert.Contains("+2", vm.WearSummaryDisplay);
        Assert.True(vm.WearRows[0].RequiresMaintenance);
        Assert.Equal(81, vm.WearRows[0].TotalPercentage);
    }

    [Fact]
    public void ResolvedFailure_KeepsPhaseSystemAndDuration_WithoutClaimingRepairMethod() {
        var triggered = new DateTime(2026, 10, 24, 23, 50, 0, DateTimeKind.Utc);
        var row = new SessionResultWindowViewModel.FailureRow(new FailureResultDto {
            Id = 1, FenixFailureId = "HYD", FailureName = "Fuga", FailureSystem = "Hydraulic", FailureGroup = "Green",
            FailureAtaId = 29, FlightPhase = ComplexFlightPhaseEnum.Cruise, TriggeredAt = triggered,
            ResolvedAtUtc = triggered.AddMinutes(20), ResolutionSource = ResolutionSource.Pilot,
            State = FailureLifecycleState.ResolvedByPilot
        });
        Assert.Contains("ATA 29", row.SystemDisplay);
        Assert.Contains("Hydraulic", row.SystemDisplay);
        Assert.Contains("Green", row.SystemDisplay);
        Assert.Contains("Crucero", row.Detail);
        Assert.Contains("00:20:00", row.Detail);
        Assert.Contains("25/10", row.Detail);
        Assert.DoesNotContain("FMGC", row.Detail);
    }

    [Fact]
    public void EmptyDiscardedResult_HasExplicitEmptyStates() {
        var vm = new SessionResultWindowViewModel(new SessionResultDto { SessionId = 1, DiscardedAt = DateTime.UtcNow });
        Assert.False(vm.HasFailures);
        Assert.False(vm.HasTimeline);
        Assert.False(vm.HasWearRows);
        Assert.Contains("descartada", vm.WearEmptyMessage);
    }
}
