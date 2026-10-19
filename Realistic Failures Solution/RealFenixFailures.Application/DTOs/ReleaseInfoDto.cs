namespace RealFenixFailures.Application.DTOs;

/// <summary>Información de una release publicada (p. ej. en GitHub Releases).</summary>
public sealed class ReleaseInfoDto {
    public required string TagName { get; init; }
    public required string HtmlUrl { get; init; }
    public bool IsPrerelease { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public string? Name { get; init; }
    public string? Body { get; init; }
}
