using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace RealFenixFailures.UI.Localization;

/// <summary>
/// Origen único de los textos visibles de la app (issue #12). Las claves viven en
/// Localization/Strings.resx (inglés, cultura neutra / fallback) y Strings.es.resx
/// (español, voseo). 
/// 
/// Plan B del plan de implementación: el spike con WPFLocalizeExtension 3.10.0
/// mostró incompatibilidad con el compilador XAML de .NET 10 (MC3074, reproducida
/// en proyecto mínimo aislado), así que se usa el patrón clásico de WPF para
/// cambio de idioma en vivo: singleton con indexador; al cambiar el idioma dispara
/// PropertyChanged con "Item[]" y TODOS los bindings {loc:Loc ...} refrescan sin
/// reiniciar. Los resx son idénticos a los del plan A.
/// 
/// Uso en XAML:  Text="{loc:Loc Settings_General_LanguageLabel}"
/// Uso en C#:    Loc.T("Key", args...)  o  Loc.Instance["Key"]
/// </summary>
public sealed class Loc : INotifyPropertyChanged {

    public static Loc Instance { get; } = new();

    private static readonly ResourceManager Resources = new(
        typeof(Loc).Namespace + ".Strings", typeof(Loc).Assembly);

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo(LanguageCodes.English);
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo(LanguageCodes.Spanish);

    /// <summary>Nombre de propiedad que WPF interpreta como "el indexador cambió entero".</summary>
    private const string IndexerPropertyName = "Item[]";

    private AppLanguage _currentLanguage;

    private Loc() { }

    /// <summary>Disparado además del PropertyChanged indexador, para VMs que recalculan textos compuestos.</summary>
    public event Action? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Idioma activo de la interfaz. Al asignarlo se aplican las culturas del
    /// hilo y se refrescan en vivo todos los bindings localizados.</summary>
    public AppLanguage CurrentLanguage {
        get => _currentLanguage;
        set {
            if (_currentLanguage == value)
                return;
            ApplyCulture(value);
            _currentLanguage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerPropertyName));
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>Indexador para binding: {Binding [Clave], Source={x:Static loc:Loc.Instance}}.</summary>
    public string this[string key] => T(key);

    /// <summary>
    /// Traduce la clave al idioma activo. Si falta en el satélite cae al recurso
    /// neutro (inglés); si tampoco existe, devuelve la clave y deja warning en el log.
    /// Con args aplica string.Format con la cultura del idioma activo.
    /// </summary>
    public static string T(string key, params object?[] args) {
        var culture = Instance._currentLanguage == AppLanguage.Spanish ? SpanishCulture : EnglishCulture;
        var value = Resources.GetString(key, culture);
        if (value is null)
            return key;
        return args.Length > 0 ? string.Format(culture, value, args) : value;
    }

    /// <summary>
    /// Aplica el idioma a las culturas del hilo (UI + formato). Static para que el
    /// bootstrap temprano (LanguageBootstrapper, antes del splash) use el mismo camino.
    /// </summary>
    public static void ApplyCulture(AppLanguage language) {
        var culture = CultureInfo.GetCultureInfo(LanguageResolver.ToCode(language));
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}

/// <summary>
/// Markup extension del Plan B: Text="{loc:Loc Clave}" equivale a un OneWay binding
/// al indexador de Loc.Instance; refresca en vivo con el PropertyChanged("Item[]").
/// </summary>
public sealed class LocExtension : MarkupExtension {
    private readonly string _key;

    public LocExtension(string key) {
        _key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) {
        var binding = new Binding("[" + _key + "]") {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
