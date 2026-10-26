using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.UI.ViewModels.Base;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>
/// ViewModel del modal de selección de perfil de riesgo: siempre refleja la configuración leída vigente
/// (perfiles resueltos por <see cref="RealisticModeSettings.ProfileFor"/> y física del motor) y recalcula
/// los mensajes y las tasas por hora de vuelo cada vez que se abre la ventana.
/// </summary>
public sealed class RiskProfileWindowViewModel : ObservableObject {
    private readonly RealisticModeViewModel _owner;
    private readonly IRealisticExpectationService? _expectationService;
    private readonly Func<double?> _averageWear;
    private RiskProfileOptionViewModel? _selected;
    private string _footNote = FootNoteBase;
    private string _estimateFootNote = string.Empty;

    private const string FootNoteBase = "La elección queda fija al iniciar la sesión.";

    public RiskProfileWindowViewModel(
        RealisticModeViewModel owner,
        IRealisticExpectationService? expectationService,
        Func<double?> averageWear) {
        _owner = owner;
        _expectationService = expectationService;
        _averageWear = averageWear;
        Profiles = RiskProfileOptionViewModel.CreateDefaults();
        foreach (var option in Profiles)
            option.SelectedChanged += (_, _) => Selected = option;
    }

    public IReadOnlyList<RiskProfileOptionViewModel> Profiles { get; }

    public RiskProfileOptionViewModel? Selected {
        get => _selected;
        private set {
            if (!SetProperty(ref _selected, value)) return;
            OnPropertyChanged(nameof(CanConfirm));
            FootNote = value is null ? FootNoteBase : $"Seleccionado: {value.Name}. {FootNoteBase}";
        }
    }

    public bool CanConfirm => Selected is not null;

    public string FootNote { get => _footNote; private set => SetProperty(ref _footNote, value); }

    public string EstimateFootNote { get => _estimateFootNote; private set => SetProperty(ref _estimateFootNote, value); }

    /// <summary>Recalcula todo desde la configuración vigente y preselecciona el perfil actual del panel.</summary>
    public async Task RefreshAsync() {
        if (_expectationService is null) {
            EstimateFootNote = "* Estimación no disponible en esta sesión.";
            return;
        }

        try {
            var wear = _averageWear();
            var expectations = await _expectationService.ComputePerProfileAsync(wear);

            var maxMultiplier = expectations.Max(e => Math.Max(0, e.Profile.ProbabilityMultiplier));
            var maxWear = expectations.Max(e => Math.Max(1, e.Profile.WearMaxMultiplier));
            foreach (var option in Profiles) {
                var dto = expectations.First(e => e.Risk == option.Risk);
                option.ApplyExpectation(dto, maxMultiplier, maxWear);
            }

            var wearDisplay = wear is null ? "sin datos de desgaste" : $"desgaste medio {Math.Round(wear.Value):0} %";
            EstimateFootNote = $"* Fallas graves (Major) y críticas (Critical) en vuelo, aproximadas por cada 10 horas de vuelo con la "
                + $"configuración vigente del motor y {wearDisplay}. La semilla de tierra (fallas del arranque) se describe en los chips. "
                + "La cadencia real la decide el motor tick a tick.";

            Preselect(_owner.SelectedRiskLevel);
        } catch (Exception) {
            EstimateFootNote = "* No se pudo calcular la estimación con la configuración actual.";
        }
    }

    /// <summary>Aplica la selección vigente; confirma por el mismo comando que usaba la ventana anterior.</summary>
    public bool Confirm() {
        if (Selected is null) return false;
        var command = _owner.SelectRiskCommand;
        var argument = Selected.Level.ToString();
        if (!command.CanExecute(argument)) return false;
        command.Execute(argument);
        return true;
    }

    private void Preselect(int? riskLevel) {
        if (riskLevel is null or < 1 or > 4) return;
        var option = Profiles.First(p => p.Level == riskLevel);
        foreach (var other in Profiles.Where(p => !ReferenceEquals(p, option)))
            other.IsSelected = false;
        Selected = option;
        option.IsSelected = true;
    }
}
