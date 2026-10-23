using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class AircraftSystemWearRepository : IAircraftSystemWearRepository
{
    private readonly RealFenixDbContext _dbContext;

    public AircraftSystemWearRepository(RealFenixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AircraftSystemWear>> GetForAircraftAsync(int userAircraftId, CancellationToken ct)
    {
        return await _dbContext.AircraftSystemWears
            .Include(w => w.WearableSystem)
            .Where(w => w.UserAircraftId == userAircraftId)
            .OrderBy(w => w.WearableSystem != null ? w.WearableSystem.DisplayOrder : 0)
            .ToListAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
