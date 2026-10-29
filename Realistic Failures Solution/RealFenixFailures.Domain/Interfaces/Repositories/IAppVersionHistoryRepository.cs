using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IAppVersionHistoryRepository {
    Task<string?> GetLatestVersionAsync(CancellationToken ct);
    Task AddAsync(AppVersionHistory entry, CancellationToken ct);
}
