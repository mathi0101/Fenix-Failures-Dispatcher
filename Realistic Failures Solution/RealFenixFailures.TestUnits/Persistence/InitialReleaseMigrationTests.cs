using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.TestUnits.Persistence;

public class InitialReleaseMigrationTests {
    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesCurrentSchemaAndSeeds_AndPreservesDataOnRestart() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RealFenixDbContext>().UseSqlite(connection).Options;

        await using (var db = new RealFenixDbContext(options)) {
            Assert.False(db.Database.HasPendingModelChanges());
            await db.Database.MigrateAsync();

            var applied = Assert.Single(await db.Database.GetAppliedMigrationsAsync());
            Assert.EndsWith("_InitialRelease", applied);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(19, await db.AircraftWearableSystems.CountAsync());
            Assert.Equal(4, await db.FailurePresetTypes.CountAsync());
            // Release no incorpora datos de demostración en la migración.
            Assert.Empty(await db.UserAircrafts.ToListAsync());
            Assert.Empty(await db.FailurePresets.ToListAsync());
            Assert.Empty(await db.FlightPlans.ToListAsync());
            Assert.Empty(await db.FlightSessions.ToListAsync());
            Assert.Empty(await db.SessionEventLogs.ToListAsync());
            Assert.Empty(await db.SessionSystemWearDeltas.ToListAsync());
            Assert.Empty(await db.TriggeredFailures.ToListAsync());

            db.UserAircrafts.Add(new UserAircraft {
                Registration = "CX-TEST", IcaoTypeCode = "A320", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var restarted = new RealFenixDbContext(options);
        await restarted.Database.MigrateAsync();
        Assert.Equal("CX-TEST", (await restarted.UserAircrafts.SingleAsync()).Registration);
        Assert.Equal(19, await restarted.AircraftWearableSystems.CountAsync());
    }

    [Fact]
    public async Task InitialRelease_CanRollbackAndReapply() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RealFenixDbContext>().UseSqlite(connection).Options;
        await using var db = new RealFenixDbContext(options);
        await db.Database.MigrateAsync();

        await db.GetService<IMigrator>().MigrateAsync("0");
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());

        await db.Database.MigrateAsync();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(19, await db.AircraftWearableSystems.CountAsync());
    }
}
