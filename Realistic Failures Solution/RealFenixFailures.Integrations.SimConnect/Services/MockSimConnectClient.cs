﻿using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Models;

namespace RealFenixFailures.Integrations.SimConnect;

public class MockSimConnectClient : ISimConnectClient, IFlightSimulationController {
    // Vuelo circular puerta-a-puerta desde/hasta la puerta de SUMU: ~2h35 de escenario real
    // (pushback → arranque → taxi → despegue → ascenso → crucero → descenso → approach →
    // aterrizaje → taxi-in → shutdown). Determinista: el estado es función pura del tiempo de escenario.
    private static readonly TimeSpan FlightDuration = TimeSpan.FromSeconds(9300);
    private static readonly FlightKeyframe[] Flight = [
        new(0, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0)),
        new(480, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0)),
        new(720, State(-34.8384, -56.0310, 105, 0, 60, 0, 3, 0, true, 0, 0, 0, 0, 0, 0)),
        new(840, State(-34.8384, -56.0312, 105, 0, 55, 0, 2, 0, true, 0, 1, 0, 20, 0, 0)),
        new(900, State(-34.8384, -56.0314, 105, 0, 50, 0, 2, 0, true, 0, 1, 1, 22, 22, 0)),
        new(1080, State(-34.8370, -56.0280, 105, 0, 60, 0, 12, 0, true, 0, 1, 1, 20, 20, 15)),
        new(1380, State(-34.8340, -56.0220, 105, 0, 68, 0, 4, 0, true, 2, 1, 1, 20, 20, 18)),
        new(1470, State(-34.8330, -56.0200, 105, 0, 70, 0, 150, 140, true, 2, 1, 1, 94, 94, 95)),
        new(1530, State(-34.8310, -56.0150, 165, 60, 72, 2800, 165, 170, false, 2, 1, 1, 94, 94, 95)),
        new(1680, State(-34.8180, -55.9850, 3500, 3395, 75, 2400, 250, 260, false, 2, 1, 1, 92, 92, 92)),
        new(2160, State(-34.7400, -55.7800, 15000, 14900, 85, 2000, 300, 320, false, 1, 1, 1, 90, 90, 90)),
        new(2880, State(-34.5900, -55.3800, 35000, 34900, 90, 900, 320, 450, false, 0, 1, 1, 88, 88, 88)),
        new(3480, State(-34.5000, -54.5000, 35000, 35000, 95, 0, 455, 450, false, 0, 1, 1, 82, 82, 82)),
        new(4680, State(-33.6000, -53.8000, 35000, 35000, 100, 0, 452, 448, false, 0, 1, 1, 81, 81, 81)),
        new(5880, State(-34.0000, -53.2000, 35000, 35000, 150, 0, 448, 445, false, 0, 1, 1, 81, 81, 81)),
        new(7080, State(-34.4000, -54.4000, 33000, 32900, 250, -1200, 430, 440, false, 0, 1, 1, 40, 40, 30)),
        new(7500, State(-34.7000, -55.3000, 20000, 19900, 280, -1800, 340, 340, false, 0, 1, 1, 25, 25, 15)),
        new(7920, State(-34.8000, -55.9000, 10000, 9900, 300, -1900, 290, 290, false, 0, 1, 1, 22, 22, 12)),
        new(8220, State(-34.8400, -56.0000, 3000, 2895, 155, -1000, 210, 200, false, 3, 1, 1, 30, 30, 25)),
        new(8340, State(-34.8420, -56.0200, 1000, 895, 160, -750, 165, 160, false, 4, 1, 1, 35, 35, 30)),
        new(8400, State(-34.8425, -56.0280, 305, 200, 160, -700, 150, 145, false, 4, 1, 1, 30, 30, 22)),
        new(8430, State(-34.8384, -56.0300, 105, 0, 160, -200, 140, 135, true, 4, 1, 1, 15, 15, 0)),
        new(8490, State(-34.8384, -56.0302, 105, 0, 150, 0, 60, 0, true, 1, 1, 1, 18, 18, 0)),
        new(8580, State(-34.8390, -56.0306, 105, 0, 120, 0, 10, 0, true, 0, 1, 1, 18, 18, 12)),
        new(8760, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 1, 1, 20, 20, 12)),
        new(8880, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 8, 8, 0)),
        new(8940, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0)),
        new(9300, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0))
    ];

    private readonly object _stateLock = new();
    private readonly TimeProvider _timeProvider;
    private bool _isConnected;
    private SimConnectAircraftState _aircraftState;
    private DateTimeOffset? _flightStartedAt;

    public event Action<SimConnectAircraftState>? OnAircraftStateChanged;
    public event Action<bool>? OnConnectionStateChanged;

    public MockSimConnectClient(
        bool isConnected = true,
        SimConnectAircraftState? aircraftState = null,
        TimeProvider? timeProvider = null) {
        _isConnected = isConnected;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _aircraftState = aircraftState?.Clone() ?? Flight[0].AircraftState.Clone();
        _aircraftState.IsConnected = isConnected;
    }

    public void StartFlightSimulation() {
        SimConnectAircraftState state;
        lock (_stateLock) {
            _flightStartedAt = _timeProvider.GetUtcNow();
            _aircraftState = Flight[0].AircraftState.Clone();
            _aircraftState.IsConnected = _isConnected;
            state = _aircraftState.Clone();
        }

        OnAircraftStateChanged?.Invoke(state);
    }

    public void StopFlightSimulation() {
        SimConnectAircraftState state;
        lock (_stateLock) {
            _flightStartedAt = null;
            _aircraftState = Flight[0].AircraftState.Clone();
            _aircraftState.IsConnected = _isConnected;
            state = _aircraftState.Clone();
        }

        OnAircraftStateChanged?.Invoke(state);
    }

    public void SetConnectionStatus(bool isConnected) {
        lock (_stateLock) {
            _isConnected = isConnected;
            _aircraftState.IsConnected = isConnected;
        }

        OnConnectionStateChanged?.Invoke(isConnected);
    }

    public void SetAircraftState(SimConnectAircraftState aircraftState) {
        SimConnectAircraftState state;
        lock (_stateLock) {
            _flightStartedAt = null;
            _aircraftState = aircraftState.Clone();
            _aircraftState.IsConnected = _isConnected;
            state = _aircraftState.Clone();
        }

        OnAircraftStateChanged?.Invoke(state);
    }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateLock) {
            return Task.FromResult(_isConnected);
        }
    }

    public Task<SimConnectAircraftState> GetAircraftStateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock) {
            if (_flightStartedAt.HasValue) {
                var elapsed = _timeProvider.GetUtcNow() - _flightStartedAt.Value;
                _aircraftState = GetFlightState(elapsed);
                _aircraftState.IsConnected = _isConnected;
            }

            return Task.FromResult(_aircraftState.Clone());
        }
    }

    private static SimConnectAircraftState GetFlightState(TimeSpan elapsed) {
        if (elapsed <= TimeSpan.Zero)
            return Flight[0].AircraftState.Clone();

        if (elapsed >= FlightDuration)
            return Flight[^1].AircraftState.Clone();

        var elapsedSeconds = elapsed.TotalSeconds;
        for (var index = 1; index < Flight.Length; index++) {
            var next = Flight[index];
            if (elapsedSeconds > next.ElapsedSeconds)
                continue;

            var previous = Flight[index - 1];
            var progress = (elapsedSeconds - previous.ElapsedSeconds) /
                (next.ElapsedSeconds - previous.ElapsedSeconds);
            return Interpolate(previous.AircraftState, next.AircraftState, progress);
        }

        return Flight[^1].AircraftState.Clone();
    }

    private static SimConnectAircraftState Interpolate(
        SimConnectAircraftState from,
        SimConnectAircraftState to,
        double progress) {
        return new SimConnectAircraftState {
            IsConnected = true,
            Latitude = Lerp(from.Latitude, to.Latitude, progress),
            Longitude = Lerp(from.Longitude, to.Longitude, progress),
            AltitudeMSL = Lerp(from.AltitudeMSL, to.AltitudeMSL, progress),
            IndicatedAltitude = Lerp(from.IndicatedAltitude, to.IndicatedAltitude, progress),
            RadioHeight = Lerp(from.RadioHeight, to.RadioHeight, progress),
            Heading = Lerp(from.Heading, to.Heading, progress),
            VerticalSpeed = Lerp(from.VerticalSpeed, to.VerticalSpeed, progress),
            GroundSpeed = Lerp(from.GroundSpeed, to.GroundSpeed, progress),
            TrueAirspeed = Lerp(from.TrueAirspeed, to.TrueAirspeed, progress),
            AirspeedIndicated = Lerp(from.AirspeedIndicated, to.AirspeedIndicated, progress),
            IsOnGround = progress < 1 ? from.IsOnGround : to.IsOnGround,
            FlapsHandleIndex = progress < 1 ? from.FlapsHandleIndex : to.FlapsHandleIndex,
            Engine1Combustion = progress < 1 ? from.Engine1Combustion : to.Engine1Combustion,
            Engine2Combustion = progress < 1 ? from.Engine2Combustion : to.Engine2Combustion,
            Engine1N1Percent = Lerp(from.Engine1N1Percent, to.Engine1N1Percent, progress),
            Engine2N1Percent = Lerp(from.Engine2N1Percent, to.Engine2N1Percent, progress),
            ThrottlePercent1 = Lerp(from.ThrottlePercent1, to.ThrottlePercent1, progress),
            ThrottlePercent2 = Lerp(from.ThrottlePercent2, to.ThrottlePercent2, progress)
        };
    }

    private static int Lerp(int from, int to, double progress) =>
        (int)Math.Round(from + ((to - from) * progress));

    private static double Lerp(double from, double to, double progress) =>
        from + ((to - from) * progress);

    private static SimConnectAircraftState State(
        double latitude,
        double longitude,
        int altitudeMsl,
        int radioHeight,
        int heading,
        int verticalSpeed,
        int groundSpeed,
        int trueAirspeed,
        bool isOnGround,
        int flapsHandleIndex,
        int engine1Combustion,
        int engine2Combustion,
        double engine1N1Percent,
        double engine2N1Percent,
        double throttlePercent) {
        return new SimConnectAircraftState {
            IsConnected = true,
            Latitude = latitude,
            Longitude = longitude,
            AltitudeMSL = altitudeMsl,
            IndicatedAltitude = altitudeMsl,
            RadioHeight = radioHeight,
            Heading = heading,
            VerticalSpeed = verticalSpeed,
            GroundSpeed = groundSpeed,
            TrueAirspeed = trueAirspeed,
            AirspeedIndicated = trueAirspeed,
            IsOnGround = isOnGround,
            FlapsHandleIndex = flapsHandleIndex,
            Engine1Combustion = engine1Combustion,
            Engine2Combustion = engine2Combustion,
            Engine1N1Percent = engine1N1Percent,
            Engine2N1Percent = engine2N1Percent,
            ThrottlePercent1 = throttlePercent,
            ThrottlePercent2 = throttlePercent
        };
    }

    private sealed record FlightKeyframe(double ElapsedSeconds, SimConnectAircraftState AircraftState);
}
