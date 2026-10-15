using Moq;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.TestUnits.Services;

public class PresetServiceTests {
    private readonly Mock<IPresetRepository> _repository;
    private readonly Mock<IPresetsLoader> _loader;
    private readonly PresetService _service;

    public PresetServiceTests() {
        _repository = new Mock<IPresetRepository>();
        _loader = new Mock<IPresetsLoader>();
        _service = new PresetService(_repository.Object, _loader.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenNothingExists_LoadsAndAddsTrainingBeforeRealistic() {
        _repository.Setup(r => r.GetAnyAsync(It.IsAny<PresetTypeEnum>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var trainingPresets = new List<FailurePreset> { new FailurePreset { Name = "Training preset", PresetType = PresetTypeEnum.TrainingMode } };
        var realisticPresets = new List<FailurePreset> { new FailurePreset { Name = "Realistic preset", PresetType = PresetTypeEnum.RealisticMode } };
        _loader.Setup(l => l.GetTrainingPresetsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trainingPresets);
        _loader.Setup(l => l.GetRealisticPresetsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(realisticPresets);

        var addedLists = new List<IReadOnlyList<FailurePreset>>();
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyList<FailurePreset>>(), It.IsAny<CancellationToken>()))
                   .Callback<IReadOnlyList<FailurePreset>, CancellationToken>((presets, _) => addedLists.Add(presets))
                   .Returns(Task.CompletedTask);

        await _service.InitializeAsync(CancellationToken.None);

        _repository.Verify(r => r.GetAnyAsync(PresetTypeEnum.TrainingMode, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetAnyAsync(PresetTypeEnum.RealisticMode, It.IsAny<CancellationToken>()), Times.Once);
        _loader.Verify(l => l.GetTrainingPresetsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _loader.Verify(l => l.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(2, addedLists.Count);
        Assert.Same(trainingPresets, addedLists[0]);
        Assert.Same(realisticPresets, addedLists[1]);
        Assert.Equal(PresetTypeEnum.TrainingMode, addedLists[0][0].PresetType);
        Assert.Equal(PresetTypeEnum.RealisticMode, addedLists[1][0].PresetType);
    }

    [Fact]
    public async Task InitializeAsync_WhenTrainingExists_SkipsTrainingLoadAndAdd_SeedsRealistic() {
        _repository.Setup(r => r.GetAnyAsync(PresetTypeEnum.TrainingMode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.GetAnyAsync(PresetTypeEnum.RealisticMode, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var realisticPresets = new List<FailurePreset> { new FailurePreset { Name = "Realistic preset", PresetType = PresetTypeEnum.RealisticMode } };
        _loader.Setup(l => l.GetRealisticPresetsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(realisticPresets);

        var addedLists = new List<IReadOnlyList<FailurePreset>>();
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyList<FailurePreset>>(), It.IsAny<CancellationToken>()))
                   .Callback<IReadOnlyList<FailurePreset>, CancellationToken>((presets, _) => addedLists.Add(presets))
                   .Returns(Task.CompletedTask);

        await _service.InitializeAsync(CancellationToken.None);

        _loader.Verify(l => l.GetTrainingPresetsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyList<FailurePreset>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(realisticPresets, addedLists[0]);
        Assert.Equal(PresetTypeEnum.RealisticMode, addedLists[0][0].PresetType);
    }

    [Fact]
    public async Task InitializeAsync_WhenRealisticExists_SkipsRealisticLoadAndAdd_SeedsTraining() {
        _repository.Setup(r => r.GetAnyAsync(PresetTypeEnum.TrainingMode, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.GetAnyAsync(PresetTypeEnum.RealisticMode, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var trainingPresets = new List<FailurePreset> { new FailurePreset { Name = "Training preset", PresetType = PresetTypeEnum.TrainingMode } };
        _loader.Setup(l => l.GetTrainingPresetsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trainingPresets);

        var addedLists = new List<IReadOnlyList<FailurePreset>>();
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyList<FailurePreset>>(), It.IsAny<CancellationToken>()))
                   .Callback<IReadOnlyList<FailurePreset>, CancellationToken>((presets, _) => addedLists.Add(presets))
                   .Returns(Task.CompletedTask);

        await _service.InitializeAsync(CancellationToken.None);

        _loader.Verify(l => l.GetRealisticPresetsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyList<FailurePreset>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(trainingPresets, addedLists[0]);
        Assert.Equal(PresetTypeEnum.TrainingMode, addedLists[0][0].PresetType);
    }
}
