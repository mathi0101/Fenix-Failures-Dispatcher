using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.TestUnits.Domain;

public class ScaledTimeProviderTests {
    [Fact]
    public void GetUtcNow_advances_at_the_configured_scale() {
        var inner = new ManualTimeProvider();
        var clock = new ScaledTimeProvider(initialScale: 60, inner: inner);
        var start = clock.GetUtcNow();

        inner.Advance(TimeSpan.FromMinutes(1));

        var elapsed = clock.GetUtcNow() - start;
        Assert.Equal(TimeSpan.FromHours(1), elapsed);
    }

    [Fact]
    public void SetScale_preserves_the_scenario_time_already_elapsed() {
        var inner = new ManualTimeProvider();
        var clock = new ScaledTimeProvider(initialScale: 60, inner: inner);
        var start = clock.GetUtcNow();

        inner.Advance(TimeSpan.FromMinutes(1));
        var before = clock.GetUtcNow() - start;
        Assert.Equal(TimeSpan.FromHours(1), before);

        clock.SetScale(1);
        var after = clock.GetUtcNow() - start;
        Assert.Equal(TimeSpan.FromHours(1), after);

        inner.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1), clock.GetUtcNow() - start);
    }

    [Fact]
    public void Advance_skips_scenario_time_without_moving_the_inner_clock() {
        var inner = new ManualTimeProvider();
        var clock = new ScaledTimeProvider(inner: inner);
        var start = clock.GetUtcNow();
        var innerBefore = inner.GetUtcNow();

        clock.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), clock.GetUtcNow() - start);
        Assert.Equal(innerBefore, inner.GetUtcNow());
    }

    [Fact]
    public void Non_positive_scales_are_clamped_to_the_minimum() {
        var inner = new ManualTimeProvider();
        var clock = new ScaledTimeProvider(initialScale: 0, inner: inner);

        Assert.Equal(ScaledTimeProvider.MinScale, clock.Scale);

        clock.SetScale(-5);
        Assert.Equal(ScaledTimeProvider.MinScale, clock.Scale);
    }

    private sealed class ManualTimeProvider : TimeProvider {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) {
            _utcNow += amount;
        }
    }
}
