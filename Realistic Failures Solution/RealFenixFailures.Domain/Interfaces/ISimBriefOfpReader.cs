using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces;

/// <summary>
/// Parsea el OFP (XML o JSON) descargado de SimBrief a una entidad <see cref="FlightPlan"/>.
/// No valida: solo mapea el XML/JSON a su representación de dominio.
/// Implementación: <see cref="RealFenixFailures.Integrations.SimBrief.OfpReaders.SimbriefOfpReader"/>.
/// </summary>
public interface ISimBriefOfpReader {
    Task<FlightPlan> ReadAsync(Stream ofpStream, CancellationToken ct);
}
