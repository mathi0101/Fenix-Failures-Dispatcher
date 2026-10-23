using RealFenixFailures.Domain.Services;
using RealFenixFailures.Integrations.SimConnect;

namespace RealFenixFailures.TestUnits.Integrations;

public class MockSimConnectClientTests {
    [Fact]
    public async Task Aircraft_waits_cold_and_dark_at_Sumu_until_a_flight_starts() {
        var clock = new ManualTimeProvider();
        var client = new MockSimConnectClient(timeProvider: clock);

        var state = await client.GetAircraftStateAsync(CancellationToken.None);

        Assert.True(state.IsConnected);
        Assert.True(state.IsOnGround);
        Assert.Equal(-34.8384, state.Latitude, 4);
        Assert.Equal(-56.0308, state.Longitude, 4);
        Assert.Equal(0, state.Engine1Combustion);
        Assert.Equal(0, state.Engine2Combustion);
        Assert.Equal(0, state.Engine1N1Percent);
        Assert.Equal(0, state.Engine2N1Percent);
    }

    [Fact]
    public async Task Started_flight_progresses_through_pushback_engine_start_taxi_takeoff_cruise_and_landing() {
        var clock = new ManualTimeProvider();
        var client = new MockSimConnectClient(timeProvider: clock);
        client.StartFlightSimulation();

        clock.Advance(TimeSpan.FromSeconds(720));
        var pushback = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(pushback.IsOnGround);
        Assert.Equal(0, pushback.Engine1Combustion);
        Assert.Equal(0, pushback.Engine2Combustion);

        clock.Advance(TimeSpan.FromSeconds(120));
        var engineStart = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(engineStart.IsOnGround);
        Assert.Equal(1, engineStart.Engine1Combustion);
        Assert.True(engineStart.Engine1N1Percent >= 20);

        clock.Advance(TimeSpan.FromSeconds(60));
        var engine2Start = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.Equal(1, engine2Start.Engine2Combustion);

        clock.Advance(TimeSpan.FromSeconds(180));
        var taxi = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(taxi.IsOnGround);
        Assert.InRange(taxi.GroundSpeed, 8, 15);

        clock.Advance(TimeSpan.FromSeconds(390));
        var takeoffRoll = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(takeoffRoll.IsOnGround);
        Assert.True(takeoffRoll.GroundSpeed > 45);
        Assert.True(takeoffRoll.ThrottlePercent1 >= 95);

        clock.Advance(TimeSpan.FromSeconds(60));
        var rotation = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(rotation.IsOnGround);
        Assert.True(rotation.VerticalSpeed > 0);

        clock.Advance(TimeSpan.FromSeconds(150));
        var climb = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(climb.IsOnGround);
        Assert.True(climb.VerticalSpeed > 0);
        Assert.True(climb.RadioHeight > 300);

        clock.Advance(TimeSpan.FromSeconds(1800));
        var cruise = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(cruise.IsOnGround);
        Assert.Equal(35000, cruise.RadioHeight);
        Assert.Equal(0, cruise.VerticalSpeed);

        clock.Advance(TimeSpan.FromSeconds(4950));
        var landed = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(landed.IsOnGround);
        Assert.True(landed.GroundSpeed > 0);
        Assert.Equal(1, landed.Engine1Combustion);
        Assert.Equal(1, landed.Engine2Combustion);

        clock.Advance(TimeSpan.FromSeconds(60));
        var rollout = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(rollout.IsOnGround);
        Assert.True(rollout.GroundSpeed > 0);
        Assert.Equal(1, rollout.Engine1Combustion);
    }

    [Fact]
    public async Task Flight_reaches_and_holds_the_cold_and_dark_Sumu_gate_after_two_and_a_half_hours() {
        var clock = new ManualTimeProvider();
        var client = new MockSimConnectClient(timeProvider: clock);
        client.StartFlightSimulation();

        clock.Advance(TimeSpan.FromSeconds(8490));
        var beforeCompletion = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.True(beforeCompletion.Engine1Combustion == 1 || beforeCompletion.Engine2Combustion == 1);

        clock.Advance(TimeSpan.FromSeconds(450));
        var completed = await client.GetAircraftStateAsync(CancellationToken.None);
        AssertAtColdAndDarkSumuGate(completed);

        clock.Advance(TimeSpan.FromHours(1));
        var held = await client.GetAircraftStateAsync(CancellationToken.None);
        AssertAtColdAndDarkSumuGate(held);
    }

    [Fact]
    public async Task Starting_another_flight_restarts_the_full_timeline() {
        var clock = new ManualTimeProvider();
        var client = new MockSimConnectClient(timeProvider: clock);
        client.StartFlightSimulation();
        clock.Advance(TimeSpan.FromMinutes(155));
        AssertAtColdAndDarkSumuGate(await client.GetAircraftStateAsync(CancellationToken.None));

        client.StartFlightSimulation();
        clock.Advance(TimeSpan.FromSeconds(840));
        var restarted = await client.GetAircraftStateAsync(CancellationToken.None);

        Assert.True(restarted.IsOnGround);
        Assert.True(restarted.GroundSpeed > 0);
        Assert.Equal(1, restarted.Engine1Combustion);
        Assert.Equal(0, restarted.Engine2Combustion);
    }

    [Fact]
    public async Task Scaled_clock_accelerates_the_whole_flight() {
        var inner = new ManualTimeProvider();
        var scaled = new ScaledTimeProvider(initialScale: 60, inner: inner);
        var client = new MockSimConnectClient(timeProvider: scaled);
        client.StartFlightSimulation();

        inner.Advance(TimeSpan.FromMinutes(1));
        var cruise = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(cruise.IsOnGround);
        Assert.True(cruise.TrueAirspeed > 200);

        scaled.SetScale(1);
        inner.Advance(TimeSpan.FromMinutes(1));
        var stillCruising = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(stillCruising.IsOnGround);

        scaled.Advance(TimeSpan.FromMinutes(50));
        scaled.Advance(TimeSpan.FromMinutes(9));
        var descent = await client.GetAircraftStateAsync(CancellationToken.None);
        Assert.False(descent.IsOnGround);
        Assert.True(descent.VerticalSpeed < 0);

        scaled.Advance(TimeSpan.FromMinutes(40));
        var parked = await client.GetAircraftStateAsync(CancellationToken.None);
        AssertAtColdAndDarkSumuGate(parked);
    }

    [Fact]
    public async Task Returned_aircraft_state_cannot_mutate_the_simulation() {
        var client = new MockSimConnectClient();
        var firstRead = await client.GetAircraftStateAsync(CancellationToken.None);
        firstRead.Latitude = 0;
        firstRead.IsOnGround = false;

        var secondRead = await client.GetAircraftStateAsync(CancellationToken.None);

        Assert.Equal(-34.8384, secondRead.Latitude, 4);
        Assert.True(secondRead.IsOnGround);
    }

    private static void AssertAtColdAndDarkSumuGate(RealFenixFailures.Integrations.SimConnect.Models.SimConnectAircraftState state) {
        Assert.True(state.IsOnGround);
        Assert.Equal(-34.8384, state.Latitude, 4);
        Assert.Equal(-56.0308, state.Longitude, 4);
        Assert.Equal(105, state.AltitudeMSL);
        Assert.Equal(0, state.RadioHeight);
        Assert.Equal(0, state.GroundSpeed);
        Assert.Equal(0, state.TrueAirspeed);
        Assert.Equal(0, state.VerticalSpeed);
        Assert.Equal(0, state.Engine1Combustion);
        Assert.Equal(0, state.Engine2Combustion);
        Assert.Equal(0, state.Engine1N1Percent);
        Assert.Equal(0, state.Engine2N1Percent);
        Assert.Equal(0, state.ThrottlePercent1);
        Assert.Equal(0, state.ThrottlePercent2);
    }

    private sealed class ManualTimeProvider : TimeProvider {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) {
            _utcNow += amount;
        }
    }
}
