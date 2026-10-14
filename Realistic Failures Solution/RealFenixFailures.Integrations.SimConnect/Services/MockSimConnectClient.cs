﻿using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Interfaces;
using RealFenixFailures.Integrations.SimConnect.Models;

namespace RealFenixFailures.Integrations.SimConnect;

public class MockSimConnectClient : ISimConnectClient, IFlightSimulationController {
    private static readonly TimeSpan FlightDuration = TimeSpan.FromMinutes(5);
    private static readonly FlightKeyframe[] Flight = [
        new(0, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0)),
        new(5, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 1, 0, 20, 0, 0)),
        new(12, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 1, 1, 60, 20, 0)),
        new(20, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 1, 1, 60, 60, 0)),
        new(25, State(-34.8379, -56.0295, 105, 0, 70, 0, 8, 0, true, 0, 1, 1, 35, 35, 20)),
        new(45, State(-34.8350, -56.0210, 105, 0, 60, 0, 15, 0, true, 1, 1, 1, 38, 38, 25)),
        new(50, State(-34.8337, -56.0170, 105, 0, 60, 0, 45, 0, true, 2, 1, 1, 82, 82, 95)),
        new(60, State(-34.8280, -56.0000, 105, 0, 60, 0, 130, 0, true, 2, 1, 1, 94, 94, 100)),
        new(65, State(-34.8180, -55.9800, 405, 300, 60, 2800, 155, 160, false, 2, 1, 1, 92, 92, 95)),
        new(100, State(-34.7000, -55.7500, 4605, 4500, 45, 2200, 250, 270, false, 1, 1, 1, 88, 88, 88)),
        new(125, State(-34.5700, -55.6500, 7105, 7000, 15, 0, 285, 300, false, 0, 1, 1, 82, 82, 78)),
        new(165, State(-34.4800, -55.9000, 7105, 7000, 270, 0, 300, 315, false, 0, 1, 1, 78, 78, 72)),
        new(190, State(-34.5800, -56.1500, 4605, 4500, 220, -1500, 255, 270, false, 1, 1, 1, 62, 62, 42)),
        new(210, State(-34.7200, -56.1700, 1905, 1800, 175, -1400, 205, 215, false, 2, 1, 1, 54, 54, 35)),
        new(230, State(-34.8050, -56.0800, 405, 300, 120, -700, 145, 150, false, 3, 1, 1, 48, 48, 28)),
        new(235, State(-34.8230, -56.0550, 105, 0, 105, -100, 115, 120, true, 3, 1, 1, 42, 42, 20)),
        new(250, State(-34.8320, -56.0410, 105, 0, 85, 0, 30, 0, true, 1, 1, 1, 36, 36, 18)),
        new(275, State(-34.8370, -56.0320, 105, 0, 70, 0, 10, 0, true, 0, 1, 1, 32, 32, 12)),
        new(290, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 1, 1, 28, 28, 0)),
        new(295, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 1, 0, 20, 0)),
        new(300, State(-34.8384, -56.0308, 105, 0, 70, 0, 0, 0, true, 0, 0, 0, 0, 0, 0))
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
