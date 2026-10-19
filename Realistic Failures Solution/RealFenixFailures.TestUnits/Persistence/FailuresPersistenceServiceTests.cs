using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Persistence;
using RealFenixFailures.Infrastructure.Repositories;
using RealFenixFailures.Infrastructure.Services;
using RealFenixFailures.Integrations.Fenix.Services;

namespace RealFenixFailures.TestUnits.Persistence;

public class FailuresPersistenceServiceTests {
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
    public async Task LoadInitialFailuresAsync_PersisteSeverityYRepairableDelCatalogo() {
        using var context = CreateContext();
        var repository = new FenixFailureDefinitionRepository(context);
        var reader = new FenixStreamFailuresReaderService(NullLogger<FenixStreamFailuresReaderService>.Instance);
        var settings = Options.Create(new FailureEngineSettings { FailuresJson = "failures.json" });
        var service = new FailuresPersistenceService(settings, repository, reader);

        await service.LoadInitialFailuresAsync(CancellationToken.None);

        var definitions = await context.FenixFailureDefinitions.ToListAsync();

        Assert.Equal(384, definitions.Count);

        Assert.Equal(173, definitions.Count(d => d.Severity == FailureSeverity.Minor));
        Assert.Equal(179, definitions.Count(d => d.Severity == FailureSeverity.Major));
        Assert.Equal(32, definitions.Count(d => d.Severity == FailureSeverity.Critical));

        Assert.Equal(20, definitions.Count(d => d.CanBeRepaired));

        var engineFailure = definitions.Single(d => d.FenixFailureId == "F_ENGINE_1");
        Assert.Equal(FailureSeverity.Major, engineFailure.Severity);
        Assert.True(engineFailure.CanBeRepaired);

        var irAlignment = definitions.Single(d => d.FenixFailureId == "F_OH_NAV_IR_ALIGNMENT_1");
        Assert.Equal(FailureSeverity.Minor, irAlignment.Severity);
        Assert.True(irAlignment.CanBeRepaired);

        var dcEssential = definitions.Single(d => d.FenixFailureId == "F_ELEC_BUS_DC_ESSENTIAL");
        Assert.Equal(FailureSeverity.Critical, dcEssential.Severity);
        Assert.False(dcEssential.CanBeRepaired);
    }
}
