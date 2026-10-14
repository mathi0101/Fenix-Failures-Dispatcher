using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealFenixFailures.Integrations.Fenix.Interfaces;
using RealFenixFailures.Integrations.Fenix.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealFenixFailures.Integrations.Fenix.Services;

public class FenixApiClient : IFenixApiClient {
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<FenixApiOptions> _optionsMonitor;
    private readonly ILogger<FenixApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FenixApiClient(HttpClient httpClient, IOptionsMonitor<FenixApiOptions> optionsMonitor, ILogger<FenixApiClient> logger) {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }
    public async Task<bool> IsApiAlive(CancellationToken ct) {
        try {
            var endpoint = BuildEndpointUri(_optionsMonitor.CurrentValue.ManualFailuresPath);
            var response = await _httpClient.GetAsync(endpoint, ct);

            return response.IsSuccessStatusCode;
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "Unable to connect to Fenix API GET manual failures endpoint.");
            return false;
        } catch (TaskCanceledException) {
            _logger.LogDebug("Timeout while requesting if Fenix API is alive");
            return false;
        }
    }

    public async Task<Stream?> GetManualFailuresAsync(CancellationToken ct) {
        try {
            var endpoint = BuildEndpointUri(_optionsMonitor.CurrentValue.ManualFailuresPath);
            var response = await _httpClient.GetAsync(endpoint, ct);

            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("Fenix GET manual failures returned status code {StatusCode}", response.StatusCode);
                return null;
            }
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return stream;
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "Unable to connect to Fenix API GET manual failures endpoint.");
            return null;
        } catch (TaskCanceledException) {
            _logger.LogDebug("Timeout while requesting Fenix API GET manual failures endpoint.");
            return null;
        }
    }

    public async Task<Stream?> SendFailureAsync(FenixSaveManualRequest rq, CancellationToken ct) {
        try {
            var endpoint = BuildEndpointUri(_optionsMonitor.CurrentValue.SaveManualPath);
            var response = await _httpClient.PostAsJsonAsync(endpoint, rq, JsonOptions, ct);

            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("Fenix POST saveManual failed for {FailureId}. Status code {StatusCode}", rq.Id, response.StatusCode);
                return null;
            }
            return await response.Content.ReadAsStreamAsync(ct);
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "Unable to connect to Fenix API POST saveManual endpoint for {FailureId}", rq.Id);
            return null;
        } catch (TaskCanceledException ex) {
            _logger.LogWarning(ex, "Timeout while calling Fenix API POST saveManual endpoint for {FailureId}", rq.Id);
            return null;
        }
    }

    private Uri BuildEndpointUri(string relativePath) {
        var options = _optionsMonitor.CurrentValue;
        var normalizedPath = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        var baseUri = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/", UriKind.Absolute);
        var builder = new UriBuilder(baseUri) {
            Port = options.Port,
            Path = normalizedPath
        };

        return builder.Uri;
    }


}
