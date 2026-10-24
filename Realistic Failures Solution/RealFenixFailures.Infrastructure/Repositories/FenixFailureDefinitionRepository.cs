using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class FenixFailureDefinitionRepository : IFenixFailureDefinitionRepository {
    private readonly RealFenixDbContext _dbContext;

    public FenixFailureDefinitionRepository(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }

    public async Task<bool> HasAnyData(CancellationToken ct) {
        return await _dbContext.FenixFailureDefinitions.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<FenixFailureDefinition>> GetAllWithSystemsAsync(CancellationToken ct) {
        // Tracking (no AsNoTracking): esta consulta se usa para reconciliar navegaciones sobre entidades
        // ya trackeadas por la carga de presets de la sesión; con AsNoTracking se materializarían instancias
        // desancladas y su asignación a navegaciones del contexto produce conflictos de identity en SaveChanges.
        return await _dbContext.FenixFailureDefinitions
            .Include(x => x.Group)
            .ThenInclude(x => x.System)
            .ToListAsync(ct);
    }
    public async Task LoadNewFailuresAsync(IReadOnlyCollection<FenixFailureSystem> systems, CancellationToken ct) {
        if (systems is null || systems.Count == 0)
            return;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try {
            _dbContext.Set<FenixFailureSystem>().AddRange(systems);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        } catch {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
