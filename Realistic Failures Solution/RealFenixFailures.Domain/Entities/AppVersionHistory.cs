namespace RealFenixFailures.Domain.Entities;

public class AppVersionHistory {
    public int Id { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
}
