using System.IO;
using System.Text.Json;

namespace RealFenixFailures.UI.Localization;

/// <summary>
/// Arranque temprano del idioma (issue #12): corre en Program.Main antes del
/// splash y de crear la Application, para que TODA la capa visible —incluidos
/// los diálogos de crash pre-DI— salga en el idioma correcto. Lee la preferencia
/// directamente de userprefs.json (la DI todavía no existe); la resolución es la
/// misma que luego aplica LocalizationService vía UserPreferencesService, por lo
/// que no hay doble fuente de verdad en memoria.
/// </summary>
public static class LanguageBootstrapper {

    /// <summary>Resuelve y aplica el idioma del arranque; devuelve lo resuelto.</summary>
    public static AppLanguage Initialize() {
        var code = LanguageResolver.Resolve(TryReadStoredLanguage());
        var language = LanguageResolver.ToAppLanguage(code);
        Loc.ApplyCulture(language);
        Loc.Instance.CurrentLanguage = language;
        return language;
    }

    private static string? TryReadStoredLanguage() {
        try {
            var filePath = Path.Combine(RealFenixFailures.Infrastructure.AppPaths.AppDataDirectory, "userprefs.json");
            if (!File.Exists(filePath))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Language", out var property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
        }
        catch {
            // Archivo ausente, corrupto o carpeta no accesible: se detecta del sistema.
            return null;
        }
    }
}
