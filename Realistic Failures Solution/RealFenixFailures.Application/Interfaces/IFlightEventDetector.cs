using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Transversal service (shared across the 3 app modes) that turns raw SimConnect/Fenix snapshots
/// into flight-phase transitions and other real-time events (engine start/stop, altitude bands crossed).
/// It is the single source of truth for "current flight phase" and owns the previous-phase memory,
/// so only one component ever computes it, regardless of which mode is running.
/// </summary>
public interface IFlightEventDetector {
    ComplexFlightPhaseEnum CurrentPhase { get; }

    /// <summary>Raised for every relevant transition detected while processing a snapshot.</summary>
    event EventHandler<FlightEventDto>? FlightEventDetected;

    /// <summary>Processes one aircraft-state snapshot, raises events for any transitions found, and returns the resulting flight phase.</summary>
    ComplexFlightPhaseEnum ProcessSnapshot(SimulatorAircraftStateSnapshot snapshot);

    /// <summary>Clears all remembered state (previous phase, engine state, altitude bands). Call when a mode stops so the next flight starts clean.</summary>
    void Reset();
}
