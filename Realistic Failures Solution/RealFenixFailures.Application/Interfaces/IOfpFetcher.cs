namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Descarga el OFP de SimBrief y expone el contenido (XML o JSON) listo para parsear.
/// Implementado en la capa de integraciones (cliente HTTP de SimBrief).
/// El "vernáculo" va acá y no en Integrations para que la lógica de dominio no dependa de HTTP.
/// </summary>
public interface IOfpFetcher {
    Task<Stream> FetchAsync(string simbriefUserId, CancellationToken ct);
}
