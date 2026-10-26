using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.UI.ViewModels.Base;
using System.Windows.Media;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Fragmento de chip con su parte destacada en negrita y el resto del texto.</summary>
public sealed record RiskProfileChip(string Lead, string Rest);

/// <summary>Segmento de la barra de intensidad; el pincel ya viene resuelto (encendido o apagado).</summary>
public sealed record IntensitySegment(Brush Fill);

/// <summary>
/// Tarjeta de un perfil de riesgo de la ventana de selección: textos fijos de producto + valores
/// dinámicos (medidores, chips y estimación) recalculados desde la configuración vigente del motor.
/// </summary>
public sealed class RiskProfileOptionViewModel : ObservableObject {
    private string _estimateDisplay = "≈ … fallas cada 10 vuelos*";
    private string _frequencyLabel = "×…";
    private double _frequencyBar;
    private string _wearLabel = "hasta ×…";
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
        WearLabel = $"hasta ×{wearMax:0.##}";
        WearBar = maxWearMultiplier > 1 ? (wearMax - 1) / (maxWearMultiplier - 1) * 100 : 100;
        EstimateDisplay = $"≈ {Math.Round(dto.TotalPerFlight * 10):0} fallas cada 10 vuelos*";

        var chips = new List<RiskProfileChip>();
        if (profile.GroundFailuresCap > 0)
            chips.Add(new RiskProfileChip("Al iniciar, la aeronave puede venir con hasta ",
                $"{profile.GroundFailuresCap} {(profile.GroundFailuresCap == 1 ? "falla" : "fallas")}"));
        if (profile.CriticalRespiteSeconds > 0)
            chips.Add(new RiskProfileChip($"{profile.CriticalRespiteSeconds / 60:0} min de tregua",
                " tras cada falla crítica"));
        chips.Add(new RiskProfileChip(VatsimAdviceLead, VatsimAdviceRest));
        Chips = chips;

        static string FormatMultiplier(double value) {
            if (Math.Abs(value - 1.0) < 0.001) return $"×{value:0.0} · referencia";
            var percentage = Math.Round((value < 1 ? 1 - value : value - 1) * 100);
            return $"×{value:0.##} · {percentage} % {(value < 1 ? "menos" : "más")}";
        }
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

    /// <summary>Crea las cuatro opciones con sus textos fijos de producto y colores del mock aprobado.</summary>
    public static IReadOnlyList<RiskProfileOptionViewModel> CreateDefaults() => [
        new(RiskLevel.Low, 1, "Bajo",
            "Fallas poco frecuentes y casi siempre leves. Para volar tranquilo y seguir aprendiendo el avión.",
            "#4CAF50", "VATSIM · seguro", "En VATSIM:",
            " casi nunca vas a declarar emergencia; volás online tranquilo.",
            (76, 175, 80), "#7DD487"),
        new(RiskLevel.Moderate, 2, "Moderado",
            "Fallas ocasionales que pueden volverse serias si la aeronave viene desgastada. El punto de equilibrio.",
            "#F59E0B", "VATSIM · ideal", "En VATSIM:",
            " el equilibrio recomendado online — trabajo real de QRH sin desvíos forzados.",
            (76, 175, 80), "#7DD487"),
        new(RiskLevel.High, 3, "Alto",
            "Fallas frecuentes y a menudo graves, varias en simultáneo. Para quienes manejan emergencias con soltura.",
            "#EF4444", "VATSIM · no recomendado", "En VATSIM:",
            " puede tocarte desviar o declarar emergencia; avisale al control con calma.",
            (245, 158, 11), "#F5C04B"),
        new(RiskLevel.Squawk7700, 4, "7700",
            "Máxima presión: fallas graves y en cadena, con poca tregua. El modo “documental de accidentes”.",
            "#8B5CF6", "VATSIM · no apto", "En VATSIM:",
            " la cadencia de emergencias no es manejable en frecuencia; pensado para offline.",
            (239, 68, 68), "#F37E7E"),
    ];
}
