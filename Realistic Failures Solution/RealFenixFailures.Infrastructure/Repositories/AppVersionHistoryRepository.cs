using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class AppVersionHistoryRepository : IAppVersionHistoryRepository {
    private readonly RealFenixDbContext _dbContext;

    public AppVersionHistoryRepository(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }

    public async Task<string?> GetLatestVersionAsync(CancellationToken ct) {
        return await _dbContext.AppVersionHistories
            .OrderByDescending(x => x.Id)
            .Select(x => x.AppVersion)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(AppVersionHistory entry, CancellationToken ct) {
        await _dbContext.AppVersionHistories.AddAsync(entry, ct);
        await _dbContext.SaveChangesAsync(ct);
    }
}
