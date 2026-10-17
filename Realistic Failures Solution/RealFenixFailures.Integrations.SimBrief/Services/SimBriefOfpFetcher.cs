using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Integrations.SimBrief.Interfaces;

namespace RealFenixFailures.Integrations.SimBrief.Services;

/// <summary>
/// Adapta el cliente HTTP de SimBrief al contrato de la capa de aplicación
/// (<see cref="IOfpFetcher"/>). Descarga el XML del OFP y lo expone como Stream.
/// </summary>
public sealed class SimBriefOfpFetcher : IOfpFetcher {
    private readonly ISimBriefApiClient _api;

    public SimBriefOfpFetcher(ISimBriefApiClient api) {
        _api = api;
    }

    public Task<Stream> FetchAsync(string simbriefUserId, CancellationToken ct)
        => _api.FetchOfpXmlAsync(simbriefUserId, ct);
}
