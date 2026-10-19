using RealFenixFailures.Application.DTOs;

namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Consulta la última versión publicada de la app (releases de GitHub) para el
/// chequeo manual de actualizaciones desde la UI.
/// </summary>
public interface IUpdateChecker {
    Task<UpdateCheckResultDto> CheckForUpdatesAsync(CancellationToken ct);
}
