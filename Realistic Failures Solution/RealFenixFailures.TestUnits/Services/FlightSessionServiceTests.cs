// RealFenixFailures.TestUnits/Services/FlightSessionServiceTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;

namespace RealFenixFailures.TestUnits.Services;

public class FlightSessionServiceTests {
    private static RealFenixDbContext CreateContext() {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<RealFenixDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new RealFenixDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static int _aircraftSeq;

    private static (FlightSessionService Service, RealFenixDbContext Db) CreateSut() {
        var db = CreateContext();
        var service = new FlightSessionService(
            new FlightSessionRepository(db),
            new TriggeredFailureRepository(db));
        return (service, db);
    }

    private static UserAircraft AddAircraft(RealFenixDbContext db) {
        var aircraft = new UserAircraft {
            Registration = $"TEST-{++_aircraftSeq}",
            IcaoTypeCode = "A320",
            CreatedAt = DateTime.UtcNow
        };
        db.UserAircrafts.Add(aircraft);
        db.SaveChanges();
        return aircraft;
    }

    private static FlightPlan AddFlightPlan(RealFenixDbContext db, string? origin, string? destination) {
        var now = DateTime.UtcNow;
        var plan = new FlightPlan {
            OriginIcao = origin ?? "XXXX",
            DestinationIcao = destination ?? "XXXX",
            AircraftIcaoType = "A320",
            Source = "SIMBRIEF",
            DownloadedAtUtc = now,
            ValidFromUtc = now.AddHours(-2),
            ValidUntilUtc = now.AddHours(2)
        };
        db.FlightPlans.Add(plan);
        db.SaveChanges();
        return plan;
    }

    private static FlightSession AddSession(RealFenixDbContext db, RiskLevel risk, string? origin, string? destination, DateTime? finishedAt) {
        var aircraft = AddAircraft(db);
        var plan = AddFlightPlan(db, origin, destination);
        var session = new FlightSession {
            UserAircraftId = aircraft.Id,
            FlightPlanId = plan.Id,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            RiskLevel = risk,
            FinishedAt = finishedAt
        };
        db.FlightSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static void AddFenixFailure(RealFenixDbContext db, string fenixFailureId, FailureSeverity severity) {
        var system = db.FenixFailureSystems.FirstOrDefault();
        if (system is null) {
            system = new FenixFailureSystem {
                Name = "Test System",
                ShortName = "TEST",
                AtaId = 21
            };
            db.FenixFailureSystems.Add(system);
            db.SaveChanges();
        }
        var group = new FenixFailureGroup {
            Name = "Test",
            SystemId = system.Id,
            System = system
        };
        db.FenixFailureGroups.Add(group);
        db.SaveChanges();
        db.FenixFailureDefinitions.Add(new FenixFailureDefinition {
            FenixFailureId = fenixFailureId,
            Name = $"Falla {fenixFailureId}",
            Severity = severity,
            GroupId = group.Id,
            Group = group
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetResultAsync_ComputesSummary_ByStateAndSeverity() {
        var (service, db) = CreateSut();
        var session = AddSession(db, RiskLevel.Moderate, "SAEZ", "SBGR", DateTime.UtcNow);

        AddFenixFailure(db, "F_ACTIVE_CRIT", FailureSeverity.Critical);
        AddFenixFailure(db, "F_PILOT_MAJ", FailureSeverity.Major);
        AddFenixFailure(db, "F_AUTO_MIN", FailureSeverity.Minor);
        AddFenixFailure(db, "F_PILOT_MIN", FailureSeverity.Minor);
        AddFenixFailure(db, "F_REJECTED", FailureSeverity.Minor);

        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_ACTIVE_CRIT",
            FailureName = "Activa crítica",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            State = FailureLifecycleState.Active,
            Severity = FailureSeverity.Critical
        });
        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_PILOT_MAJ",
            FailureName = "Resuelta por piloto",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            State = FailureLifecycleState.ResolvedByPilot,
            Severity = FailureSeverity.Major,
            ResolutionSource = ResolutionSource.Pilot
        });
        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_AUTO_MIN",
            FailureName = "Auto-resuelta",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            State = FailureLifecycleState.AutoResolved,
            Severity = FailureSeverity.Minor,
            ResolutionSource = ResolutionSource.EngineAuto
        });
        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_PILOT_MIN",
            FailureName = "Otra por piloto",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            State = FailureLifecycleState.ResolvedByPilot,
            Severity = FailureSeverity.Minor
        });
        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_REJECTED",
            FailureName = "Rechazada",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Takeoff,
            State = FailureLifecycleState.Rejected,
            Severity = FailureSeverity.Minor
        });
        db.SaveChanges();

        var result = await service.GetResultAsync(session.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SAEZ", result.OriginIcao);
        Assert.Equal("SBGR", result.DestinationIcao);
        Assert.Equal(4, result.Summary.TotalFailures);
        Assert.Equal(1, result.Summary.ActiveAtEnd);
        Assert.Equal(2, result.Summary.ResolvedByPilot);
        Assert.Equal(1, result.Summary.AutoResolved);
        Assert.Equal(1, result.Summary.Rejected);
        Assert.Equal(3, result.Summary.ResolvedCount);
        Assert.Equal(1, result.Summary.Critical);
        Assert.Equal(1, result.Summary.Major);
        Assert.Equal(2, result.Summary.Minor);
        Assert.Equal(5, result.Failures.Count);
    }

    [Fact]
    public async Task GetResultAsync_IsSelfContained_AndClosureEventsAreIdempotent() {
        var (service, db) = CreateSut();
        using var disposableDb = db;
        var session = AddSession(db, RiskLevel.Moderate, "SUMU", "SAEZ", null);
        session.FlightPlan!.FlightNumber = "SAB1704";
        var failure = AddTriggeredFailure(db, session.Id, FailureLifecycleState.Active);
        var finished = DateTime.UtcNow;
        await service.FinalizeSessionAsync(session.Id, finished, null, CancellationToken.None);
        await service.FinalizeSessionAsync(session.Id, finished, null, CancellationToken.None);
        db.ChangeTracker.Clear();

        var result = (await service.GetResultAsync(session.Id, CancellationToken.None))!;

        Assert.Equal(session.UserAircraftId, result.UserAircraftId);
        Assert.StartsWith("TEST-", result.Registration);
        Assert.Equal("A320", result.AircraftIcaoType);
        Assert.Equal("SAB1704", result.FlightNumber);
        var closure = Assert.Single(result.EventLogs);
        Assert.Equal("FAILURE_NOT_RESOLVED", closure.EventCode);
        Assert.Equal(failure.Id, closure.TriggeredFailureId);
        Assert.Equal(FailureLifecycleState.NotResolved, Assert.Single(result.Failures).State);
        Assert.False(string.IsNullOrEmpty(result.Failures[0].FailureGroup));
        Assert.NotNull(result.Failures[0].FailureAtaId);
    }

    [Fact]
    public async Task GetResultAsync_ReturnsNull_WhenSessionMissing() {
        var (service, _) = CreateSut();

        var result = await service.GetResultAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FinalizeSessionAsync_SetsFinishedAt_AndReturnsResult() {
        var (service, db) = CreateSut();
        var session = AddSession(db, RiskLevel.Low, "SUMU", "SAEZ", null);

        var result = await service.FinalizeSessionAsync(session.Id, DateTime.UtcNow, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(db.FlightSessions.First().FinishedAt.HasValue);
        Assert.Equal(0, result.Summary.TotalFailures);
    }

    [Fact]
    public async Task GetUnfinishedSessionsAsync_ReturnsOnlyOpenSessions() {
        var (service, db) = CreateSut();
        AddSession(db, RiskLevel.Low, null, null, null);
        AddSession(db, RiskLevel.High, null, null, DateTime.UtcNow.AddHours(-1));

        var result = await service.GetUnfinishedSessionsAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Null(result[0].FinishedAt);
    }

    private static DateTime TrimMicroseconds(DateTime value) => new(value.Ticks / 10 * 10, value.Kind);

    private static FlightSessionService CreateSutWithWear(RealFenixDbContext db) {
        var service = new FlightSessionService(
            new FlightSessionRepository(db),
            new TriggeredFailureRepository(db),
            new AircraftSystemWearRepository(db));
        return service;
    }

    private static (AircraftWearableSystem System, AircraftSystemWear Wear) AddWearable(RealFenixDbContext db, int aircraftId, double ratePerHour, double basePercentage) {
        var system = new AircraftWearableSystem {
            AtaId = 29,
            Name = "Hidráulica",
            ShortName = "HYD",
            DisplayOrder = 1,
            WearPerFlightHour = ratePerHour
        };
        db.AircraftWearableSystems.Add(system);
        db.SaveChanges();
        var wear = new AircraftSystemWear {
            UserAircraftId = aircraftId,
            WearableSystemId = system.Id,
            WearPercentage = basePercentage,
            LastUpdatedAt = DateTime.UtcNow
        };
        db.AircraftSystemWears.Add(wear);
        db.SaveChanges();
        return (system, wear);
    }

    private static TriggeredFailure AddTriggeredFailure(RealFenixDbContext db, int sessionId, FailureLifecycleState state) {
        var fenixId = $"F_{state}_{db.TriggeredFailures.Count()}";
        AddFenixFailure(db, fenixId, FailureSeverity.Major);
        var failure = new TriggeredFailure {
            FlightSessionId = sessionId,
            FenixFailureId = fenixId,
            FailureName = $"Falla {state}",
            TriggeredAt = DateTime.UtcNow.AddHours(-1),
            FlightPhase = ComplexFlightPhaseEnum.Cruise,
            State = state,
            Severity = FailureSeverity.Major
        };
        db.TriggeredFailures.Add(failure);
        db.SaveChanges();
        return failure;
    }

    [Fact]
    public async Task FinalizeSessionAsync_ConsolidatesUnresolved_AsNotResolvedSessionEnded() {
        var (service, db) = CreateSut();
        var session = AddSession(db, RiskLevel.Moderate, "SUMU", "SAEZ", null);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Active);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Sent);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Rejected);

        var finishedAt = DateTime.UtcNow;
        var result = await service.FinalizeSessionAsync(session.Id, finishedAt, null, CancellationToken.None);

        var failures = db.TriggeredFailures.Where(f => f.FlightSessionId == session.Id).ToList();
        var unresolved = failures.Where(f => f.State == FailureLifecycleState.NotResolved).ToList();
        Assert.Equal(2, unresolved.Count);
        Assert.All(unresolved, f => {
            Assert.Equal(ResolutionSource.SessionEnded, f.ResolutionSource);
            Assert.NotNull(f.ResolvedAtUtc);
            Assert.Equal(TrimMicroseconds(finishedAt), TrimMicroseconds(f.ResolvedAtUtc!.Value));
        });
        Assert.Contains(failures, f => f.State == FailureLifecycleState.Rejected);
        Assert.Equal(0, result.Summary.ActiveAtEnd);
    }

    [Fact]
    public async Task DiscardSessionAsync_MarksDiscarded_AndDoesNotApplyWear() {
        var (service, db) = CreateSut();
        var session = AddSession(db, RiskLevel.Moderate, "SUMU", "SAEZ", null);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Active);
        AddWearable(db, session.UserAircraftId, ratePerHour: 5.0, basePercentage: 10.0);

        await service.DiscardSessionAsync(session.Id, DateTime.UtcNow, CancellationToken.None);

        var stored = db.FlightSessions.First();
        Assert.NotNull(stored.DiscardedAt);
        Assert.NotNull(stored.FinishedAt);
        Assert.Contains(db.TriggeredFailures.Where(f => f.FlightSessionId == session.Id),
            f => f.State == FailureLifecycleState.NotResolved);
        Assert.Equal(10.0, db.AircraftSystemWears.Single().WearPercentage, precision: 6);
    }

    [Fact]
    public async Task FinalizeUnfinishedSessionAsync_AppliesWearByEta_FromEnginesStart() {
        var db = CreateContext();
        var service = CreateSutWithWear(db);
        var session = AddSession(db, RiskLevel.Moderate, "SUMU", "SAEZ", null);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Active);
        var (_, existingWear) = AddWearable(db, session.UserAircraftId, ratePerHour: 5.0, basePercentage: 10.0);

        // Encendido de motores 1 h después del arranque (AddSession arranca la sesión 2 h atrás) y
        // ETA 2 h después del arranque: el desgaste diferido cubre solo la hora de vuelo hasta la ETA.
        db.SessionEventLogs.Add(new SessionEventLog {
            FlightSessionId = session.Id,
            OccurredAt = session.StartedAt.AddHours(1),
            Category = SessionEventCategory.EnginesStarted,
            EventCode = "ENGINES_STARTED",
            Description = "Motores encendidos",
            FlightPhase = ComplexFlightPhaseEnum.Taxi
        });
        var plan = db.FlightPlans.First(p => p.Id == session.FlightPlanId);
        plan.EstimatedArrivalUtc = session.StartedAt.AddHours(2);
        db.SaveChanges();

        var result = await service.FinalizeUnfinishedSessionAsync(session.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(db.FlightSessions.First().FinishedAt.HasValue);
        Assert.Equal(15.0, db.AircraftSystemWears.Single(w => w.Id == existingWear.Id).WearPercentage, precision: 6);
        Assert.Contains(db.TriggeredFailures.Where(f => f.FlightSessionId == session.Id),
            f => f.State == FailureLifecycleState.NotResolved);
    }

    [Fact]
    public async Task FinalizeUnfinishedSessionAsync_WithoutEnginesStarted_DoesNotApplyWear() {
        var db = CreateContext();
        var service = CreateSutWithWear(db);
        var session = AddSession(db, RiskLevel.Moderate, "SUMU", "SAEZ", null);
        AddTriggeredFailure(db, session.Id, FailureLifecycleState.Active);
        var (_, existingWear) = AddWearable(db, session.UserAircraftId, ratePerHour: 5.0, basePercentage: 10.0);

        // ETA 2 h después del arranque, pero sin ENGINES_STARTED registrado: no acumula desgaste.
        var plan = db.FlightPlans.First(p => p.Id == session.FlightPlanId);
        plan.EstimatedArrivalUtc = session.StartedAt.AddHours(2);
        db.SaveChanges();

        var result = await service.FinalizeUnfinishedSessionAsync(session.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(db.FlightSessions.First().FinishedAt.HasValue);
        Assert.Equal(10.0, db.AircraftSystemWears.Single(w => w.Id == existingWear.Id).WearPercentage, precision: 6);
    }
}
