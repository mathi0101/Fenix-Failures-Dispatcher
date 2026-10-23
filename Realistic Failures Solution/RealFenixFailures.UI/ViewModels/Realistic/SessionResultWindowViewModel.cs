using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.ViewModels.Base;
using System.Collections.ObjectModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Contenido del modal de resultados de una sesión Realista finalizada (diseño objetivo §13, mock aprobado).</summary>
public sealed class SessionResultWindowViewModel : ObservableObject {
    public SessionResultWindowViewModel(SessionResultDto result, IReadOnlyList<SessionEventLogDto>? eventLogs = null, string? registration = null) {
        SessionId = result.SessionId;

        var origin = string.IsNullOrWhiteSpace(result.OriginIcao) ? "????" : result.OriginIcao;
        var destination = string.IsNullOrWhiteSpace(result.DestinationIcao) ? "????" : result.DestinationIcao;
        RouteDisplay = $"{origin} → {destination}";
        RegistrationDisplay = string.IsNullOrWhiteSpace(registration) ? string.Empty : registration;
        RiskLevelDisplay = result.RiskLevel switch {
            RiskLevel.Low => "PERFIL BAJO",
            RiskLevel.Moderate => "PERFIL MODERADO",
            RiskLevel.High => "PERFIL ALTO",
            _ => "PERFIL 7700"
        };

        StartedDisplay = $"Inicio {result.StartedAt:dd/MM/yyyy HH:mm:ss} UTC";
        FinishedDisplay = result.FinishedAt is null
            ? "Sin finalizar"
            : $"Fin {result.FinishedAt.Value:dd/MM/yyyy HH:mm:ss} UTC";
        DurationDisplay = result.Duration is null
            ? string.Empty
            : $"Duración {(int)result.Duration.Value.TotalHours:D2}:{result.Duration.Value.Minutes:D2}";

        var s = result.Summary;
        TotalFailures = s.TotalFailures;
        ResolvedByPilot = s.ResolvedByPilot;
        AutoResolved = s.AutoResolved;
        NotResolvedCount = result.Failures.Count(f => f.State == FailureLifecycleState.NotResolved);
        Rejected = s.Rejected;
        ActiveAtEnd = s.ActiveAtEnd;
        MinorCount = s.Minor;
        MajorCount = s.Major;
        CriticalCount = s.Critical;

        foreach (var e in (eventLogs ?? []).OrderBy(e => e.OccurredAt))
            Timeline.Add(new TimelineRow(e.OccurredAt, e.EventCode, e.Description, e.FlightPhase.ToString()));

        foreach (var f in result.Failures.OrderBy(f => f.TriggeredAt))
            Failures.Add(new FailureRow(f));

        foreach (var w in result.WearDeltas.OrderBy(w => w.AtaId))
            WearRows.Add(new WearRow(w));

        HasWearRows = WearRows.Count > 0;
        HasTimeline = Timeline.Count > 0;
    }

    public int SessionId { get; }

    public string RouteDisplay { get; }
    public string RegistrationDisplay { get; }
    public string RiskLevelDisplay { get; }
    public string StartedDisplay { get; }
    public string FinishedDisplay { get; }
    public string DurationDisplay { get; }

    public int TotalFailures { get; }
    public int ResolvedByPilot { get; }
    public int AutoResolved { get; }
    public int NotResolvedCount { get; }
    public int Rejected { get; }
    public int ActiveAtEnd { get; }
    public int MinorCount { get; }
    public int MajorCount { get; }
    public int CriticalCount { get; }

    public bool HasTimeline { get; }
    public bool HasWearRows { get; }

    public ObservableCollection<TimelineRow> Timeline { get; } = [];
    public ObservableCollection<FailureRow> Failures { get; } = [];
    public ObservableCollection<WearRow> WearRows { get; } = [];

    /// <summary>Fila de la cronología de la sesión (todos los eventos de vuelo y de fallas, en UTC).</summary>
    public sealed record TimelineRow {
        public TimelineRow(DateTime occurredAt, string eventCode, string description, string phaseName) {
            TimeDisplay = occurredAt.ToString("HH:mm:ss") + " UTC";
            EventCode = eventCode;
            Description = description;
            PhaseName = phaseName;
            Kind = eventCode switch {
                "FAILURE_TRIGGERED" or "FAILURE_CONFIRMED_ACTIVE" => RowKind.Trigger,
                "FAILURE_RESOLVED_BY_PILOT" or "FAILURE_AUTO_RESOLVED" => RowKind.Good,
                "FAILURE_NOT_RESOLVED" => RowKind.Bad,
                "FAILURE_REJECTED" => RowKind.Muted,
                _ => RowKind.Info
            };
        }

        public string TimeDisplay { get; }
        public string EventCode { get; }
        public string Description { get; }
        public string PhaseName { get; }
        public RowKind Kind { get; }
    }

    /// <summary>Fila de falla lanzada con su consolidación.</summary>
    public sealed class FailureRow {
        public FailureRow(FailureResultDto f) {
            Name = f.FailureName;
            SeverityDisplay = f.Severity switch {
                FailureSeverity.Minor => "Minor",
                FailureSeverity.Major => "Major",
                FailureSeverity.Critical => "Critical",
                _ => string.Empty
            };
            SeverityKind = f.Severity switch {
                FailureSeverity.Major => "major",
                FailureSeverity.Critical => "crit",
                _ => "minor"
            };
            ShowSeverity = !string.IsNullOrEmpty(SeverityDisplay);
            StateDisplay = f.State switch {
                FailureLifecycleState.ResolvedByPilot => "Resuelta por el piloto",
                FailureLifecycleState.AutoResolved => "Auto-resuelta (engine)",
                FailureLifecycleState.NotResolved => "No resuelta (sesión terminó)",
                FailureLifecycleState.Rejected => "Rechazada (no evento)",
                FailureLifecycleState.Active => "Activa al cierre",
                _ => f.State.ToString()
            };
            StateKind = f.State switch {
                FailureLifecycleState.ResolvedByPilot or FailureLifecycleState.AutoResolved => "ok",
                FailureLifecycleState.NotResolved => "nr",
                _ => "grey"
            };
            Detail = f.ResolvedAtUtc is null
                ? $"{f.TriggeredAt:HH:mm:ss} UTC · {f.FlightPhase}"
                : $"{f.TriggeredAt:HH:mm:ss} UTC → {f.ResolvedAtUtc:HH:mm:ss} UTC · Fuente: {f.ResolutionSource?.ToString() ?? "—"}";
            IsUnresolved = f.State == FailureLifecycleState.NotResolved;
        }

        public string Name { get; }
        public string SeverityDisplay { get; }
        public string SeverityKind { get; }
        public bool ShowSeverity { get; }
        public string StateDisplay { get; }
        public string StateKind { get; }
        public string Detail { get; }
        public bool IsUnresolved { get; }
    }

    /// <summary>Fila de desgaste por componente: delta aplicado por la sesión + total acumulado (§12).</summary>
    public sealed class WearRow {
        public WearRow(SystemWearDeltaDto w) {
            SystemName = w.SystemName;
            AtaDisplay = $"ATA {w.AtaId}";
            DeltaDisplay = $"+{w.DeltaPercentage.ToString("0.0")} %";
            TotalDisplay = $"{w.TotalPercentage.ToString("0.0")} %";
            BarWidth = Math.Clamp(w.TotalPercentage, 0, 100);
            RequiresMaintenance = w.RequiresMaintenance;
        }

        public string SystemName { get; }
        public string AtaDisplay { get; }
        public string DeltaDisplay { get; }
        public string TotalDisplay { get; }
        public double BarWidth { get; }
        public bool RequiresMaintenance { get; }
    }

    public enum RowKind { Info, Trigger, Good, Bad, Muted }
}
