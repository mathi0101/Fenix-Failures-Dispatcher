using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class FlightPlanRepository : IFlightPlanRepository {
    private readonly RealFenixDbContext _dbContext;

    public FlightPlanRepository(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }

    public async Task<FlightPlan?> GetByIdAsync(int id, CancellationToken ct) {
        return await _dbContext.FlightPlans
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<FlightPlan?> GetLatestForAircraftTypeAsync(string icaoTypeCode, CancellationToken ct) {
        return await _dbContext.FlightPlans
            .Where(x => x.AircraftIcaoType == icaoTypeCode)
            .OrderByDescending(x => x.DownloadedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FlightPlan> AddAsync(FlightPlan plan, CancellationToken ct) {
        var result = await _dbContext.FlightPlans.AddAsync(plan, ct);
        await _dbContext.SaveChangesAsync(ct);
        return result.Entity;
    }
}
