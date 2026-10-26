using System.Text.Json;

namespace RealFenixFailures.Domain.Services;

/// <summary>
/// Conteo de severidades del catálogo curado embebido (<see cref="EmbeddedResourceNames.FailuresCatalog"/>).
/// Es el universo real de la semilla de tierra y alimenta la estimación de presentación de la pantalla
/// de perfiles; el catálogo es inmutable durante la ejecución, así que el conteo se calcula una sola vez.
/// </summary>
public static class FailureCatalogStats {
    private static readonly Lazy<(int Minor, int Major, int Critical)> Counts = new(Load);

    public static (int Minor, int Major, int Critical) SeverityCounts() => Counts.Value;

    private static (int Minor, int Major, int Critical) Load() {
        try {
            using var stream = EmbeddedJsonLoader.LoadFromEmbeddedJson(EmbeddedResourceNames.FailuresCatalog)
                ?? throw new FileNotFoundException("Recurso embebido failures.json no encontrado");
            var catalog = JsonSerializer.Deserialize<CatalogDto>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("failures.json deserializó a null");
            var minor = 0;
            var major = 0;
            var critical = 0;
            foreach (var ata in catalog.Atas)
                foreach (var group in ata.Groups)
                    foreach (var failure in group.Failures) {
                        switch (failure.Severity) {
                            case 1: minor++; break;
                            case 2: major++; break;
                            case 3: critical++; break;
                        }
                    }
            return (minor, major, critical);
        } catch (Exception ex) {
            throw new InvalidOperationException("No se pudo contar el catálogo embebido failures.json", ex);
        }
    }

    private sealed record CatalogDto(List<AtaDto> Atas);
    private sealed record AtaDto(List<GroupDto> Groups);
    private sealed record GroupDto(List<FailureDto> Failures);
    private sealed record FailureDto(int Severity);
}
