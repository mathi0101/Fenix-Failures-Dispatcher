// RealFenixFailures.TestUnits/Services/UserAircraftServiceTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Application.DTOs;
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

    private static FlightPlan AddFlightPlan(RealFenixDbContext db) {
        var now = DateTime.UtcNow;
        var plan = new FlightPlan {
            OriginIcao = "SAEZ",
            DestinationIcao = "SBGR",
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
        var plan = AddFlightPlan(db);

        db.FlightSessions.AddRange(
            new FlightSession {
                UserAircraftId = aircraft.Id,
                FlightPlanId = plan.Id,
                StartedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc),
                RiskLevel = RiskLevel.Moderate
            },
            new FlightSession {
                UserAircraftId = aircraft.Id,
                FlightPlanId = plan.Id,
                StartedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
                RiskLevel = RiskLevel.Low
            },
            new FlightSession {
                UserAircraftId = aircraft.Id,
                FlightPlanId = plan.Id,
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
            FlightPlanId = AddFlightPlan(db).Id,
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

    [Fact]
    public async Task CreateAsync_TrimsAndUppercasesRegistration() {
        await using var db = CreateContext();
        var service = new UserAircraftService(db);

        var dto = await service.CreateAsync(
            new CreateUserAircraftRequest { Registration = " lv-ab ", IcaoTypeCode = "A319" },
            CancellationToken.None);

        Assert.Equal("LV-AB", dto.Registration);
        Assert.Equal("A319", dto.IcaoTypeCode);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidRegistrations() {
        await using var db = CreateContext();
        var service = new UserAircraftService(db);

        var tooLong = new CreateUserAircraftRequest { Registration = "12345678901", IcaoTypeCode = "A320" };
        var whitespaceOnly = new CreateUserAircraftRequest { Registration = "   ", IcaoTypeCode = "A320" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(tooLong, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(whitespaceOnly, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_RejectsUnsupportedIcaoTypeCode() {
        await using var db = CreateContext();
        var service = new UserAircraftService(db);

        var request = new CreateUserAircraftRequest { Registration = "LV-AB", IcaoTypeCode = "B738" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_AcceptsAllSupportedIcaoTypeCodes() {
        await using var db = CreateContext();
        var service = new UserAircraftService(db);

        foreach (var icao in new[] { "A319", "A320", "A321" }) {
            var dto = await service.CreateAsync(
                new CreateUserAircraftRequest { Registration = "LV-" + icao, IcaoTypeCode = icao },
                CancellationToken.None);

            Assert.Equal(icao, dto.IcaoTypeCode);
        }
    }

    [Fact]
    public async Task GetDashboardAsync_AfterExternalWearUpdate_ReadsPersistedValue() {
        await using var db = CreateContext();
        var aircraft = new UserAircraft { Registration = "LV-FRESH", IcaoTypeCode = "A320", CreatedAt = DateTime.UtcNow };
        var system = new AircraftWearableSystem { Name = "Electrical", ShortName = "Elec" };
        var wear = new AircraftSystemWear { UserAircraft = aircraft, WearableSystem = system, WearPercentage = 50, LastUpdatedAt = DateTime.UtcNow };
        db.Add(wear);
        await db.SaveChangesAsync();
        var service = new UserAircraftService(db);
        Assert.Equal(50, Assert.Single((await service.GetDashboardAsync(aircraft.Id, CancellationToken.None)).SystemWears).WearPercentage);

        // Simula el guardado del scope de sesión sin refrescar las entidades trackeadas de la UI.
        await db.AircraftSystemWears.Where(w => w.Id == wear.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.WearPercentage, 51.2));

        var dashboard = await service.GetDashboardAsync(aircraft.Id, CancellationToken.None);

        Assert.Equal(51.2, Assert.Single(dashboard.SystemWears).WearPercentage, 6);
    }

    [Fact]
    public async Task GetAllAsync_IncludesSystemWearsWithShortNameAndPercentage() {
        await using var db = CreateContext();
        var aircraft = new UserAircraft {
            Registration = "LV-WEAR",
            IcaoTypeCode = "A320",
            CreatedAt = DateTime.UtcNow
        };
        db.UserAircrafts.Add(aircraft);
        db.SaveChanges();

        var system = new AircraftWearableSystem {
            Name = "Motores",
            ShortName = "PROP",
            DisplayOrder = 1
        };
        db.AircraftWearableSystems.Add(system);
        db.SaveChanges();

        db.AircraftSystemWears.Add(new AircraftSystemWear {
            UserAircraftId = aircraft.Id,
            WearableSystemId = system.Id,
            WearPercentage = 42.5,
            LastUpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var service = new UserAircraftService(db);
        var result = await service.GetAllAsync(CancellationToken.None);

        var dto = Assert.Single(result);
        var wear = Assert.Single(dto.SystemsWear);
        Assert.Equal("PROP", wear.SystemWear.ShortName);
        Assert.Equal(42.5, wear.WearPercentage);
    }
}
