namespace RealFenixFailures.Domain.Services;

public class FailureEngineSettings {
    public const string SectionName = "FailureEngine";

    public int CheckConnectionIntervalSeconds { get; set; }
    public int CheckIntervalSeconds { get; set; }

    public string FailuresJson { get; set; } = string.Empty;
    public string TrainingPresetsJson { get; set; } = string.Empty;
    public string RealisticPresetsJson { get; set; } = string.Empty;
}
