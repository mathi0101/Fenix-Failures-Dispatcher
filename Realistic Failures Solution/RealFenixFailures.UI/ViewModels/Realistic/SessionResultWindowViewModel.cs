using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.UI.Localization;
using System.Collections.ObjectModel;
using System.Globalization;

namespace RealFenixFailures.UI.ViewModels.Realistic;

/// <summary>Presentación del resultado autocontenido de una sesión. Se construye al abrir el
/// modal (es efímero): los textos se resuelven con el idioma vigente de ese momento (issue #12);
/// si el usuario cambia de idioma con el modal abierto, se refleja al volver a abrirlo.</summary>
public sealed class SessionResultWindowViewModel {
    public SessionResultWindowViewModel(SessionResultDto result, int wearNoticeThresholdPct) {
        WearNoticeThresholdPct = wearNoticeThresholdPct;
        SessionId = result.SessionId;
        RouteDisplay = $"{ValueOr(result.OriginIcao, "????")} → {ValueOr(result.DestinationIcao, "????")}";
        RegistrationDisplay = $"{ValueOr(result.Registration, Loc.T("Srw_NoRegistration"))} · {ValueOr(result.AircraftIcaoType, Loc.T("Srw_TypeUnavailable"))}";
        OfpDisplay = Loc.T("Srw_OfpDisplay",
            ValueOr(result.FlightNumber, ValueOr(result.Callsign, Loc.T("Srw_NoIdentifier"))),
            ValueOr(result.OfpSource, Loc.T("Srw_SourceUnavailableLower")));
        RiskLevelDisplay = result.RiskLevel switch {
            RiskLevel.Low => Loc.T("Srw_ProfileLow"), RiskLevel.Moderate => Loc.T("Srw_ProfileModerate"),
            RiskLevel.High => Loc.T("Srw_ProfileHigh"), _ => Loc.T("Srw_Profile7700")
        };
        RiskColor = result.RiskLevel switch {
            RiskLevel.Low => "#22C55E", RiskLevel.Moderate => "#3B82F6",
            RiskLevel.High => "#F97316", _ => "#EF4444"
        };
        StartedDisplay = Loc.T("Srw_Started", result.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"));
        FinishedDisplay = result.FinishedAt is DateTime end
            ? Loc.T("Srw_Finished", end.ToString("dd/MM/yyyy HH:mm:ss")) : Loc.T("Srw_NotFinished");
        DurationDisplay = result.Duration is TimeSpan duration
            ? Loc.T("Srw_Duration", (int)duration.TotalHours, duration.Minutes.ToString("D2"), duration.Seconds.ToString("D2"))
            : Loc.T("Srw_DurationUnavailable");
        WearSummaryDisplay = result.WearDeltas.Count > 0
            ? Loc.T("Srw_WearAverageLine", Percent(result.WearDeltas.Average(w => w.DeltaPercentage), true))
            : Loc.T("Srw_NoWear");
        WearEmptyMessage = result.DiscardedAt.HasValue ? Loc.T("Srw_WearDiscarded")
            : Loc.T("Srw_WearEmpty");
        WearThresholdDisplay = Loc.T("Srw_WearThreshold", wearNoticeThresholdPct);
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
        foreach (var w in result.WearDeltas.OrderBy(w => w.AtaId)) WearRows.Add(new WearRow(w, WearNoticeThresholdPct));
    }
    private static string ValueOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string Percent(double value, bool delta = false) =>
        (delta && value > 0 ? "+" : "") + (value is > 0 and < 0.01 ? Loc.T("Srw_PercentBelowMin") : value.ToString("0.##", CultureInfo.CurrentUICulture)) + " %";
    public static string PhaseName(ComplexFlightPhaseEnum phase) => phase switch {
        ComplexFlightPhaseEnum.OnGate => Loc.T("Srw_PhaseOnGate"), ComplexFlightPhaseEnum.Taxi => Loc.T("Srw_PhaseTaxi"),
        ComplexFlightPhaseEnum.Takeoff => Loc.T("Srw_PhaseTakeoff"), ComplexFlightPhaseEnum.Climb => Loc.T("Srw_PhaseClimb"),
        ComplexFlightPhaseEnum.Cruise => Loc.T("Srw_PhaseCruise"), ComplexFlightPhaseEnum.Descent => Loc.T("Srw_PhaseDescent"),
        ComplexFlightPhaseEnum.Approach => Loc.T("Srw_PhaseApproach"), ComplexFlightPhaseEnum.Landing => Loc.T("Srw_PhaseLanding"),
        ComplexFlightPhaseEnum.Parked => Loc.T("Srw_PhaseParked"), _ => Loc.T("Srw_PhaseUnavailable")
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
    public string WearThresholdDisplay { get; }
    private int WearNoticeThresholdPct { get; }
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
            SeverityDisplay = f.Severity?.ToString() ?? Loc.T("Srw_NoSeverity");
            SeverityColor = f.Severity switch {
                FailureSeverity.Critical => "#EF4444", FailureSeverity.Major => "#F97316",
                FailureSeverity.Minor => "#F59E0B", _ => "#94A3B8"
            };
            StateDisplay = StateText(f.State);
            StateColor = f.State switch {
                FailureLifecycleState.ResolvedByPilot or FailureLifecycleState.AutoResolved => "#22C55E",
                FailureLifecycleState.NotResolved => "#FCA5A5", _ => "#94A3B8"
            };
            IsUnresolved = f.State == FailureLifecycleState.NotResolved;
            var source = SourceText(f.ResolutionSource ?? ResolutionSource.SessionEnded);
            Detail = $"{f.TriggeredAt:dd/MM HH:mm:ss} UTC · {PhaseName(f.FlightPhase)}";
            if (f.ResolvedAtUtc is DateTime resolved) {
                var elapsed = resolved - f.TriggeredAt;
                var duration = elapsed >= TimeSpan.Zero
                    ? Loc.T("Srw_Elapsed", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds)
                    : "";
                Detail += $" → {resolved:dd/MM HH:mm:ss} UTC · {source}{duration}";
            }
        }
        internal static string StateText(FailureLifecycleState state) => state switch {
            FailureLifecycleState.ResolvedByPilot => Loc.T("Srw_StateResolvedByPilot"),
            FailureLifecycleState.AutoResolved => Loc.T("Srw_StateAutoResolved"),
            FailureLifecycleState.NotResolved => Loc.T("Srw_StateNotResolved"),
            FailureLifecycleState.Rejected => Loc.T("Srw_StateRejected"),
            FailureLifecycleState.Active => Loc.T("Srw_StateActive"),
            FailureLifecycleState.Sent => Loc.T("Srw_StateSent"),
            FailureLifecycleState.Queued => Loc.T("Srw_StateQueued"),
            _ => Loc.T("Srw_StatePreparing")
        };
        internal static string SourceText(ResolutionSource? source) => source switch {
            ResolutionSource.Pilot => Loc.T("Srw_SourcePilot"),
            ResolutionSource.EngineAuto => Loc.T("Srw_SourceEngine"),
            ResolutionSource.SessionEnded => Loc.T("Srw_SourceSessionEnded"),
            _ => Loc.T("Srw_SourceUnavailable")
        };
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
        public WearRow(SystemWearDeltaDto w, int wearNoticeThresholdPct) {
            SystemName = w.SystemName;
            AtaDisplay = $"ATA {w.AtaId}";
            DeltaDisplay = Percent(w.DeltaPercentage, true);
            TotalDisplay = Percent(w.TotalPercentage);
            TotalPercentage = Math.Clamp(w.TotalPercentage, 0, 100);
            RequiresMaintenance = TotalPercentage >= wearNoticeThresholdPct;
        }
        public string SystemName { get; }
        public string AtaDisplay { get; }
        public string DeltaDisplay { get; }
        public string TotalDisplay { get; }
        public double TotalPercentage { get; }
        public bool RequiresMaintenance { get; }
    }
}
