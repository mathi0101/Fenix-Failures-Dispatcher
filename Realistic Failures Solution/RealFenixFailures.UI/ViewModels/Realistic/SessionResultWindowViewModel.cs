using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using System.Collections.ObjectModel;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Presentación del resultado autocontenido de una sesión.</summary>
public sealed class SessionResultWindowViewModel {
    public SessionResultWindowViewModel(SessionResultDto result) {
        SessionId = result.SessionId;
        RouteDisplay = $"{ValueOr(result.OriginIcao, "????")} → {ValueOr(result.DestinationIcao, "????")}";
        RegistrationDisplay = $"{ValueOr(result.Registration, "Sin matrícula")} · {ValueOr(result.AircraftIcaoType, "Tipo no disponible")}";
        OfpDisplay = $"OFP {ValueOr(result.FlightNumber, ValueOr(result.Callsign, "sin identificador"))} ({ValueOr(result.OfpSource, "fuente no disponible")})";
        RiskLevelDisplay = result.RiskLevel switch {
            RiskLevel.Low => "PERFIL BAJO", RiskLevel.Moderate => "PERFIL MODERADO",
            RiskLevel.High => "PERFIL ALTO", _ => "PERFIL 7700"
        };
        RiskColor = result.RiskLevel switch {
            RiskLevel.Low => "#22C55E", RiskLevel.Moderate => "#3B82F6",
            RiskLevel.High => "#F97316", _ => "#EF4444"
        };
        StartedDisplay = $"Inicio {result.StartedAt:dd/MM/yyyy HH:mm:ss} UTC";
        FinishedDisplay = result.FinishedAt is DateTime end ? $"Fin {end:dd/MM/yyyy HH:mm:ss} UTC" : "Sin finalizar";
        DurationDisplay = result.Duration is TimeSpan duration
            ? $"Duración {(int)duration.TotalHours} h {duration.Minutes:D2} min {duration.Seconds:D2} s" : "Duración no disponible";
        WearSummaryDisplay = result.WearDeltas.Count > 0
            ? $"Desgaste medio por sistema {Percent(result.WearDeltas.Average(w => w.DeltaPercentage), true)}"
            : "Sin desgaste registrado";
        WearEmptyMessage = result.DiscardedAt.HasValue ? "Sesión descartada: no se aplicó desgaste."
            : "No hay desgaste registrado para esta sesión.";
        TotalFailures = result.Summary.TotalFailures;
        ResolvedByPilot = result.Summary.ResolvedByPilot;
        AutoResolved = result.Summary.AutoResolved;
        NotResolvedCount = result.Failures.Count(f => f.State == FailureLifecycleState.NotResolved);
        Rejected = result.Summary.Rejected;
        ActiveAtEnd = result.Summary.ActiveAtEnd;
        MinorCount = result.Summary.Minor;
        MajorCount = result.Summary.Major;
        CriticalCount = result.Summary.Critical;
        foreach (var e in result.EventLogs.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)) Timeline.Add(new TimelineRow(e));
        foreach (var f in result.Failures.OrderBy(f => f.TriggeredAt).ThenBy(f => f.Id)) {
            if (f.State == FailureLifecycleState.Rejected) RejectedFailures.Add(new FailureRow(f));
            else Failures.Add(new FailureRow(f));
        }
        foreach (var w in result.WearDeltas.OrderBy(w => w.AtaId)) WearRows.Add(new WearRow(w));
    }
    private static string ValueOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string Percent(double value, bool delta = false) =>
        (delta && value > 0 ? "+" : "") + (value is > 0 and < 0.01 ? "<0,01" : value.ToString("0.##")) + " %";
    public static string PhaseName(ComplexFlightPhaseEnum phase) => phase switch {
        ComplexFlightPhaseEnum.OnGate => "Puerta", ComplexFlightPhaseEnum.Taxi => "Taxi",
        ComplexFlightPhaseEnum.Takeoff => "Despegue", ComplexFlightPhaseEnum.Climb => "Ascenso",
        ComplexFlightPhaseEnum.Cruise => "Crucero", ComplexFlightPhaseEnum.Descent => "Descenso",
        ComplexFlightPhaseEnum.Approach => "Aproximación", ComplexFlightPhaseEnum.Landing => "Aterrizaje",
        ComplexFlightPhaseEnum.Parked => "Estacionado", _ => "Fase no disponible"
    };
    public int SessionId { get; }
    public string RouteDisplay { get; }
    public string RegistrationDisplay { get; }
    public string OfpDisplay { get; }
    public string RiskLevelDisplay { get; }
    public string RiskColor { get; }
    public string StartedDisplay { get; }
    public string FinishedDisplay { get; }
    public string DurationDisplay { get; }
    public string WearSummaryDisplay { get; }
    public string WearEmptyMessage { get; }
    public int TotalFailures { get; }
    public int ResolvedByPilot { get; }
    public int AutoResolved { get; }
    public int NotResolvedCount { get; }
    public int Rejected { get; }
    public int ActiveAtEnd { get; }
    public int MinorCount { get; }
    public int MajorCount { get; }
    public int CriticalCount { get; }
    public bool HasTimeline => Timeline.Count > 0;
    public bool HasWearRows => WearRows.Count > 0;
    public bool HasFailures => Failures.Count > 0;
    public bool HasRejected => RejectedFailures.Count > 0;
    public ObservableCollection<TimelineRow> Timeline { get; } = [];
    public ObservableCollection<FailureRow> Failures { get; } = [];
    public ObservableCollection<FailureRow> RejectedFailures { get; } = [];
    public ObservableCollection<WearRow> WearRows { get; } = [];
    public sealed class TimelineRow {
        public TimelineRow(SessionEventLogDto e) {
            TimeDisplay = $"{e.OccurredAt:HH:mm:ss} UTC";
            DateDisplay = $"{e.OccurredAt:dd/MM/yyyy HH:mm:ss} UTC";
            EventCode = e.EventCode;
            Description = e.Description;
            PhaseDisplay = PhaseName(e.FlightPhase);
            Color = e.EventCode switch {
                "FAILURE_NOT_RESOLVED" => "#EF4444",
                "FAILURE_RESOLVED_BY_PILOT" or "FAILURE_AUTO_RESOLVED" => "#22C55E",
                "FAILURE_REJECTED" => "#64748B",
                _ => e.Category switch {
                    SessionEventCategory.EnginesStarted or SessionEventCategory.EnginesStopped => "#22C55E",
                    SessionEventCategory.AltitudeCrossed => "#94A3B8",
                    SessionEventCategory.FailureTriggered or SessionEventCategory.FailureStateChanged => "#F97316",
                    SessionEventCategory.SessionError => "#EF4444", _ => "#3B82F6"
                }
            };
        }
        public string TimeDisplay { get; }
        public string DateDisplay { get; }
        public string EventCode { get; }
        public string Description { get; }
        public string PhaseDisplay { get; }
        public string Color { get; }
    }
    public sealed class FailureRow {
        public FailureRow(FailureResultDto f) {
            Name = ValueOr(f.FailureName, f.FenixFailureId);
            SystemDisplay = string.Join(" · ", new[] {
                f.FailureAtaId is > 0 ? $"ATA {f.FailureAtaId}" : null, f.FailureSystem, f.FailureGroup
            }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            SeverityDisplay = f.Severity?.ToString() ?? "Sin severidad";
            SeverityColor = f.Severity switch {
                FailureSeverity.Critical => "#EF4444", FailureSeverity.Major => "#F97316",
                FailureSeverity.Minor => "#F59E0B", _ => "#94A3B8"
            };
            StateDisplay = f.State switch {
                FailureLifecycleState.ResolvedByPilot => "Resuelta por el piloto",
                FailureLifecycleState.AutoResolved => "Auto-resuelta (engine)",
                FailureLifecycleState.NotResolved => "No resuelta (sesión terminó)",
                FailureLifecycleState.Rejected => "Rechazada (no evento)",
                FailureLifecycleState.Active => "Activa", FailureLifecycleState.Sent => "Enviada a Fenix",
                FailureLifecycleState.Queued => "En cola", _ => "En preparación"
            };
            StateColor = f.State switch {
                FailureLifecycleState.ResolvedByPilot or FailureLifecycleState.AutoResolved => "#22C55E",
                FailureLifecycleState.NotResolved => "#FCA5A5", _ => "#94A3B8"
            };
            IsUnresolved = f.State == FailureLifecycleState.NotResolved;
            var source = f.ResolutionSource switch {
                ResolutionSource.Pilot => "Piloto (despejada en Fenix)",
                ResolutionSource.EngineAuto => "Motor de fallas",
                ResolutionSource.SessionEnded => "Consolidada al cierre", _ => "Fuente no disponible"
            };
            Detail = $"{f.TriggeredAt:dd/MM HH:mm:ss} UTC · {PhaseName(f.FlightPhase)}";
            if (f.ResolvedAtUtc is DateTime resolved) {
                var elapsed = resolved - f.TriggeredAt;
                var duration = elapsed >= TimeSpan.Zero ? $" · {(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2} transcurridos" : "";
                Detail += $" → {resolved:dd/MM HH:mm:ss} UTC · {source}{duration}";
            }
        }
        public string Name { get; }
        public string SystemDisplay { get; }
        public string SeverityDisplay { get; }
        public string SeverityColor { get; }
        public string StateDisplay { get; }
        public string StateColor { get; }
        public string Detail { get; }
        public bool IsUnresolved { get; }
    }
    public sealed class WearRow {
        public WearRow(SystemWearDeltaDto w) {
            SystemName = w.SystemName;
            AtaDisplay = $"ATA {w.AtaId}";
            DeltaDisplay = Percent(w.DeltaPercentage, true);
            TotalDisplay = Percent(w.TotalPercentage);
            TotalPercentage = Math.Clamp(w.TotalPercentage, 0, 100);
            RequiresMaintenance = w.RequiresMaintenance;
        }
        public string SystemName { get; }
        public string AtaDisplay { get; }
        public string DeltaDisplay { get; }
        public string TotalDisplay { get; }
        public double TotalPercentage { get; }
        public bool RequiresMaintenance { get; }
    }
}
