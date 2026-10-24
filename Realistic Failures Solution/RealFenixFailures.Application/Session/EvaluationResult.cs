﻿using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Session;

/// <summary>
/// Immutable result returned by <c>SessionProcessorEngine.EvaluateAsync</c> on every timer tick.
/// The manager inspects this to decide whether it must pause the session (lost connection),
/// which failures (if any) were triggered and which lifecycle transitions were confirmed by
/// Fenix during this tick, so it can raise the corresponding events.
/// </summary>
public sealed class EvaluationResult {
    /// <summary>
    /// When true, the manager should pause the session (e.g. the simulator/Fenix connection was lost).
    /// </summary>
    public bool ShouldPauseSession { get; }

    /// <summary>
    /// Failures that were successfully triggered during this evaluation tick.
    /// Empty when nothing was triggered.
    /// </summary>
    public IReadOnlyList<TriggeredFailureInfo> TriggeredFailures { get; }

    /// <summary>
    /// Lifecycle transitions confirmed by Fenix during this tick (Sent→Active, Active→ResolvedByPilot, ...).
    /// Empty when no transition was observed.
    /// </summary>
    public IReadOnlyList<FailureStateChangeInfo> StateChanges { get; }

    private EvaluationResult(bool shouldPauseSession, IReadOnlyList<TriggeredFailureInfo> triggeredFailures, IReadOnlyList<FailureStateChangeInfo> stateChanges) {
        ShouldPauseSession = shouldPauseSession;
        TriggeredFailures = triggeredFailures ?? Array.Empty<TriggeredFailureInfo>();
        StateChanges = stateChanges ?? Array.Empty<FailureStateChangeInfo>();
    }

    /// <summary>
    /// The simulator/Fenix connection is not available. The manager should pause the session.
    /// </summary>
    public static EvaluationResult Disconnected() =>
        new(shouldPauseSession: true, triggeredFailures: Array.Empty<TriggeredFailureInfo>(), stateChanges: Array.Empty<FailureStateChangeInfo>());

    /// <summary>
    /// The tick completed normally but no failure was triggered (cooldown, probability roll, guards, etc.).
    /// </summary>
    public static EvaluationResult NoAction() =>
        new(shouldPauseSession: false, triggeredFailures: Array.Empty<TriggeredFailureInfo>(), stateChanges: Array.Empty<FailureStateChangeInfo>());

    /// <summary>
    /// The tick completed and one or more failures were triggered.
    /// </summary>
    public static EvaluationResult Completed(IReadOnlyList<TriggeredFailureInfo> triggeredFailures) =>
        new(shouldPauseSession: false, triggeredFailures: triggeredFailures ?? Array.Empty<TriggeredFailureInfo>(), stateChanges: Array.Empty<FailureStateChangeInfo>());

    /// <summary>
    /// Returns a copy of this result with the given lifecycle transitions attached (the transitions
    /// observed during the same tick: Sent→Active, Active→ResolvedByPilot, ...).
    /// </summary>
    public EvaluationResult WithStateChanges(IReadOnlyList<FailureStateChangeInfo> stateChanges) =>
        new(ShouldPauseSession, TriggeredFailures, stateChanges);
}

/// <summary>
/// Lightweight, transport-friendly description of a failure whose lifecycle state changed during
/// evaluation (confirmed active by Fenix, resolved by pilot, ...). The manager maps this onto
/// <c>FailureStateChangedEventArgs</c> when raising UI events.
/// </summary>
public sealed record FailureStateChangeInfo(
    string FenixFailureId,
    string Description,
    FailureLifecycleState NewState,
    FailureSeverity? Severity,
    DateTime OccurredAtUtc);

/// <summary>
/// Lightweight, transport-friendly description of a failure that was triggered during evaluation.
/// The manager maps this onto <c>FailureTriggeredEventArgs</c> when raising UI events.
/// </summary>
public record TriggeredFailureInfo(
    string FenixFailureId,
    string Description,
    string PresetName,
    DateTime TriggeredAtUtc);
