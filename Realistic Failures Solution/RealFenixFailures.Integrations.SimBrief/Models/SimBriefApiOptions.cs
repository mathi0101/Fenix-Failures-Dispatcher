namespace RealFenixFailures.Integrations.SimBrief.Models;

public class SimBriefApiOptions {
    public const string SectionName = "SimBriefApi";

    public string BaseUrl { get; set; } = "https://www.simbrief.com/api/xml.fetcher.php";
    public int DownloadTimeoutSeconds { get; set; } = 15;
}
