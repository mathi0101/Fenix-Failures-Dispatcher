using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using Serilog.Core;
using Serilog.Events;

namespace RealFenixFailures.UI.Diagnostics;

/// <summary>Única frontera entre los logs de todas las capas y la ventana de errores de WPF.</summary>
public sealed class UnexpectedErrorSink(Action<Exception, string, bool> report) : ILogEventSink {
    public void Emit(LogEvent logEvent) {
        if (logEvent.Level < LogEventLevel.Warning) return;
        var source = logEvent.Properties.TryGetValue("SourceContext", out var value)
            ? (value as ScalarValue)?.Value as string : null;
        var fatal = logEvent.Level == LogEventLevel.Fatal;
        if (!fatal && source is not null && !source.StartsWith("RealFenixFailures.", StringComparison.Ordinal)) return;
        var exception = logEvent.Exception;
        if (!fatal && (exception is null || IsExpected(exception, source))) return;
        var context = $"{source ?? "Aplicación"}: {logEvent.RenderMessage()}";
        exception ??= new InvalidOperationException(context);
        try { report(exception, context, fatal); }
        catch { /* El reporter no puede romper el logger ni disparar recursión. */ }
    }

    private static bool IsExpected(Exception exception, string? source) {
        if (exception is AggregateException aggregate)
            return aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(e => IsExpected(e, source));
        if (exception is OperationCanceledException or HttpRequestException or TimeoutException or WebException
            or System.Net.Sockets.SocketException) return true;
        // E_FAIL es el estado «simulador no disponible» que ya reconoce SimConnectClient.
        return exception is COMException { HResult: unchecked((int)0x80004005) } && source is not null
            && (source.Contains("SimConnect", StringComparison.Ordinal) || source.EndsWith("SimulatorConnectionService", StringComparison.Ordinal));
    }
}
