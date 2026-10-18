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

    private static FlightSession AddSession(RealFenixDbContext db, RiskLevel risk, string? origin, string? destination, DateTime? finishedAt) {
        var aircraft = AddAircraft(db);
        var session = new FlightSession {
            UserAircraftId = aircraft.Id,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            RiskLevel = risk,
            OriginIcao = origin,
            DestinationIcao = destination,
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
        Assert.Equal(5, result.Summary.TotalFailures);
        Assert.Equal(1, result.Summary.ActiveAtEnd);
        Assert.Equal(2, result.Summary.ResolvedByPilot);
        Assert.Equal(1, result.Summary.AutoResolved);
        Assert.Equal(1, result.Summary.Rejected);
        Assert.Equal(3, result.Summary.ResolvedCount);
        Assert.Equal(1, result.Summary.Critical);
        Assert.Equal(1, result.Summary.Major);
        Assert.Equal(3, result.Summary.Minor);
        Assert.Equal(5, result.Failures.Count);
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

        var result = await service.FinalizeSessionAsync(session.Id, DateTime.UtcNow, CancellationToken.None);

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
}
