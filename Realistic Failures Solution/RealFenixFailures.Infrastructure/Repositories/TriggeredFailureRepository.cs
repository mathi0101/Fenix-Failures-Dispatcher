using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class TriggeredFailureRepository : ITriggeredFailureRepository
{
    private readonly RealFenixDbContext _dbContext;

    public TriggeredFailureRepository(RealFenixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TriggeredFailure> AddAsync(TriggeredFailure triggeredFailure, CancellationToken cancellationToken)
    {
        var result = await _dbContext.TriggeredFailures.AddAsync(triggeredFailure, cancellationToken);
        return result.Entity;
    }

    /// <summary>
    /// Fallas aún en curso del ciclo de vida (Sent, Queued, Preparing, Active) para una sesión.
    /// Base para que el engine reconcilie contra la lectura real de Fenix (confirmación, resolución por piloto).
    /// </summary>
    public async Task<IReadOnlyList<TriggeredFailure>> GetActiveForSessionAsync(int sessionId, CancellationToken ct)
    {
        var states = new[]
        {
            FailureLifecycleState.Preparing,
            FailureLifecycleState.Queued,
            FailureLifecycleState.Sent,
            FailureLifecycleState.Active
        };
        return await _dbContext.TriggeredFailures
            .Where(x => x.FlightSessionId == sessionId && states.Contains(x.State))
            .OrderBy(x => x.TriggeredAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Falla activa (Sent/Preparing/Queued/Active) de la sesión, rastreada por EF para poder persistir
    /// una transición del ciclo de vida (confirmación o resolución). Null si no está en curso o ya resolvió.
    /// </summary>
    public async Task<TriggeredFailure?> GetActiveForUpdateAsync(int sessionId, string fenixFailureId, CancellationToken ct)
    {
        var states = new[]
        {
            FailureLifecycleState.Preparing,
            FailureLifecycleState.Queued,
            FailureLifecycleState.Sent,
            FailureLifecycleState.Active
        };
        return await _dbContext.TriggeredFailures
            .Where(x => x.FlightSessionId == sessionId && x.FenixFailureId == fenixFailureId && states.Contains(x.State))
            .OrderByDescending(x => x.TriggeredAt)
            .FirstOrDefaultAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
