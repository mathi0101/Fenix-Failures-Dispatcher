using System.Globalization;

namespace RealFenixFailures.UI.Localization;

/// <summary>Idiomas de interfaz soportados (issue #12). Los códigos de dos letras son la
/// representación canónica persistida en userprefs.json (propiedad Language).</summary>
public enum AppLanguage {
    English,
    Spanish,
}

/// <summary>Opción del selector de idioma de Ajustes. Los nombres se muestran en su
/// propio idioma (endónimos), así que no requieren traducción.</summary>
public sealed record LanguageOption(string DisplayName, AppLanguage Language) {
    public static readonly IReadOnlyList<LanguageOption> All = new LanguageOption[] {
        new("English", AppLanguage.English),
        new("Español", AppLanguage.Spanish),
    };

    public override string ToString() {
        return DisplayName;
    }
}

/// <summary>Constantes de códigos de idioma (persistidos y comparables).</summary>
public static class LanguageCodes {
    public const string English = "en";
    public const string Spanish = "es";
}

/// <summary>
/// Lógica pura de resolución de idioma del primer arranque (issue #12):
/// si hay preferencia guardada manda la preferencia; si no, se detecta del
/// idioma de Windows — español → ES, cualquier otro caso → EN por defecto.
/// Es estática y pura para poder testearla sin WPF ni archivos.
/// </summary>
public static class LanguageResolver {

    /// <summary>
    /// Normaliza la preferencia persistida: devuelve "es"/"en" si es válida,
    /// o null si está ausente o corrupta (→ corresponde detectar).
    /// </summary>
    public static string? FromStored(string? stored) {
        var normalized = stored?.Trim().ToLowerInvariant();
        return normalized switch {
            LanguageCodes.Spanish => LanguageCodes.Spanish,
            LanguageCodes.English => LanguageCodes.English,
            _ => null,
        };
    }

    /// <summary>
    /// Detección por entorno: el idioma de visualización de Windows (CurrentUICulture)
    /// manda; español → ES y cualquier otro idioma → EN (default de la issue).
    /// </summary>
    public static string Detect(CultureInfo? uiCulture = null) {
        var code = (uiCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return code.Equals(LanguageCodes.Spanish, StringComparison.OrdinalIgnoreCase)
            ? LanguageCodes.Spanish
            : LanguageCodes.English;
    }

    /// <summary>Resolución completa: preferencia válida → esa; si no → detección.</summary>
    public static string Resolve(string? stored, CultureInfo? uiCulture = null)
        => FromStored(stored) ?? Detect(uiCulture);

    /// <summary>Mapea un código canónico al enum de la app (inválido → inglés).</summary>
    public static AppLanguage ToAppLanguage(string code)
        => code == LanguageCodes.Spanish ? AppLanguage.Spanish : AppLanguage.English;

    /// <summary>Mapea el enum al código canónico para persistir.</summary>
    public static string ToCode(AppLanguage language)
        => language == AppLanguage.Spanish ? LanguageCodes.Spanish : LanguageCodes.English;
}
