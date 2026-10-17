namespace RealFenixFailures.Domain.Services;

public class FailureEngineSettings {
    public const string SectionName = "FailureEngine";

    public int CheckConnectionIntervalSeconds { get; set; }
    public int CheckIntervalSeconds { get; set; }

    public string FailuresJson { get; set; } = string.Empty;
    public string TrainingPresetsJson { get; set; } = string.Empty;
    public string RealisticPresetsJson { get; set; } = string.Empty;

    public RealisticModeSettings RealisticMode { get; set; } = new();
}

public class RealisticModeSettings {
    public int EvaluationIntervalSeconds { get; set; } = 30;
    public int PreparedQueueLimit { get; set; } = 5;
    public bool GroundFailureEnabled { get; set; } = true;
    public int ThinkingLinesCap { get; set; } = 50;
    public OfpWindowSettings Ofp { get; set; } = new();
    public WearSettings Wear { get; set; } = new();
}

public class OfpWindowSettings {
    public int PreStartWindowHours { get; set; } = 2;
    public int PostArrivalWindowHours { get; set; } = 2;
}

public class WearSettings {
    public double AmplificationFactor { get; set; } = 0.5;
    public int ThresholdNoticePct { get; set; } = 80;
}
