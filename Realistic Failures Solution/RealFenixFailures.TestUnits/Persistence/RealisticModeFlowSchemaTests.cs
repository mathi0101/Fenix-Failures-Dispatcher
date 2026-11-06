// RealFenixFailures.TestUnits/Persistence/RealisticModeFlowSchemaTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.TestUnits.Persistence;

public class RealisticModeFlowSchemaTests {
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
    public async Task SeedAircraftWearableSystems_MatchesSeedDataCatalog_WithUniqueAtaIdsAndDisplayOrder() {
        await using var db = CreateContext();

        var systems = await db.AircraftWearableSystems.OrderBy(s => s.AtaId).ToListAsync();

        // El catálogo sembrado en BD replica la fuente del seed (misma referencia que usa la migración),
        // sin valores literales: curar SeedData actualiza el test sin tocarlo.
        Assert.Equal(SeedData.WearableSystems.Count, systems.Count);
        Assert.Equal(
            SeedData.WearableSystems.Select(s => s.AtaId).OrderBy(a => a),
            systems.Select(s => s.AtaId));
        Assert.Equal(systems.Count, systems.Select(s => s.AtaId).Distinct().Count());
        Assert.Equal(systems.Count, systems.Select(s => s.DisplayOrder).Distinct().Count());
        Assert.All(systems, s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
        Assert.All(systems, s => Assert.False(string.IsNullOrWhiteSpace(s.ShortName)));
    }

    [Fact]
    public void SessionEventCategory_HasAdditiveValues() {
        Assert.Equal(9, (int)SessionEventCategory.Pause);
        Assert.Equal(10, (int)SessionEventCategory.ResumeOrReconnect);
        Assert.Equal(11, (int)SessionEventCategory.FailureStateChanged);
        Assert.Equal(12, (int)SessionEventCategory.WearUpdated);
        Assert.Equal(13, (int)SessionEventCategory.OfpLoaded);
        Assert.Equal(14, (int)SessionEventCategory.SessionRecovered);
    }
}
