namespace RealFenixFailures.Integrations.SimBrief.Models;

/// <summary>Configuración del cliente SimBrief (sección <c>SimBriefApi</c>): de dónde se descarga el OFP.</summary>
public class SimBriefApiOptions {
    public const string SectionName = "SimBriefApi";

    /// <summary>URL del endpoint público de SimBrief que devuelve el OFP en XML.</summary>
    public string BaseUrl { get; set; } = "https://www.simbrief.com/api/xml.fetcher.php";
    /// <summary>Timeout, s, de la descarga del OFP.</summary>
    public int DownloadTimeoutSeconds { get; set; } = 15;
}
