using RealFenixFailures.UI.Localization;

namespace RealFenixFailures.UI.ViewModels.Base;

/// <summary>
/// Base para viewmodels con textos localizados (issue #12): se suscribe al cambio
/// de idioma de <see cref="Loc"/> y la subclase re-notifica sus propiedades
/// traducidas en <see cref="OnLanguageChanged"/> (las propiedades calculadas que
/// llaman a Loc.T se re-evalúan con el idioma nuevo; las cacheadas se recalculan ahí).
/// </summary>
public abstract class LocalizedViewModelBase : ObservableObject {
    protected LocalizedViewModelBase() {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Re-notifica las propiedades traducidas del VM (se llama al cambiar el idioma).</summary>
    protected virtual void OnLanguageChanged() { }
}
