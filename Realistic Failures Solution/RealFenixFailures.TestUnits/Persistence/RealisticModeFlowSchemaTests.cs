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

    private static readonly int[] ExpectedAtaIds = [21, 22, 23, 24, 26, 27, 28, 29, 30, 31, 32, 34, 35, 36, 46, 49, 52, 70, 80];

    [Fact]
    public async Task SeedAircraftWearableSystems_Provides19AtaSystems_WithUniqueAtaIds() {
        await using var db = CreateContext();

        var systems = await db.AircraftWearableSystems.OrderBy(s => s.Id).ToListAsync();

        Assert.Equal(19, systems.Count);
        Assert.Equal(ExpectedAtaIds, systems.Select(s => s.AtaId).OrderBy(a => a).ToArray());
        Assert.Equal(19, systems.Select(s => s.AtaId).Distinct().Count());
        Assert.Equal(19, systems.Select(s => s.DisplayOrder).Distinct().Count());
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

    [Fact]
    public void FailureEngineSettings_RealisticMode_HasObjectiveDefaults() {
        var settings = new FailureEngineSettings();

        Assert.False(settings.Debug);
        Assert.Equal(50, settings.MaxLogEntries);
        Assert.Equal(30, settings.CheckConnectionIntervalSeconds);
        Assert.Equal(10, settings.CheckIntervalSeconds);
        Assert.Equal(60, settings.RealisticMode.EvaluationIntervalSeconds);
        Assert.Equal(5, settings.RealisticMode.PreparedQueueLimit);
        Assert.True(settings.RealisticMode.GroundFailureEnabled);
        Assert.Equal(50, settings.RealisticMode.ThinkingLinesCap);
        Assert.Equal(2, settings.RealisticMode.Ofp.PreStartWindowHours);
        Assert.Equal(0.5, settings.RealisticMode.Ofp.PostArrivalWindowHours);
        Assert.Equal(0.5, settings.RealisticMode.Wear.AmplificationFactor);
        Assert.Equal(80, settings.RealisticMode.Wear.ThresholdNoticePct);
    }
}
