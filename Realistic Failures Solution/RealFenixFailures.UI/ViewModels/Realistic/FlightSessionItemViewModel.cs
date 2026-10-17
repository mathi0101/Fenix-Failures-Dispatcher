using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.ViewModels.Base;
using System.Collections.ObjectModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Sesión de vuelo del historial (Paso 2). Expandible para ver sus fallas.</summary>
public sealed class FlightSessionItemViewModel : ObservableObject {
    private bool _isExpanded;

    public FlightSessionItemViewModel(FlightSessionDto dto) {
        Id = dto.Id;
        StartedAt = dto.StartedAt;
        FinishedAt = dto.FinishedAt;
        RiskLevel = (int)dto.RiskLevel;
        Duration = dto.Duration;
        FailureCount = dto.FailureCount;
        TriggeredFailures = new ObservableCollection<TriggeredFailureItemViewModel>(
            dto.TriggeredFailures
               .OrderBy(f => f.TriggeredAt)
               .Select(f => new TriggeredFailureItemViewModel(f)));
    }

    public int Id { get; }
    public DateTime StartedAt { get; }
    public DateTime? FinishedAt { get; }
    public int RiskLevel { get; }
    public TimeSpan? Duration { get; }
    public int FailureCount { get; }
    public ObservableCollection<TriggeredFailureItemViewModel> TriggeredFailures { get; }

    public bool IsExpanded {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool HasFailures => FailureCount > 0;

    public string DateDisplay => StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string RiskLevelDisplay => RiskLevel switch {
        1 => "Bajo",
        2 => "Moderado",
        3 => "Alto",
        _ => "—"
    };

    public string DurationDisplay => Duration.HasValue
        ? $"{(int)Duration.Value.TotalHours:D2}:{Duration.Value.Minutes:D2}"
        : "En curso";

    public string FailureCountDisplay => FailureCount == 1 ? "1 falla" : $"{FailureCount} fallas";

    public string OfpStatusDisplay => "No registrado";
}
