using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.Localization;
using RealFenixFailures.UI.ViewModels.Base;
using System.Globalization;
using System.Windows.Media;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Fragmento de chip con su parte destacada en negrita y el resto del texto.</summary>
public sealed record RiskProfileChip(string Lead, string Rest);

/// <summary>Segmento de la barra de intensidad; el pincel ya viene resuelto (encendido o apagado).</summary>
public sealed record IntensitySegment(Brush Fill);

/// <summary>
/// Tarjeta de un perfil de riesgo de la ventana de selección: textos fijos de producto + valores
/// dinámicos (medidores, chips y estimación) recalculados desde la configuración vigente del motor.
/// Los textos llegan resueltos por <see cref="CreateDefaults"/>; al cambiar el idioma la ventana
/// reconstruye las cuatro tarjetas (ver RiskProfileWindowViewModel), así que no se re-notifica acá.
/// </summary>
public sealed class RiskProfileOptionViewModel : ObservableObject {
    private string _estimateDisplay = Loc.T("Rp_EstimatePlaceholder");
    private string _frequencyLabel = "×...";
    private double _frequencyBar;
    private string _wearLabel = Loc.T("Rp_WearPlaceholder");
    private double _wearBar;
    private IReadOnlyList<RiskProfileChip> _chips = [];
    private bool _isSelected;
    private Brush _cardBorder = FrozenBrush("#232A38");

    public RiskProfileOptionViewModel(RiskLevel risk, int level, string name, string description,
        string accentHex, string vatsimPill, string vatsimAdviceLead, string vatsimAdviceRest,
        (byte R, byte G, byte B) pillTint, string pillForegroundHex) {
        Risk = risk;
        Level = level;
        Name = name;
        Description = description;
        Accent = FrozenBrush(accentHex);
        VatsimPill = vatsimPill;
        VatsimAdviceLead = vatsimAdviceLead;
        VatsimAdviceRest = vatsimAdviceRest;
        VatsimPillBackground = FrozenBrushAlpha(pillTint.R, pillTint.G, pillTint.B, 0x1F);
        VatsimPillForeground = FrozenBrush(pillForegroundHex);
        var gray = FrozenBrush("#232B3B");
        Intensity = Enumerable.Range(1, 4)
            .Select(i => new IntensitySegment(i <= level ? Accent : gray))
            .ToList();
    }

    public RiskLevel Risk { get; }
    public int Level { get; }
    public string Name { get; }
    public string Description { get; }
    public Brush Accent { get; }
    public string VatsimPill { get; }
    public Brush VatsimPillBackground { get; }
    public Brush VatsimPillForeground { get; }
    public string VatsimAdviceLead { get; }
    public string VatsimAdviceRest { get; }
    public IReadOnlyList<IntensitySegment> Intensity { get; }

    public string EstimateDisplay { get => _estimateDisplay; private set => SetProperty(ref _estimateDisplay, value); }
    public string FrequencyLabel { get => _frequencyLabel; private set => SetProperty(ref _frequencyLabel, value); }
    public double FrequencyBar { get => _frequencyBar; private set => SetProperty(ref _frequencyBar, value); }
    public string WearLabel { get => _wearLabel; private set => SetProperty(ref _wearLabel, value); }
    public double WearBar { get => _wearBar; private set => SetProperty(ref _wearBar, value); }
    public IReadOnlyList<RiskProfileChip> Chips { get => _chips; private set => SetProperty(ref _chips, value); }

    public bool IsSelected {
        get => _isSelected;
        set {
            if (!SetProperty(ref _isSelected, value)) return;
            CardBorder = value ? Accent : FrozenBrush("#232A38");
            if (value) SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Borde de la tarjeta: color del perfil cuando está seleccionada, gris en reposo.</summary>
    public Brush CardBorder {
        get => _cardBorder;
        private set => SetProperty(ref _cardBorder, value);
    }

    /// <summary>Se dispara cuando la tarjeta pasa a seleccionada; la ventana la usa para sincronizar el estado.</summary>
    public event EventHandler? SelectedChanged;

    /// <summary>Aplica los valores resueltos del servicio de expectativa y normaliza las barras al máximo del conjunto.</summary>
    public void ApplyExpectation(ProfileExpectationDto dto, double maxMultiplier, double maxWearMultiplier) {
        var profile = dto.Profile;
        var multiplier = Math.Max(0, profile.ProbabilityMultiplier);
        var wearMax = Math.Max(1, profile.WearMaxMultiplier);

        FrequencyLabel = FormatMultiplier(multiplier);
        FrequencyBar = maxMultiplier > 0 ? multiplier / maxMultiplier * 100 : 0;
        WearLabel = Loc.T("Rp_WearUpTo", $"{wearMax:0.##}");
        WearBar = maxWearMultiplier > 1 ? (wearMax - 1) / (maxWearMultiplier - 1) * 100 : 100;
        // Regla de producto: Bajo solo menciona graves (el motor le descarta las críticas); el resto,
        // graves y críticas, ambas por cada 10 horas de vuelo.
        var majorTen = FormatRate(dto.MajorPerHour * 10);
        EstimateDisplay = Risk == RiskLevel.Low
            ? Loc.T("Rp_EstimateMajorOnly", majorTen)
            : Loc.T("Rp_EstimateMajorCritical", majorTen, FormatRate(dto.CriticalPerHour * 10));

        var chips = new List<RiskProfileChip>();
        if (profile.GroundFailuresCap > 0)
            chips.Add(new RiskProfileChip(Loc.T("Rp_ChipGroundLead"),
                Loc.T(profile.GroundFailuresCap == 1 ? "Rp_ChipGroundOne" : "Rp_ChipGroundMany", profile.GroundFailuresCap)));
        if (profile.CriticalRespiteSeconds > 0)
            chips.Add(new RiskProfileChip(Loc.T("Rp_ChipRespiteLead", profile.CriticalRespiteSeconds / 60),
                Loc.T("Rp_ChipRespiteRest")));
        chips.Add(new RiskProfileChip(VatsimAdviceLead, VatsimAdviceRest));
        Chips = chips;

        static string FormatMultiplier(double value) {
            if (Math.Abs(value - 1.0) < 0.001) return Loc.T("Rp_MultReference", $"{value:0.0}");
            var percentage = Math.Round((value < 1 ? 1 - value : value - 1) * 100);
            return Loc.T("Rp_MultPercent", $"{value:0.##}", percentage, Loc.T(value < 1 ? "Rp_MultLess" : "Rp_MultMore"));
        }
    }

    /// <summary>Tasa por hora a texto: entero a partir de 10, un decimal desde 1, dos decimales por debajo;
    /// separador decimal según el idioma activo.</summary>
    private static string FormatRate(double value) {
        if (value == 0) return "0";
        if (value < 0.01) return Loc.T("Rp_RateBelowMin");
        var culture = CultureInfo.CurrentUICulture;
        return value >= 10
            ? value.ToString("0", culture)
            : value >= 1
                ? value.ToString("0.#", culture)
                : value.ToString("0.00", culture);
    }

    private static SolidColorBrush FrozenBrush(string hex) {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush FrozenBrushAlpha(byte r, byte g, byte b, byte alpha) {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>Crea las cuatro opciones con sus textos fijos de producto (localizados) y colores del mock aprobado.</summary>
    public static IReadOnlyList<RiskProfileOptionViewModel> CreateDefaults() => [
        new(RiskLevel.Low, 1, Loc.T("Rp_Low_Name"),
            Loc.T("Rp_Low_Desc"),
            "#4CAF50", Loc.T("Rp_VatsimSafe"), Loc.T("Rp_VatsimLead"),
            Loc.T("Rp_VatsimLowRest"),
            (76, 175, 80), "#7DD487"),
        new(RiskLevel.Moderate, 2, Loc.T("Rp_Moderate_Name"),
            Loc.T("Rp_Moderate_Desc"),
            "#F59E0B", Loc.T("Rp_VatsimIdeal"), Loc.T("Rp_VatsimLead"),
            Loc.T("Rp_VatsimModerateRest"),
            (76, 175, 80), "#7DD487"),
        new(RiskLevel.High, 3, Loc.T("Rp_High_Name"),
            Loc.T("Rp_High_Desc"),
            "#EF4444", Loc.T("Rp_VatsimNotRecommended"), Loc.T("Rp_VatsimLead"),
            Loc.T("Rp_VatsimHighRest"),
            (245, 158, 11), "#F5C04B"),
        new(RiskLevel.Squawk7700, 4, "7700",
            Loc.T("Rp_7700_Desc"),
            "#8B5CF6", Loc.T("Rp_VatsimNotFit"), Loc.T("Rp_VatsimLead"),
            Loc.T("Rp_Vatsim7700Rest"),
            (239, 68, 68), "#F37E7E"),
    ];
}
