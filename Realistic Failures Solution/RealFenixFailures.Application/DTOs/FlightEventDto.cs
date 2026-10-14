namespace RealFenixFailures.Application.DTOs;

/// <summary>Kind of transversal flight event surfaced to the on-screen real-time log.</summary>
public enum FlightEventKind {
    EnginesStarted,
    EnginesStopped,
    PhaseChanged,
    AltitudeCrossed
}

/// <summary>A single real-time flight event detected from SimConnect/Fenix data, meant for the on-screen log (not the functional/Serilog log).</summary>
public record FlightEventDto(DateTime TimestampUtc, FlightEventKind Kind, string Message);
