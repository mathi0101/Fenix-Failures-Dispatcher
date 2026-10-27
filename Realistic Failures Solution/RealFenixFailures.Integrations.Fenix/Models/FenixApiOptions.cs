namespace RealFenixFailures.Integrations.Fenix.Models;

/// <summary>Configuración de la API HTTP local de Fenix (sección <c>FenixApi</c>): dónde vive el addon y qué endpoints usa la app.</summary>
public class FenixApiOptions {
    public const string SectionName = "FenixApi";

    /// <summary>Base de la API de Fenix, sin puerto (corre en la misma máquina que la app).</summary>
    public string BaseUrl { get; set; } = "http://localhost";
    /// <summary>Puerto de la API de Fenix; debe coincidir con el configurado en el addon (default 8083).</summary>
    public int Port { get; set; } = 8083;
    /// <summary>Endpoint GET de fallas manuales: lista las ya activas en Fenix y hace de ping de vida de la API.</summary>
    public string ManualFailuresPath { get; set; } = "/fenix/failures/manual";
    /// <summary>Endpoint POST por el que la app dispara la falla manual en el avión.</summary>
    public string SaveManualPath { get; set; } = "/fenix/failures/saveManual";
    /// <summary>Intervalo, s, del health check contra la API (nunca menor a 1).</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    /// <summary>Timeout, s, de cada request del health check; agotado = API caída.</summary>
    public int HealthCheckTimeout { get; set; } = 2;
}
