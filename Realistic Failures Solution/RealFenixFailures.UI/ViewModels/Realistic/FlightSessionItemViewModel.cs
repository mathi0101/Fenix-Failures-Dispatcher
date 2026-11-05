using RealFenixFailures.Application.DTOs;
using RealFenixFailures.UI.Localization;
using RealFenixFailures.UI.ViewModels.Base;
using System.Collections.ObjectModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Sesión de vuelo del historial (Paso 2). Expandible para ver sus fallas.</summary>
public sealed class FlightSessionItemViewModel : LocalizedViewModelBase {
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

    public string DiscardedDisplay => IsDiscarded ? Loc.T("Fs_Discarded") : string.Empty;
    public ObservableCollection<TriggeredFailureItemViewModel> TriggeredFailures { get; }

    public bool IsExpanded {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool HasFailures => FailureCount > 0;

    public string DateDisplay => StartedAt.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

    public string RiskLevelDisplay => RiskLevel switch {
        1 => Loc.T("Fs_RiskLow"),
        2 => Loc.T("Fs_RiskModerate"),
        3 => Loc.T("Fs_RiskHigh"),
        _ => "—"
    };

    public string DurationDisplay => Duration.HasValue
        ? $"{(int)Duration.Value.TotalHours:D2}:{Duration.Value.Minutes:D2}"
        : Loc.T("Fs_InProgress");

    public string FailureCountDisplay => FailureCount == 1
        ? Loc.T("Fs_FailuresOne", FailureCount)
        : Loc.T("Fs_FailuresMany", FailureCount);

    public string OfpStatusDisplay {
        get {
            if (string.IsNullOrWhiteSpace(OriginIcao) && string.IsNullOrWhiteSpace(DestinationIcao))
                return Loc.T("Fs_NotRegistered");
            if (string.IsNullOrWhiteSpace(OriginIcao) || string.IsNullOrWhiteSpace(DestinationIcao))
                return OriginIcao ?? DestinationIcao ?? Loc.T("Fs_OfpPartial");
            return $"{OriginIcao} → {DestinationIcao}";
        }
    }

    public bool HasResult => Result is not null;

    public string ResultDisplay {
        get {
            if (Result is null) return string.Empty;
            var r = Result;
            return r.TotalFailures switch {
                0 => Loc.T("Fs_NoFailures"),
                1 => Loc.T("Fs_FailuresOne", r.TotalFailures),
                _ => Loc.T("Fs_FailuresMany", r.TotalFailures)
            };
        }
    }

    public string ResolvedDisplay {
        get {
            if (Result is null) return string.Empty;
            var r = Result;
            var parts = new List<string>();
            if (r.ResolvedByPilot > 0) parts.Add(Loc.T("Fs_ResolvedByPilot", r.ResolvedByPilot));
            if (r.AutoResolved > 0) parts.Add(Loc.T("Fs_ResolvedAuto", r.AutoResolved));
            if (r.ActiveAtEnd > 0) parts.Add(Loc.T("Fs_ResolvedAtEnd", r.ActiveAtEnd));
            return parts.Count == 0 ? "N/D" : string.Join(" · ", parts);
        }
    }

    protected override void OnLanguageChanged() {
        OnPropertyChanged(nameof(DiscardedDisplay));
        OnPropertyChanged(nameof(RiskLevelDisplay));
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(FailureCountDisplay));
        OnPropertyChanged(nameof(OfpStatusDisplay));
        OnPropertyChanged(nameof(ResultDisplay));
        OnPropertyChanged(nameof(ResolvedDisplay));
    }
}
