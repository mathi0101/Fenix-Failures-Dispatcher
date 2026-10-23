using Serilog.Core;
using Serilog.Events;

namespace RealFenixFailures.Infrastructure.Logging;

/// <summary>
/// Agrega la propiedad <c>UtcTimestamp</c> a cada evento de log: la marca de tiempo
/// del evento en UTC. Las plantillas de salida de los sinks de archivo la usan
/// («{UtcTimestamp:...}») para que todos los logs queden en hora Zulu (todo en UTC).
/// </summary>
public sealed class UtcTimestampEnricher : ILogEventEnricher {
    public const string PropertyName = "UtcTimestamp";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            PropertyName, logEvent.Timestamp.UtcDateTime));
    }
}
