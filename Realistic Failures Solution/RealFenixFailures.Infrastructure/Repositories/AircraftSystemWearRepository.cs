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
        var wears = await _dbContext.AircraftSystemWears
            .Include(w => w.WearableSystem)
            .Where(w => w.UserAircraftId == userAircraftId)
            .OrderBy(w => w.WearableSystem != null ? w.WearableSystem.DisplayOrder : 0)
            .ToListAsync(ct);
        var existingSystemIds = wears.Select(w => w.WearableSystemId).ToHashSet();
        var catalog = await _dbContext.AircraftWearableSystems.ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var system in catalog.Where(s => !existingSystemIds.Contains(s.Id))) {
            var wear = new AircraftSystemWear {
                UserAircraftId = userAircraftId,
                WearableSystemId = system.Id,
                WearableSystem = system,
                WearPercentage = 0,
                LastUpdatedAt = now
            };
            _dbContext.AircraftSystemWears.Add(wear);
            wears.Add(wear);
        }
        return wears.OrderBy(w => w.WearableSystem?.DisplayOrder ?? 0).ToArray();
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
