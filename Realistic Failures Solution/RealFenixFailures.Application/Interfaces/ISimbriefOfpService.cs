using RealFenixFailures.Application.DTOs;

namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Orquesta el flujo de OFP del modo Realistic: descargar → validar → persistir y devolver estado.
/// El ID de SimBrief del usuario vive en userprefs (privacidad), no en BD.
/// </summary>
public interface ISimbriefOfpService {
    Task<FlightPlanDto> DownloadAsync(string aircraftIcaoTypeCode, string simbriefUserId, CancellationToken ct);
}
