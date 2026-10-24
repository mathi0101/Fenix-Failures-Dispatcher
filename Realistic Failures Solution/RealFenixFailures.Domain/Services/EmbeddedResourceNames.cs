namespace RealFenixFailures.Domain.Services;

/// <summary>
/// Nombres de los catálogos embebidos en el ensamblado (failures.json y los
/// presets de Training/Realistic). Son parte del producto como EmbeddedResource
/// versionadas y sus nombres no son configurables: por eso viven como constantes
/// en el código y no en appsettings.
/// </summary>
public static class EmbeddedResourceNames {
    public const string FailuresCatalog = "failures.json";
    public const string TrainingPresets = "training_presets.json";
    public const string RealisticPresets = "realistic_presets.json";
}
