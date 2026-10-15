using RealFenixFailures.Domain.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Helpers;
using RealFenixFailures.Domain.Interfaces;

namespace RealFenixFailures.Domain.Services;

public class TestPresetsLoader : IPresetsLoader {
    private readonly List<FailurePreset> _trainingPresets;
    private readonly List<FailurePreset> _realisticPresets;

    public TestPresetsLoader() {
        _trainingPresets = LoadTrainingPresets()
            .Select(p => new FailurePreset {
                Name = p.Name,
                Description = p.Description,
                TriggerDescription = p.TriggerDescription,
                FlightPhase = p.Phase,
                Difficulty = p.Difficulty,
                PresetType = PresetTypeEnum.TrainingMode,
                PresetFailureDefinitions = p.Failures?.ToList() ?? []
            })
            .ToList();

        _realisticPresets = LoadRealisticPresets()
            .Select(p => new FailurePreset {
                Name = p.Name,
                Description = p.Description,
                TriggerDescription = p.TriggerDescription,
                FlightPhase = p.Phase,
                Difficulty = p.Difficulty,
                PresetType = PresetTypeEnum.RealisticMode,
                PresetFailureDefinitions = p.Failures?.ToList() ?? []
            })
            .ToList();
    }

    public async Task<IReadOnlyList<FailurePreset>> GetTrainingPresetsAsync(CancellationToken ct) {
        return await Task.FromResult<IReadOnlyList<FailurePreset>>(_trainingPresets);
    }

    public async Task<IReadOnlyList<FailurePreset>> GetRealisticPresetsAsync(CancellationToken ct) {
        return await Task.FromResult<IReadOnlyList<FailurePreset>>(_realisticPresets);
    }
    #region Training
    private IReadOnlyCollection<PreloadedPresetDto> LoadTrainingPresets() {
        return new List<PreloadedPresetDto>
        {
            new PreloadedPresetDto(
                "Engine Failure Before V1",
                "Falla de motor antes de V1. Requiere rejected takeoff.",
                "Se dispara antes de alcanzar V1. Procedimiento: RTO.",
                ComplexFlightPhaseEnum.Takeoff,
                DifficultyEnum.Easy,
                new List<PresetFailureDefinition>
                {
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_1_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(70, 130),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_2_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(70, 130),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Wheel_brake_fault.Wheel_brake_fault_1,
                        Probability = 0.4,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Wheel_brake_fault.Wheel_brake_fault_2,
                        Probability = 0.4,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    },new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Wheel_brake_fault.Wheel_brake_fault_3,
                        Probability = 0.4,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Wheel_brake_fault.Wheel_brake_fault_4,
                        Probability = 0.4,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Tyre_pressure.Tyre_pressure_main_1_low,
                        ProbabilityGroup=3,
                        Probability = 0.5,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Landing_gear.Tyre_pressure.Tyre_pressure_main_2_low,
                        ProbabilityGroup=3,
                        Probability = 0.5,
                        Ias = FenixHelper.Intervalos.AbiertoEntre(20, 70),
                    }
                }
            ),

            new PreloadedPresetDto(
                "Engine Failure After V1",
                "Falla de motor durante el roll de despegue, después de alcanzar V1.",
                "Se dispara automáticamente al detectar V1 durante el despegue.",
                ComplexFlightPhaseEnum.Takeoff,
                DifficultyEnum.Hard,
                new List<PresetFailureDefinition>
                {
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_1_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        AfterEvent=FenixEvents.V1,
                        AfterEventSeconds = FenixHelper.Intervalos.AbiertoEntre(1, 3),
                    },
                    new PresetFailureDefinition
                    {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_2_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        AfterEvent=FenixEvents.V1,
                        AfterEventSeconds = FenixHelper.Intervalos.AbiertoEntre(1, 3),
                    }
                }
            ),

            new PreloadedPresetDto(
                "Engine Failure After V2",
                "Falla de motor luego de pasar V2 en ascenso inicial.",
                "Se dispara al superar V2 en el ascenso inicial.",
                ComplexFlightPhaseEnum.Climb,
                DifficultyEnum.Medium,
                [
                    new() {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_1_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        AfterEvent=FenixEvents.TakeOff,
                        AfterEventSeconds = FenixHelper.Intervalos.AbiertoEntre(1, 15),
                    },
                    new() {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_2_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                        AfterEvent=FenixEvents.TakeOff,
                        AfterEventSeconds = FenixHelper.Intervalos.AbiertoEntre(1, 15),
                    }
                ]
            ),

            new PreloadedPresetDto(
                "Hydraulic System Failure",
                "Pérdida del sistema hidráulico en crucero.",
                "Se dispara aleatoriamente durante la fase de crucero.",
                ComplexFlightPhaseEnum.Cruise,
                DifficultyEnum.Easy,
                new List<PresetFailureDefinition>
                {
                    // TODO: reemplazar por los IDs reales de hidráulico cuando los tengas
                }
            ),

            new PreloadedPresetDto(
                "Dual Bleed Failure",
                "Falla de sangrado en ambos motores. Pérdida de presurización.",
                "Se dispara en crucero. Requiere descenso de emergencia.",
                ComplexFlightPhaseEnum.Cruise,
                DifficultyEnum.Medium,
                new List<PresetFailureDefinition>
                {
                    // TODO: reemplazar por los IDs reales de bleed cuando los tengas
                }
            ),

            new PreloadedPresetDto(
                "GPWS Warning on Approach",
                "Activación de GPWS durante la aproximación final.",
                "Se dispara en la aproximación final por debajo de 1000ft AGL.",
                ComplexFlightPhaseEnum.Approach,
                DifficultyEnum.Easy,
                new List<PresetFailureDefinition>
                {
                    // TODO: reemplazar por el ID real de GPWS cuando lo tengas
                }
            ),

            new PreloadedPresetDto(
                "Gear Not Down on Final",
                "Tren de aterrizaje no baja correctamente en la final.",
                "Se dispara al seleccionar gear down en la aproximación.",
                ComplexFlightPhaseEnum.Approach,
                DifficultyEnum.Medium,
                new List<PresetFailureDefinition>
                {
                    // TODO: reemplazar por el ID real de gear cuando lo tengas
                }
            ),
        };
    }

    #endregion
    #region Realistic
    private IReadOnlyCollection<PreloadedPresetDto> LoadRealisticPresets() {
        return new List<PreloadedPresetDto>
        {
            new PreloadedPresetDto(
                "Falla de motor en crucero",
                "Ejemplo realista: falla de motor en fase de crucero.",
                "Se dispara aleatoriamente durante el crucero.",
                ComplexFlightPhaseEnum.Cruise,
                DifficultyEnum.Medium,
                [
                    new() {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_1_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                    },
                    new() {
                        FenixFailureId = FenixFailures.Power_plant.Engine_failure.Eng_2_failure,
                        ProbabilityGroup = 1,
                        Probability = 0.5,
                    }
                ]
            ),
        };
    }

    #endregion
}