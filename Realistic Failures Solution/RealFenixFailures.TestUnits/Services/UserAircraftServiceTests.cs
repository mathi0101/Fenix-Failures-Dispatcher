// RealFenixFailures.TestUnits/Services/UserAircraftServiceTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Services;

namespace RealFenixFailures.TestUnits.Services;

public class UserAircraftServiceTests {
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

    [Fact]
    public async Task GetAllAsync_ComputesTotalsFromFinishedSessionsOnly() {
        await using var db = CreateContext();
        var aircraft = new UserAircraft {
            Registration = "LV-XYZ",
            IcaoTypeCode = "A320",
            CreatedAt = DateTime.UtcNow
        };
        db.UserAircrafts.Add(aircraft);
        db.SaveChanges();

        db.FlightSessions.AddRange(
            new FlightSession {
                UserAircraftId = aircraft.Id,
                StartedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc),
                RiskLevel = RiskLevel.Moderate
            },
            new FlightSession {
                UserAircraftId = aircraft.Id,
                StartedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
                RiskLevel = RiskLevel.Low
            },
            new FlightSession {
                UserAircraftId = aircraft.Id,
                StartedAt = new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc),
                RiskLevel = RiskLevel.High
            });
        db.SaveChanges();

        var service = new UserAircraftService(db);
        var result = await service.GetAllAsync(CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(2, dto.TotalFlights);
        Assert.Equal(3.5, dto.TotalFlightHours, 3);
    }

    [Fact]
    public async Task GetDashboardAsync_ComputesTotalsAndFailureCount() {
        await using var db = CreateContext();
        var aircraft = new UserAircraft {
            Registration = "LV-ABC",
            IcaoTypeCode = "B738",
            CreatedAt = DateTime.UtcNow
        };
        db.UserAircrafts.Add(aircraft);
        db.SaveChanges();

        var session = new FlightSession {
            UserAircraftId = aircraft.Id,
            StartedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 1, 1, 11, 15, 0, DateTimeKind.Utc),
            RiskLevel = RiskLevel.Moderate
        };
        db.FlightSessions.Add(session);
        db.SaveChanges();

        db.FenixFailureDefinitions.Add(new FenixFailureDefinition {
            FenixFailureId = "F_ENGINE_1",
            Name = "Motor 1",
            Group = new FenixFailureGroup {
                Name = "Engines",
                System = new FenixFailureSystem { Name = "Propulsion", ShortName = "PROP" }
            }
        });
        db.SaveChanges();

        db.TriggeredFailures.Add(new TriggeredFailure {
            FlightSessionId = session.Id,
            FenixFailureId = "F_ENGINE_1",
            FailureName = "Motor 1",
            TriggeredAt = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc),
            FlightPhase = ComplexFlightPhaseEnum.Climb
        });
        db.SaveChanges();

        var service = new UserAircraftService(db);
        var dashboard = await service.GetDashboardAsync(aircraft.Id, CancellationToken.None);

        Assert.Equal(1, dashboard.Aircraft.TotalFlights);
        Assert.Equal(1.25, dashboard.Aircraft.TotalFlightHours, 3);
        Assert.Equal(1, dashboard.TotalFailuresTriggered);
    }
}
