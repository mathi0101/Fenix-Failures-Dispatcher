namespace RealFenixFailures.Application.DTOs;

/// <summary>
/// Resultado del chequeo de actualizaciones. <see cref="LatestRelease"/> es la
/// release más reciente publicada (incluyendo pre-releases: al compartir builds
/// de prueba, la última publicación suele ser una pre-release);
/// <see cref="LatestStableRelease"/> es la más reciente sin pre-release.
/// </summary>
public sealed class UpdateCheckResultDto {
    public ReleaseInfoDto? LatestRelease { get; init; }
    public ReleaseInfoDto? LatestStableRelease { get; init; }
    public string? ErrorMessage { get; init; }

    public bool HasReleases => LatestRelease is not null;
    public bool IsError => ErrorMessage is not null;
}
