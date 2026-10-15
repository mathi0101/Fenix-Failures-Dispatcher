// RealFenixFailures.TestUnits/Services/FlightEventDetectorTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.TestUnits.Services;

public class FlightEventDetectorTests {
    private static FlightEventDetector CreateDetector() =>
        new(NullLogger<FlightEventDetector>.Instance, NullLoggerFactory.Instance);

    private static SimulatorAircraftStateSnapshot OnGround(bool enginesOn, int groundSpeed = 0, int throttle = 0, int altMsl = 100) =>
        new() {
            IsOnGround = true,
            AltitudeMSL = altMsl,
            AltitudeAGL = 0,
            GroundSpeed = groundSpeed,
            ThrottlePercent1 = throttle,
            ThrottlePercent2 = throttle,
            Engine1Combustion = enginesOn,
            Engine2Combustion = enginesOn,
            Engine1N1Percent = enginesOn ? 22 : 0,
            Engine2N1Percent = enginesOn ? 22 : 0,
        };

    private static SimulatorAircraftStateSnapshot Airborne(int altAgl, int verticalSpeed, int altMsl) =>
        new() {
            IsOnGround = false,
            AltitudeAGL = altAgl,
            AltitudeMSL = altMsl,
            VerticalSpeed = verticalSpeed,
            GroundSpeed = 240,
            Engine1Combustion = true,
            Engine2Combustion = true,
            Engine1N1Percent = 85,
            Engine2N1Percent = 85,
        };

    [Fact]
    public void ProcessSnapshot_FullFlightCycle_RaisesExpectedPhaseTransitions() {
        var detector = CreateDetector();
        var events = new List<FlightEventDto>();
        detector.FlightEventDetected += (_, e) => events.Add(e);

        Assert.Equal(ComplexFlightPhaseEnum.OnGate, detector.ProcessSnapshot(OnGround(enginesOn: false)));
        Assert.Equal(ComplexFlightPhaseEnum.Taxi, detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 12)));
        Assert.Equal(ComplexFlightPhaseEnum.Climb, detector.ProcessSnapshot(Airborne(altAgl: 3000, verticalSpeed: 1200, altMsl: 3000)));
        Assert.Equal(ComplexFlightPhaseEnum.Cruise, detector.ProcessSnapshot(Airborne(altAgl: 12000, verticalSpeed: 0, altMsl: 12000)));
        Assert.Equal(ComplexFlightPhaseEnum.Descent, detector.ProcessSnapshot(Airborne(altAgl: 12000, verticalSpeed: -800, altMsl: 12000)));
        Assert.Equal(ComplexFlightPhaseEnum.Approach, detector.ProcessSnapshot(Airborne(altAgl: 2500, verticalSpeed: -600, altMsl: 2500)));
        Assert.Equal(ComplexFlightPhaseEnum.Landing, detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 55)));
        Assert.Equal(ComplexFlightPhaseEnum.Taxi, detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 12)));
        Assert.Equal(ComplexFlightPhaseEnum.OnGate, detector.ProcessSnapshot(OnGround(enginesOn: false)));

        var phaseMessages = events.Where(e => e.Kind == FlightEventKind.PhaseChanged).Select(e => e.Message).ToList();
        Assert.Equal(
            new[]
            {
                "Iniciando rodaje (taxi)",
                "Ascenso",
                "Crucero alcanzado",
                "Iniciando descenso",
                "Aproximación",
                "Aterrizaje",
                "Taxi final hacia el gate",
                "Motores apagados en gate"
            },
            phaseMessages);
        Assert.Single(events, e => e.Kind == FlightEventKind.PhaseChanged && e.Message == "Aterrizaje");
    }

    [Fact]
    public void ProcessSnapshot_Touchdown_RaisesLandingForExactlyOneTick() {
        var detector = CreateDetector();
        var events = new List<FlightEventDto>();
        detector.FlightEventDetected += (_, e) => events.Add(e);

        Assert.Equal(ComplexFlightPhaseEnum.Climb, detector.ProcessSnapshot(Airborne(altAgl: 3000, verticalSpeed: 1200, altMsl: 3000)));
        Assert.Equal(ComplexFlightPhaseEnum.Landing, detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 55)));
        Assert.Equal(ComplexFlightPhaseEnum.Taxi, detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 12)));

        Assert.Single(events, e => e.Kind == FlightEventKind.PhaseChanged && e.Message == "Aterrizaje");
        Assert.Contains(events, e => e.Kind == FlightEventKind.PhaseChanged && e.Message == "Taxi final hacia el gate");
    }

    [Fact]
    public void ProcessSnapshot_EngineAndAltitudeEvents_AreRaised() {
        var detector = CreateDetector();
        var events = new List<FlightEventDto>();
        detector.FlightEventDetected += (_, e) => events.Add(e);

        detector.ProcessSnapshot(OnGround(enginesOn: false)); // OnGate
        detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 10)); // taxi + engines started
        detector.ProcessSnapshot(Airborne(altAgl: 8500, verticalSpeed: 1500, altMsl: 9000)); // climb
        detector.ProcessSnapshot(Airborne(altAgl: 9500, verticalSpeed: 1500, altMsl: 10100)); // cross FL100 up
        detector.ProcessSnapshot(Airborne(altAgl: 17000, verticalSpeed: 1200, altMsl: 18500)); // cross FL180 up
        detector.ProcessSnapshot(Airborne(altAgl: 10000, verticalSpeed: -600, altMsl: 12000)); // descent, cross FL180 down
        detector.ProcessSnapshot(Airborne(altAgl: 7000, verticalSpeed: -600, altMsl: 9000)); // cross FL100 down
        detector.ProcessSnapshot(OnGround(enginesOn: false)); // engines stopped

        var crossings = events.Where(e => e.Kind == FlightEventKind.AltitudeCrossed).Select(e => e.Message).ToList();
        Assert.Equal(
            new[]
            {
                "Cruzando FL100 en ascenso",
                "Cruzando FL180 en ascenso",
                "Cruzando FL180 en descenso",
                "Cruzando FL100 en descenso"
            },
            crossings);
        Assert.Contains(events, e => e.Kind == FlightEventKind.EnginesStarted);
        Assert.Contains(events, e => e.Kind == FlightEventKind.EnginesStopped);
    }

    [Fact]
    public void Reset_ClearsState_AndNextSnapshotsDoNotRaiseGhostEvents() {
        var detector = CreateDetector();
        var events = new List<FlightEventDto>();
        detector.FlightEventDetected += (_, e) => events.Add(e);

        detector.ProcessSnapshot(OnGround(enginesOn: false));
        detector.ProcessSnapshot(OnGround(enginesOn: true, groundSpeed: 10));
        Assert.NotEmpty(events);
        events.Clear();

        detector.Reset();
        Assert.Equal(ComplexFlightPhaseEnum.Unknown, detector.CurrentPhase);

        // After a reset the previous phase is unknown: a new on-gate snapshot must not raise a phase event.
        detector.ProcessSnapshot(OnGround(enginesOn: false));
        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessSnapshot_ConcurrentSnapshotsAndReset_DoNotThrow() {
        var detector = CreateDetector();
        var snapshots = new[] {
            OnGround(enginesOn: false),
            OnGround(enginesOn: true, groundSpeed: 12),
            Airborne(altAgl: 3000, verticalSpeed: 1200, altMsl: 3000),
            Airborne(altAgl: 12000, verticalSpeed: 0, altMsl: 12000),
            OnGround(enginesOn: true, groundSpeed: 40)
        };

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
            for (int i = 0; i < 250; i++) {
                detector.ProcessSnapshot(snapshots[i % snapshots.Length]);
                if (i % 64 == 63) detector.Reset();
            }
        }));

        await Task.WhenAll(tasks);

        Assert.True(Enum.IsDefined(detector.CurrentPhase));
    }
}
