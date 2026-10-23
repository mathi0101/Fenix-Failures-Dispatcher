namespace RealFenixFailures.Domain.Services;

/// <summary>
/// Reloj de simulación escalable: su <see cref="TimeProvider.GetUtcNow"/> avanza a ×<see cref="Scale"/>
/// respecto del reloj interno. Un solo instance compartido por mocks (SimConnect/Fenix) y motor hace que
/// TODO el mundo simulado (vuelo, cadencia, desgaste, tregua) acelere a la vez.
/// <para>
/// <see cref="SetScale"/> conserva el tiempo de escenario acumulado (sin salto: si el vuelo iba en T+12min,
/// sigue en T+12min pero ahora corre más rápido). <see cref="Advance"/> salta el escenario hacia adelante
/// (p. ej. ir directo al descenso). Con escala 1 el reloj es el wall-clock del <c>inner</c>.
/// </para>
/// </summary>
public sealed class ScaledTimeProvider : TimeProvider {
    public const double MinScale = 0.1;

    private readonly object _lock = new();
    private readonly TimeProvider _inner;
    private DateTimeOffset _anchorInner;
    private DateTimeOffset _anchorScaled;
    private double _scale;

    public ScaledTimeProvider(double initialScale = 1.0, TimeProvider? inner = null) {
        _inner = inner ?? TimeProvider.System;
        _scale = ClampScale(initialScale);
        _anchorInner = _inner.GetUtcNow();
        _anchorScaled = _anchorInner;
    }

    /// <summary>Multiplicador vigente: 1 = tiempo real.</summary>
    public double Scale {
        get {
            lock (_lock) return _scale;
        }
    }

    /// <summary>Cambia el multiplicador sin alterar el tiempo de escenario ya transcurrido.</summary>
    public void SetScale(double scale) {
        lock (_lock) {
            var innerNow = _inner.GetUtcNow();
            _anchorScaled += (innerNow - _anchorInner) * _scale;
            _anchorInner = innerNow;
            _scale = ClampScale(scale);
        }
    }

    /// <summary>Salta el tiempo de escenario hacia adelante (el tiempo interno no se mueve).</summary>
    public void Advance(TimeSpan scenarioDelta) {
        if (scenarioDelta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(scenarioDelta), "El salto de escenario no puede ser negativo.");

        lock (_lock) {
            _anchorScaled += scenarioDelta;
        }
    }

    public override DateTimeOffset GetUtcNow() {
        lock (_lock) {
            var scaledElapsed = (_inner.GetUtcNow() - _anchorInner) * _scale;
            return _anchorScaled + scaledElapsed;
        }
    }

    private static double ClampScale(double scale) =>
        double.IsFinite(scale) ? Math.Max(scale, MinScale) : 1.0;
}
