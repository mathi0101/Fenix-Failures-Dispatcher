using Microsoft.Extensions.Options;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.Infrastructure.Services;

namespace RealFenixFailures.TestUnits.Services;

public class PresetsLoaderTests {
    [Fact]
    public async Task GetRealisticPresetsAsync_LoadsCatalog_AllRealisticMode_WithFailuresAndFullPhaseCoverage() {
        var settings = Options.Create(new FailureEngineSettings {
            RealisticPresetsJson = "realistic_presets.json"
        });
        var loader = new PresetsLoader(settings);

        var presets = await loader.GetRealisticPresetsAsync(CancellationToken.None);

        Assert.Equal(44, presets.Count);
        Assert.All(presets, p => Assert.Equal(PresetTypeEnum.RealisticMode, p.PresetType));
        Assert.All(presets, p => Assert.NotEmpty(p.PresetFailureDefinitions));

        var coveredPhases = presets.Select(p => (int)p.FlightPhase).Distinct().OrderBy(p => p).ToList();
        Assert.Equal(9, coveredPhases.Count);
        for (var phase = 1; phase <= 9; phase++) {
            Assert.Contains(presets, p => (int)p.FlightPhase == phase);
        }

        Assert.Equal(Enumerable.Range(1, 9), coveredPhases);
    }
}
