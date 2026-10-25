using Microsoft.EntityFrameworkCore;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Interfaces.Repositories;
using RealFenixFailures.Infrastructure.Persistence;

namespace RealFenixFailures.Infrastructure.Repositories;

public class FlightSessionRepository : IFlightSessionRepository {
    private readonly RealFenixDbContext _dbContext;

    public FlightSessionRepository(RealFenixDbContext dbContext) {
        _dbContext = dbContext;
    }


    public async Task<FlightSession?> GetByIdAsync(int id, CancellationToken ct) {
        return await _dbContext.FlightSessions
            .Include(x => x.UserAircraft)
            .Include(x => x.TriggeredFailures)
                .ThenInclude(x=>x.FenixFailure)
                .ThenInclude(x=>x!.Group)
                .ThenInclude(x=>x.System)
            .Include(x => x.EventLogs)
            .Include(x => x.FlightPlan)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<FlightSession>> GetUnfinishedAsync(CancellationToken ct) {
        return await _dbContext.FlightSessions
            .AsNoTracking()
            .Include(x => x.TriggeredFailures)
            .Include(x => x.EventLogs)
            .Include(x => x.FlightPlan)
            .AsSplitQuery()
            .Where(x => x.FinishedAt == null)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);
    }

    public async Task<FlightSession> AddAsync(FlightSession session, CancellationToken ct) {
        var result = await _dbContext.FlightSessions.AddAsync(session, ct);
        return result.Entity;
    }

    public async Task AddTriggeredFailureAsync(TriggeredFailure triggeredFailure, CancellationToken ct) {
        await _dbContext.TriggeredFailures.AddAsync(triggeredFailure, ct);
    }

    public async Task AddEventLogAsync(SessionEventLog eventLog, CancellationToken ct) {
        await _dbContext.SessionEventLogs.AddAsync(eventLog, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct) {
        var session = await _dbContext.FlightSessions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"FlightSession {id} not found.");

        _dbContext.FlightSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SessionSystemWearDelta>> GetWearDeltasAsync(int sessionId, CancellationToken ct) {
        var deltas = await _dbContext.SessionSystemWearDeltas
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .OrderBy(x => x.AtaId)
            .ToListAsync(ct);
        return deltas;
    }

    public void AddWearDelta(SessionSystemWearDelta delta) {
        _dbContext.SessionSystemWearDeltas.Add(delta);
    }

    public Task SaveChangesAsync(CancellationToken ct) {
        return _dbContext.SaveChangesAsync(ct);
    }
}
