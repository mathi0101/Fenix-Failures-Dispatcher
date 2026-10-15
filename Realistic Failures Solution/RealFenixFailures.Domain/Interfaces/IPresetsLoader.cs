using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces;

public interface IPresetsLoader {
    Task<IReadOnlyList<FailurePreset>> GetTrainingPresetsAsync(CancellationToken ct);
    Task<IReadOnlyList<FailurePreset>> GetRealisticPresetsAsync(CancellationToken ct);
}
