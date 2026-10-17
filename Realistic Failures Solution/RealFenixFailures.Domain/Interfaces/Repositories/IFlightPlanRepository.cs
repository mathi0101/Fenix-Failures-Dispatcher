using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IFlightPlanRepository {
    Task<FlightPlan?> GetByIdAsync(int id, CancellationToken ct);
    Task<FlightPlan> AddAsync(FlightPlan plan, CancellationToken ct);
    Task<FlightPlan?> GetLatestForAircraftTypeAsync(string icaoTypeCode, CancellationToken ct);
}
