// RealFenixFailures.TestUnits/Services/FailureTriggerTests.cs
using Moq;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Mocks;

namespace RealFenixFailures.TestUnits.Services;

public class FailureTriggerTests {
    private readonly Mock<IFailureRuleEvaluator> _mockRuleEvaluator;
    private readonly MockRandom _mockRandom;
    private readonly FailureTrigger _failureTrigger;

    public FailureTriggerTests() {
        _mockRuleEvaluator = new Mock<IFailureRuleEvaluator>();
        _mockRandom = new MockRandom();
        _failureTrigger = new FailureTrigger(_mockRuleEvaluator.Object, _mockRandom);
    }

    [Fact]
    public void TryTriggerFailure_ReturnsNull_WhenNoDefinitionsExist() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>()
        };

        // Act
        var result = _failureTrigger.TryTriggerFailure(preset, ComplexFlightPhaseEnum.Cruise, RiskLevel.High, 1.0, Array.Empty<string>());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryTriggerFailure_ReturnsNull_WhenGlobalProbabilityFails() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 1.0 }
            }
        };

        _mockRuleEvaluator.Setup(e => e.EvaluateEligibleFailures(It.IsAny<IEnumerable<PresetFailureDefinition>>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<RiskLevel>(), It.IsAny<IReadOnlyCollection<string>>()))
                          .Returns(new List<PresetFailureDefinition>(preset.PresetFailureDefinitions));

        // Primero NextDouble() -> chequeo global -> devolvemos 0.9 (> 0.5) => no debe continuar
        _mockRandom.SetupNextDouble(0.9);

        // Act
        var result = _failureTrigger.TryTriggerFailure(preset, ComplexFlightPhaseEnum.Cruise, RiskLevel.High, 0.5, Array.Empty<string>());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryTriggerFailure_ReturnsTriggeredFailure_WhenConditionsAreMet() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                // Un único candidato con probabilidad 1.0 -> si pasamos el chequeo global, debe elegirse.
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 1.0 }
            }
        };

        _mockRuleEvaluator.Setup(e => e.EvaluateEligibleFailures(It.IsAny<IEnumerable<PresetFailureDefinition>>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<RiskLevel>(), It.IsAny<IReadOnlyCollection<string>>()))
                          .Returns(new List<PresetFailureDefinition>(preset.PresetFailureDefinitions));

        // Encolamos dos valores:
        // 1) Para el chequeo global (NextDouble() <= globalProbability)
        // 2) Para el roll de selección: TryTriggerFailure genera roll = NextDouble() * totalWeight
        _mockRandom.SetupNextDouble(0.3); // chequeo global (0.3 <= 0.5)
        _mockRandom.SetupNextDouble(0.1); // roll (0.1 * totalWeight(=1.0) => 0.1 -> selecciona al único candidato)

        // Act
        var result = _failureTrigger.TryTriggerFailure(preset, ComplexFlightPhaseEnum.Cruise, RiskLevel.High, 0.5, Array.Empty<string>());

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FID1", result!.FenixFailureId);
    }

    [Fact]
    public void GetTriggeredPresetFailures_SelectsOneFromGroup_WithCorrectWeights() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 0.2, ProbabilityGroup = 1 },
                new PresetFailureDefinition { FenixFailureId = "FID2", Probability = 0.5, ProbabilityGroup = 1 },
                new PresetFailureDefinition { FenixFailureId = "FID3", Probability = 0.3, ProbabilityGroup = 1 }
            }
        };
        // Configurar el shuffle para que cambie el orden:
        _mockRandom.SetupNextInt(2); // Next(0, 3) → devuelve 2
        _mockRandom.SetupNextInt(1); // Next(1, 3) → devuelve 1

        // Alternamos la lista para que sea FID3 -> FID2 -> FID1

        // Para grupos: ProbabilidadEsExitosa(totalWeight=1.0) -> NextDouble() debe devolver <= 1.0 
        _mockRandom.SetupNextDouble(0.45);

        // FID2 ganadora debido a .45 < .8 (.3 + .2)

        // Act
        var result = _failureTrigger.GetTriggeredPresetFailures(preset);

        // Assert
        Assert.Single(result);
        Assert.Equal("FID2", result[0].FenixFailureId);
    }

    [Fact]
    public void GetTriggeredPresetFailures_SelectsNoneInGroup_WhenRollExceedsTotalWeight() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 0.2, ProbabilityGroup = 1 },
                new PresetFailureDefinition { FenixFailureId = "FID2", Probability = 0.3, ProbabilityGroup = 1 }
            }
        };

        // totalWeight = 0.5 -> si NextDouble() devuelve 0.7 (> 0.5), ProbabilidadEsExitosa devuelve false -> no se activa nada
        _mockRandom.SetupNextDouble(0.7);

        // Act
        var result = _failureTrigger.GetTriggeredPresetFailures(preset);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetTriggeredPresetFailures_ActivatesIndependentFailures_Correctly() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 0.8 }, // Independiente
                new PresetFailureDefinition { FenixFailureId = "FID2", Probability = 0.2 }  // Independiente
            }
        };

        // Se evaluarán en orden: FID1 luego FID2
        _mockRandom.SetupNextDouble(0.1); // Para FID1 -> 0.1 <= 0.8 -> se activa
        _mockRandom.SetupNextDouble(0.3); // Para FID2 -> 0.3 > 0.2 -> no se activa

        // Act
        var result = _failureTrigger.GetTriggeredPresetFailures(preset);

        // Assert
        Assert.Single(result);
        Assert.Equal("FID1", result[0].FenixFailureId);
    }

    [Fact]
    public void GetTriggeredPresetFailures_HandlesMixedGroupsAndIndependents() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
                new PresetFailureDefinition { FenixFailureId = "FID1", Probability = 0.3, ProbabilityGroup = 1 },
                new PresetFailureDefinition { FenixFailureId = "FID2", Probability = 0.4, ProbabilityGroup = 1 },
                new PresetFailureDefinition { FenixFailureId = "FID3", Probability = 0.6 }, // Independiente
                new PresetFailureDefinition { FenixFailureId = "FID4", Probability = 0.2 }  // Independiente
            }
        };

        // Orden de consumo:
        // 1) Grupo -> ProbabilidadEsExitosa(totalWeight=0.7) -> NextDouble() = 0.5 => pasa
        //    luego se usa ese mismo roll (0.5) comparado con acumulado (0.3, 0.7) -> cae en FID2
        // 2) Independiente FID3 -> NextDouble() = 0.4 => activa (0.4 <= 0.6)
        // 3) Independiente FID4 -> NextDouble() = 0.3 => no activa (0.3 > 0.2)
        int realRandom = _mockRandom.Next(0, 2);
        _mockRandom.SetupNextInt(realRandom);

        _mockRandom.SetupNextDouble(0.5); // para el grupo
        _mockRandom.SetupNextDouble(0.4); // para FID3
        _mockRandom.SetupNextDouble(0.3); // para FID4

        // Act
        var result = _failureTrigger.GetTriggeredPresetFailures(preset);

        // Assert
        Assert.Equal(2, result.Count);
        string fid = realRandom == 0 ? "FID2" : "FID1";
        Assert.Contains(result, f => f.FenixFailureId == fid);
        Assert.Contains(result, f => f.FenixFailureId == "FID3");
    }

    [Fact]
    public void GetTriggeredPresetFailures_Probabilities_Are_Respected_Statistically() {
        // Arrange
        var preset = new FailurePreset {
            Id = 1,
            PresetFailureDefinitions = new List<PresetFailureDefinition>
            {
            new PresetFailureDefinition { FenixFailureId = "FID_LOW", Probability = 0.1, ProbabilityGroup = 1 },    // 10%
            new PresetFailureDefinition { FenixFailureId = "FID_MEDIUM", Probability = 0.3, ProbabilityGroup = 1 }, // 30%
            new PresetFailureDefinition { FenixFailureId = "FID_HIGH", Probability = 0.6, ProbabilityGroup = 1 }    // 60%
        }
        };

        // Usamos una semilla fija para reproducibilidad en CI
        var rng = new Random(42);
        var failureTrigger = new FailureTrigger(_mockRuleEvaluator.Object, rng);

        // Mock para que siempre devuelva todas las definiciones como elegibles
        _mockRuleEvaluator.Setup(e => e.EvaluateEligibleFailures(It.IsAny<IEnumerable<PresetFailureDefinition>>(), It.IsAny<ComplexFlightPhaseEnum>(), It.IsAny<RiskLevel>(), It.IsAny<IReadOnlyCollection<string>>()))
                          .Returns<IEnumerable<PresetFailureDefinition>, ComplexFlightPhaseEnum, RiskLevel, IReadOnlyCollection<string>>((defs, phase, risk, activeIds) => defs.ToList());

        const int iterations = 100000;
        var counts = new Dictionary<string, int> {
            ["FID_LOW"] = 0,
            ["FID_MEDIUM"] = 0,
            ["FID_HIGH"] = 0,
            ["NONE"] = 0
        };

        // Act: Ejecutar muchas veces
        for (int i = 0; i < iterations; i++) {
            var result = failureTrigger.GetTriggeredPresetFailures(preset);

            if (result.Count == 0) {
                counts["NONE"]++;
            } else {
                var winnerId = result[0].FenixFailureId;
                counts[winnerId]++;
            }
        }

        // Assert: Verificar que las frecuencias observadas están dentro de márgenes aceptables
        var expectedProbabilities = new Dictionary<string, double> {
            ["FID_LOW"] = 0.1,
            ["FID_MEDIUM"] = 0.3,
            ["FID_HIGH"] = 0.6,
            ["NONE"] = 0.0 // 1.0 - (0.1 + 0.3 + 0.6) = 0.0
        };

        // Usamos intervalo de confianza del 95% (Z = 1.96)
        const double confidenceLevel = 1.96;

        foreach (var kvp in expectedProbabilities) {
            string failureId = kvp.Key;
            double expectedProb = kvp.Value;
            double observedCount = counts[failureId];
            double observedProb = observedCount / iterations;

            // Calcular intervalo de confianza usando distribución normal (aproximación binomial)
            if (expectedProb > 0) {
                double standardError = Math.Sqrt(expectedProb * (1 - expectedProb) / iterations);
                double marginOfError = confidenceLevel * standardError;

                double lowerBound = expectedProb - marginOfError;
                double upperBound = expectedProb + marginOfError;

                // Añadimos un pequeño margen adicional por redondeo
                lowerBound = Math.Max(0, lowerBound - 0.001);
                upperBound = Math.Min(1, upperBound + 0.001);

                Assert.True(observedProb >= lowerBound && observedProb <= upperBound,
                    $"Falla {failureId}: Esperado {expectedProb:P2}, Observado {observedProb:P2}. " +
                    $"Intervalo esperado [{lowerBound:P2}, {upperBound:P2}]");
            } else {
                // Para probabilidades esperadas de 0, aceptamos un margen muy pequeño
                Assert.True(observedProb <= 0.005, // 0.5% máximo
                    $"Falla {failureId}: Esperado 0%, Observado {observedProb:P2}");
            }
        }

        // Verificación adicional: la suma total debe ser ~100%
        double totalObserved = counts.Values.Sum() / (double)iterations;
        Assert.True(Math.Abs(totalObserved - 1.0) < 0.001,
            $"La suma de todas las probabilidades observadas debe ser ~100%. Fue {totalObserved:P2}");
    }
}