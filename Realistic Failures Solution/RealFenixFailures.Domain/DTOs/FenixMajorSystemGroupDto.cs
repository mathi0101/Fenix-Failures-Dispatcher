namespace RealFenixFailures.Domain.DTOs;

public sealed record FenixMajorSystemGroupDto(
    int Id,
    string Title,
    string ShortTitle,
    IReadOnlyList<FenixSystemGroupDto> SystemGroups
);