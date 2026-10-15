using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.Application.Services;

public class PresetService : IPresetService {
    private readonly IPresetRepository _presetRepository;
    private readonly IPresetsLoader _presetsLoader;

    public PresetService(IPresetRepository presetRepository, IPresetsLoader presetsLoader) {
        _presetRepository = presetRepository;
        _presetsLoader = presetsLoader;
    }

    public async Task InitializeAsync(CancellationToken ct) {
        var hasTrainingPresets = await _presetRepository.GetAnyAsync(PresetTypeEnum.TrainingMode, ct);
        if (!hasTrainingPresets) {
            var presets = await _presetsLoader.GetTrainingPresetsAsync(ct);
            await _presetRepository.AddAsync(presets, ct);
        }

        var hasRealisticPresets = await _presetRepository.GetAnyAsync(PresetTypeEnum.RealisticMode, ct);
        if (!hasRealisticPresets) {
            var presets = await _presetsLoader.GetRealisticPresetsAsync(ct);
            await _presetRepository.AddAsync(presets, ct);
        }
    }

    public Task DeletePresetAsync(int id, CancellationToken ct) {
        throw new NotImplementedException();
    }

    public async Task<IReadOnlyList<FailurePreset>> GetTrainingPresetsAsync(CancellationToken ct) {
        var presets = await _presetRepository.GetAllAsync(PresetTypeEnum.TrainingMode, ct);
        return presets;
    }

    public async Task<IReadOnlyList<FailurePreset>> GetRealisticPresetsAsync(CancellationToken ct) {
        var presets = await _presetRepository.GetAllAsync(PresetTypeEnum.RealisticMode, ct);
        return presets;
    }

    public async Task<IReadOnlyList<FailurePreset>> GetCustomPresetsAsync(CancellationToken ct) {
        var presets = await _presetRepository.GetAllAsync(PresetTypeEnum.Custom, ct);
        return presets;
    }

    public async Task<FailurePreset?> GetByIdAsync(int presetId, CancellationToken ct) {
        var preset = await _presetRepository.GetByIdAsync(presetId, ct);
        return preset;
    }

    public Task<FailurePreset> CreateEmptyCustomPresetAsync(CancellationToken none) {
        throw new NotImplementedException();
    }
}
