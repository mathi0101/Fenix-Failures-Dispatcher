using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IAircraftSystemWearRepository {
    /// <summary>
    /// Desgaste de todos los sistemas de una aeronave, rastreado por EF e incluyendo el catálogo
    /// (<see cref="AircraftWearableSystem"/>), para poder acumular por <c>WearPerFlightHour</c>.
    /// </summary>
    Task<IReadOnlyList<AircraftSystemWear>> GetForAircraftAsync(int userAircraftId, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
