using System.Globalization;
using System.Resources;
using RealFenixFailures.UI.Localization;

namespace RealFenixFailures.TestUnits.Localization;

/// <summary>
/// Tests de la resolución de idioma del primer arranque (issue #12):
/// preferencia guardada manda; sin preferencia, español → ES y cualquier
/// otro caso (inglés, francés, invariant) → EN por defecto.
/// </summary>
public class LanguageResolverTests {

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("ES", "es")]
    [InlineData(" en ", "en")]
    public void FromStored_ValorValido_Normaliza(string stored, string expected) {
        Assert.Equal(expected, LanguageResolver.FromStored(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("es-AR")] // solo códigos canónicos de dos letras
    [InlineData("garbage")]
    public void FromStored_ValorAusenteOCorrupto_DevuelveNull(string? stored) {
        Assert.Null(LanguageResolver.FromStored(stored));
    }

    [Theory]
    [InlineData("es-ES", "es")]
    [InlineData("es-AR", "es")]
    [InlineData("es", "es")]
    [InlineData("en-US", "en")]
    [InlineData("en", "en")]
    [InlineData("fr-FR", "en")]  // cualquier otro idioma → inglés
    [InlineData("pt-BR", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("", "en")]       // cultura invariante → inglés
    public void Detect_SegunCulturaUI_EspanolOSiempreIngles(string cultureName, string expected) {
        var culture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expected, LanguageResolver.Detect(culture));
    }

    [Fact]
    public void Resolve_ConPreferenciaValida_LaPreferenciaMandaSobreLaDeteccion() {
        Assert.Equal("en", LanguageResolver.Resolve("en", CultureInfo.GetCultureInfo("es-AR")));
        Assert.Equal("es", LanguageResolver.Resolve("es", CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void Resolve_SinPreferencia_DetectaDelSistema() {
        Assert.Equal("es", LanguageResolver.Resolve(null, CultureInfo.GetCultureInfo("es-UY")));
        Assert.Equal("en", LanguageResolver.Resolve(null, CultureInfo.GetCultureInfo("ja-JP")));
    }

    [Theory]
    [InlineData("es", AppLanguage.Spanish)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("xx", AppLanguage.English)] // inválido → inglés
    public void ToAppLanguage_MapeaCodigoACanonical(string code, AppLanguage expected) {
        Assert.Equal(expected, LanguageResolver.ToAppLanguage(code));
    }

    [Theory]
    [InlineData(AppLanguage.Spanish, "es")]
    [InlineData(AppLanguage.English, "en")]
    public void ToCode_MapeaCanonicalACodigo(AppLanguage language, string expected) {
        Assert.Equal(expected, LanguageResolver.ToCode(language));
    }
}

/// <summary>
/// Tests del administrador de textos (issue #12): traducción por cultura,
/// fallback a inglés y notificación de refresco en vivo de la UI.
/// </summary>
public class LocTests : IDisposable {
    private readonly AppLanguage _originalLanguage = Loc.Instance.CurrentLanguage;

    public void Dispose() {
        // Los tests mutan el singleton global; se restaura el idioma original.
        Loc.Instance.CurrentLanguage = _originalLanguage;
    }

    [Fact]
    public void T_ClaveConocida_DevuelveTextoPorIdioma() {
        Loc.Instance.CurrentLanguage = AppLanguage.English;
        var english = Loc.T("Settings_General_LanguageLabel");
        Assert.Equal("LANGUAGE", english);

        Loc.Instance.CurrentLanguage = AppLanguage.Spanish;
        Assert.Equal("IDIOMA", Loc.T("Settings_General_LanguageLabel"));
    }

    [Fact]
    public void T_ConArgs_FormateaConCulturaDelIdioma() {
        Loc.Instance.CurrentLanguage = AppLanguage.English;
        Assert.Equal("Value: 42.", Loc.T("Settings_Test_Format", 42));
    }

    [Fact]
    public void Indexer_EquivaleAT() {
        Loc.Instance.CurrentLanguage = AppLanguage.English;
        Assert.Equal(Loc.T("Settings_General_LanguageLabel"), Loc.Instance["Settings_General_LanguageLabel"]);
    }

    [Fact]
    public void T_ClaveInexistente_DevuelveLaClave() {
        Assert.Equal("Key_Que_No_Existe", Loc.T("Key_Que_No_Existe"));
    }

    [Fact]
    public void CurrentLanguage_Cambio_DisparaIndexerPropertyChanged() {
        Loc.Instance.CurrentLanguage = AppLanguage.English;

        object? notifiedProperty = null;
        Loc.Instance.PropertyChanged += (_, e) => notifiedProperty = e.PropertyName;
        Loc.Instance.CurrentLanguage = AppLanguage.Spanish;

        Assert.Equal("Item[]", notifiedProperty); // refresca TODOS los bindings {loc:Loc}
    }

    [Fact]
    public void CurrentLanguage_Cambio_DisparaLanguageChanged() {
        Loc.Instance.CurrentLanguage = AppLanguage.English;

        var fired = false;
        Loc.Instance.LanguageChanged += () => fired = true;
        Loc.Instance.CurrentLanguage = AppLanguage.Spanish;
        Loc.Instance.LanguageChanged -= () => fired = true;

        Assert.True(fired);
    }

    [Fact]
    public void CurrentLanguage_MismoValor_NoDisparaEventos() {
        Loc.Instance.CurrentLanguage = AppLanguage.Spanish;

        var propertyChanged = false;
        Loc.Instance.PropertyChanged += (_, _) => propertyChanged = true;
        Loc.Instance.CurrentLanguage = AppLanguage.Spanish;

        Assert.False(propertyChanged);
    }
}

/// <summary>
/// Paridad de claves entre los diccionarios de idioma (issue #12): el español
/// debe tener exactamente las mismas claves que el inglés, y valores no vacíos.
/// </summary>
public class ResxParityTests {

    private static Dictionary<string, string> ReadResources(string resourceName, string? satelliteCulture = null) {
        var assembly = typeof(Loc).Assembly;
        if (satelliteCulture is not null) {
            // El resx de la cultura NO neutra compila a un ensamblado satélite
            // (es/RealFenixFailures.resources.dll), no al ensamblado principal.
            var satellitePath = Path.Combine(AppContext.BaseDirectory, satelliteCulture, "RealFenixFailures.resources.dll");
            Assert.True(File.Exists(satellitePath), $"No existe el satélite {satelliteCulture} en {AppContext.BaseDirectory}");
            assembly = System.Reflection.Assembly.LoadFrom(satellitePath);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido {resourceName} en {assembly.GetName().Name}");
        using var reader = new System.Resources.ResourceReader(stream);
        var result = new Dictionary<string, string>();
        foreach (var entry in reader.OfType<System.Collections.DictionaryEntry>()) {
            var key = Assert.IsType<string>(entry.Key);
            var value = Assert.IsType<string>(entry.Value);
            result[key] = value;
        }
        return result;
    }

    [Fact]
    public void Espanol_EIngles_TienenLasMismasClaves() {
        var english = ReadResources("RealFenixFailures.UI.Localization.Strings.resources");
        var spanish = ReadResources("RealFenixFailures.UI.Localization.Strings.es.resources", satelliteCulture: "es");

        var missingInSpanish = english.Keys.Except(spanish.Keys).ToList();
        var missingInEnglish = spanish.Keys.Except(english.Keys).ToList();

        Assert.True(missingInSpanish.Count == 0,
            "Claves sin traducción al español: " + string.Join(", ", missingInSpanish));
        Assert.True(missingInEnglish.Count == 0,
            "Claves sin equivalente en inglés: " + string.Join(", ", missingInEnglish));
    }

    [Fact]
    public void TodosLosValores_NoEstanVacios() {
        var english = ReadResources("RealFenixFailures.UI.Localization.Strings.resources");
        var spanish = ReadResources("RealFenixFailures.UI.Localization.Strings.es.resources", satelliteCulture: "es");

        foreach (var (key, value) in english)
            Assert.False(string.IsNullOrWhiteSpace(value), $"Valor vacío en inglés → {key}");
        foreach (var (key, value) in spanish)
            Assert.False(string.IsNullOrWhiteSpace(value), $"Valor vacío en español → {key}");
    }
}
