using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IAircraftSystemWearRepository {
    /// <summary>
    /// Desgaste de todos los sistemas de una aeronave, rastreado por EF e incluyendo el catálogo
    /// (<see cref="AircraftWearableSystem"/>), para poder acumular por <c>WearPerFlightHour</c>.
    /// Inicializa en cero los sistemas del catálogo que faltan para la aeronave, sin modificar los
    /// existentes. Las filas nuevas se persisten con <see cref="SaveAsync"/> junto con la acumulación.
    /// </summary>
    Task<IReadOnlyList<AircraftSystemWear>> GetForAircraftAsync(int userAircraftId, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
