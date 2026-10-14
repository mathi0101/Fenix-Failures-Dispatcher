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
            .Include(x => x.TriggeredFailures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<FlightSession> AddAsync(FlightSession session, CancellationToken ct) {
        var result = await _dbContext.FlightSessions.AddAsync(session, ct);
        return result.Entity;
    }

    public async Task AddTriggeredFailureAsync(TriggeredFailure triggeredFailure, CancellationToken ct) {
        await _dbContext.TriggeredFailures.AddAsync(triggeredFailure, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct) {
        var session = await _dbContext.FlightSessions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"FlightSession {id} not found.");

        _dbContext.FlightSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) {
        return _dbContext.SaveChangesAsync(ct);
    }
}
