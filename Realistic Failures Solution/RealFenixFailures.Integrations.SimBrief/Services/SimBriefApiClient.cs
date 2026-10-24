using Microsoft.Extensions.Options;
using RealFenixFailures.Integrations.SimBrief.Interfaces;
using RealFenixFailures.Integrations.SimBrief.Models;
using System.Text;

namespace RealFenixFailures.Integrations.SimBrief.Services;

/// <summary>
/// Descarga el OFP de SimBrief por userid. Base: https://www.simbrief.com/api/xml.fetcher.php?userid=X
/// El XML llega como response del endpoint; se expone como Stream para que el parser lo consuma.
/// </summary>
public sealed class SimBriefApiClient : ISimBriefApiClient {
    private readonly HttpClient _http;
    private readonly SimBriefApiOptions _opts;

    public SimBriefApiClient(HttpClient http, IOptions<SimBriefApiOptions> opts) {
        _http = http;
        _opts = opts.Value;
        _http.Timeout = TimeSpan.FromSeconds(_opts.DownloadTimeoutSeconds);
    }

    public async Task<Stream> FetchOfpXmlAsync(string simbriefUserId, CancellationToken ct) {
        var requestUri = $"{_opts.BaseUrl}?username={Uri.EscapeDataString(simbriefUserId)}";
        var response = await _http.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();
        // El OFP es texto (XML/JSON); lo devolvemos como un MemoryStream memoria-evaluable más de una vez.
        var content = await response.Content.ReadAsStringAsync(ct);
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
