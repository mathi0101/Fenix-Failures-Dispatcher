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
        OriginIcao = dto.OriginIcao;
        DestinationIcao = dto.DestinationIcao;
        Result = dto.Result;
        IsDiscarded = dto.DiscardedAt is not null;
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
    public string? OriginIcao { get; }
    public string? DestinationIcao { get; }
    public ResultSummaryDto? Result { get; }

    /// <summary>Sesión descartada desde la recuperación (§6): quedó en historial sin desgaste.</summary>
    public bool IsDiscarded { get; }

    public string DiscardedDisplay => IsDiscarded ? "Descartada" : string.Empty;
    public ObservableCollection<TriggeredFailureItemViewModel> TriggeredFailures { get; }

    public bool IsExpanded {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool HasFailures => FailureCount > 0;

    public string DateDisplay => StartedAt.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

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

    public string OfpStatusDisplay {
        get {
            if (string.IsNullOrWhiteSpace(OriginIcao) && string.IsNullOrWhiteSpace(DestinationIcao))
                return "No registrado";
            if (string.IsNullOrWhiteSpace(OriginIcao) || string.IsNullOrWhiteSpace(DestinationIcao))
                return OriginIcao ?? DestinationIcao ?? "OFP parcial";
            return $"{OriginIcao} → {DestinationIcao}";
        }
    }

    public bool HasResult => Result is not null;

    public string ResultDisplay {
        get {
            if (Result is null) return string.Empty;
            var r = Result;
            return r.TotalFailures switch {
                0 => "Sin fallas",
                1 => "1 falla",
                _ => $"{r.TotalFailures} fallas"
            };
        }
    }

    public string ResolvedDisplay {
        get {
            if (Result is null) return string.Empty;
            var r = Result;
            var parts = new List<string>();
            if (r.ResolvedByPilot > 0) parts.Add($"{r.ResolvedByPilot} piloto");
            if (r.AutoResolved > 0) parts.Add($"{r.AutoResolved} auto");
            if (r.ActiveAtEnd > 0) parts.Add($"{r.ActiveAtEnd} al final");
            return parts.Count == 0 ? "N/D" : string.Join(" · ", parts);
        }
    }
}
