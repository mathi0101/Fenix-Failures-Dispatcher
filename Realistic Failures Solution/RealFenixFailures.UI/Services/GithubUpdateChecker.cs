using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;

namespace RealFenixFailures.UI.Services;

/// <summary>
/// Consulta las releases de GitHub mediante la API pública (sin token, sin coste).
/// Incluye pre-releases: mientras la app se comparte como builds de prueba, la
/// publicación más reciente suele ser una pre-release y debe ofrecerse igual.
/// </summary>
public sealed class GithubUpdateChecker : IUpdateChecker {

    /// <summary>Repo de GitHub de la app (dueño/nombre), fuente de las publicaciones.</summary>
    public const string RepositoryName = "mathi0101/Fenix-Failures-Dispatcher";

    private const int ReleasesPerPage = 5;

    private readonly ILogger<GithubUpdateChecker> _logger;
    private readonly HttpClient _http;

    public GithubUpdateChecker(ILogger<GithubUpdateChecker> logger) {
        _logger = logger;
        _http = new HttpClient {
            BaseAddress = new Uri($"https://api.github.com/repos/{RepositoryName}/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        // La API de GitHub exige un User-Agent propio.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("RealFenixFailures-update-check/1.0");
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResultDto> CheckForUpdatesAsync(CancellationToken ct) {
        try {
            using var response = await _http.GetAsync($"releases?per_page={ReleasesPerPage}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResultDto(); // el repo aún no tiene releases publicados

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<GithubReleaseItem>>(json);

            var releases = (items ?? [])
                .Where(i => !string.IsNullOrWhiteSpace(i.TagName) && !i.IsDraft)
                .Select(i => new ReleaseInfoDto {
                    TagName = i.TagName!,
                    HtmlUrl = i.HtmlUrl ?? string.Empty,
                    IsPrerelease = i.IsPrerelease,
                    PublishedAt = i.PublishedAt,
                    Name = i.Name,
                    Body = i.Body
                })
                .ToList();

            return new UpdateCheckResultDto {
                LatestRelease = releases.FirstOrDefault(),
                LatestStableRelease = releases.FirstOrDefault(r => !r.IsPrerelease)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "No se pudo consultar GitHub Releases para el chequeo de actualizaciones.");
            return new UpdateCheckResultDto {
                ErrorMessage = "No se pudo contactar a GitHub. Comprobá tu conexión e intentá de nuevo."
            };
        }
    }

    /// <summary>Forma del JSON de la API pública: GET /repos/{repo}/releases.</summary>
    private sealed record GithubReleaseItem {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("prerelease")] public bool IsPrerelease { get; init; }
        [JsonPropertyName("draft")] public bool IsDraft { get; init; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("body")] public string? Body { get; init; }
    }
}
