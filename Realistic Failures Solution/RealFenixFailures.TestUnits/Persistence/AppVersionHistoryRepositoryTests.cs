using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;

namespace RealFenixFailures.TestUnits.Persistence;

public class AppVersionHistoryRepositoryTests {
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
    public async Task GetLatestVersionAsync_SinDatos_DevuelveNull() {
        using var context = CreateContext();
        var repository = new AppVersionHistoryRepository(context);

        var latest = await repository.GetLatestVersionAsync(CancellationToken.None);

        Assert.Null(latest);
    }

    [Fact]
    public async Task GetLatestVersionAsync_ConVariasFilas_DevuelveLaUltimaPorId() {
        using var context = CreateContext();
        var repository = new AppVersionHistoryRepository(context);

        await repository.AddAsync(new AppVersionHistory { AppVersion = "0.3.0", DetectedAtUtc = DateTime.UtcNow }, CancellationToken.None);
        await repository.AddAsync(new AppVersionHistory { AppVersion = "0.4.1-beta.1", DetectedAtUtc = DateTime.UtcNow }, CancellationToken.None);

        var latest = await repository.GetLatestVersionAsync(CancellationToken.None);

        Assert.Equal("0.4.1-beta.1", latest);
    }
}
