using RealFenixFailures.UI.Diagnostics;
using Serilog;
using Serilog.Events;

namespace RealFenixFailures.TestUnits.Diagnostics;

public class CrashReportingTests {
    private static CrashReport Create(Exception e, string context, bool fatal) => new(e, context, fatal, "report.log", e.ToString());

    [Fact]
    public void RecoverableError_LogsBeforeDialog_AndAllowsContinuing() {
        var actions = new Queue<Action>();
        var steps = new List<string>();
        var coordinator = new CrashReportCoordinator((e, c, f) => { steps.Add("log"); return Create(e, c, f); },
            _ => steps.Add("dialog"), () => steps.Add("shutdown"), actions.Enqueue);
        coordinator.Report(new InvalidOperationException("db"), "Guardar", false);
        Assert.Equal(["log"], steps);
        actions.Dequeue()();
        Assert.Equal(["log", "dialog"], steps);
        coordinator.Report(new Exception("second"), "Otra operación", false);
        actions.Dequeue()();
        Assert.Equal(2, steps.Count(s => s == "dialog"));
    }

    [Fact]
    public void SameErrorAcrossLayers_EscalatesToFatalBeforeDisplaying_AndShutsDownAfterDialog() {
        var actions = new Queue<Action>();
        var steps = new List<string>();
        var coordinator = new CrashReportCoordinator(Create, r => { Assert.True(r.IsFatal); steps.Add("dialog"); },
            () => steps.Add("shutdown"), actions.Enqueue);
        var error = new InvalidOperationException("db");
        coordinator.Report(error, "Primera capa", false);
        coordinator.Report(error, "Duplicado", false);
        coordinator.Report(new Exception("Otra capa", error), "Fatal", true);
        Assert.Single(actions);
        Assert.Empty(steps);
        actions.Dequeue()();
        Assert.Equal(["dialog", "shutdown"], steps);
    }

    [Fact]
    public void ReentrantError_DoesNotNestDialogs_AndIsNotLost() {
        CrashReportCoordinator coordinator = null!;
        int count = 0, depth = 0;
        coordinator = new CrashReportCoordinator(Create, _ => {
            Assert.Equal(1, ++depth);
            if (++count == 1) coordinator.Report(new Exception("Segundo"), "Otro error", false);
            depth--;
        }, () => Assert.Fail("No debe cerrar"), action => action());
        coordinator.Report(new Exception("Primero"), "Error", false);
        Assert.Equal(2, count);
    }

    [Fact]
    public void FailureToWriteOrSchedule_DoesNotHideFatalError() {
        var steps = new List<string>();
        var coordinator = new CrashReportCoordinator((_, _, _) => throw new IOException(),
            r => { Assert.Null(r.LogPath); steps.Add("dialog"); }, () => steps.Add("shutdown"), _ => throw new Exception());
        coordinator.Report(new Exception("Original"), "Error", true);
        Assert.Equal(["dialog", "shutdown"], steps);
    }

    [Fact]
    public void EscalationOfPreviouslyShownError_WhileAnotherDialogIsOpen_IsQueued() {
        var first = new Exception("first");
        var shown = new List<string>();
        CrashReportCoordinator coordinator = null!;
        coordinator = new CrashReportCoordinator(Create, r => {
            shown.Add(r.Message);
            if (r.Message == "second") coordinator.Report(first, "fatal", true);
        }, () => shown.Add("shutdown"), action => action());
        coordinator.Report(first, "first", false);
        coordinator.Report(new Exception("second"), "second", false);
        Assert.Equal(["first", "second", "fatal", "shutdown"], shown);
    }

    [Fact]
    public void Sink_ReportsUnexpectedWarningsAndErrors_ButNotExpectedDisconnectsOrCancellation() {
        var reports = new List<(Exception, bool)>();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose()
            .WriteTo.Sink(new UnexpectedErrorSink((e, _, f) => reports.Add((e, f)))).CreateLogger();
        var appLogger = logger.ForContext("SourceContext", "RealFenixFailures.Application.Test");
        appLogger.Warning(new IOException("disk"), "Warning inesperado");
        appLogger.Error(new NullReferenceException(), "Bug");
        appLogger.Warning(new HttpRequestException(), "Fenix desconectado");
        appLogger.Error(new TaskCanceledException(), "Stop");
        appLogger.Warning("Rechazo esperado sin excepción");
        logger.ForContext("SourceContext", "Microsoft.EntityFrameworkCore").Error(new Exception(), "Reportado por el servicio");
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.False(r.Item2));
        var simLogger = logger.ForContext("SourceContext", "RealFenixFailures.Integrations.SimConnect.Client");
        simLogger.Error(new System.Runtime.InteropServices.COMException("No disponible", unchecked((int)0x80004005)), "Esperado");
        Assert.Equal(2, reports.Count);
        simLogger.Error(new System.Runtime.InteropServices.COMException("Otro error", -1), "Inesperado");
        Assert.Equal(3, reports.Count);
        appLogger.Fatal(new HttpRequestException(), "Compensación imposible");
        appLogger.Fatal("Estado inconsistente sin excepción original");
        logger.ForContext("SourceContext", "Microsoft.Extensions.Hosting").Fatal(new Exception(), "Host fatal");
        Assert.Equal(6, reports.Count);
        Assert.True(reports[3].Item2);
        Assert.True(reports[4].Item2);
        Assert.True(reports[5].Item2);
    }

    [Fact]
    public void EscalationWhileDialogIsOpen_ShowsFatalBeforeShutdown() {
        var shown = new List<string>();
        var error = new Exception("first");
        CrashReportCoordinator coordinator = null!;
        coordinator = new CrashReportCoordinator(Create, r => {
            shown.Add(r.IsFatal ? "fatal" : "recoverable");
            if (!r.IsFatal) coordinator.Report(error, "Escalada", true);
        }, () => shown.Add("shutdown"), action => action());
        coordinator.Report(error, "Error", false);
        Assert.Equal(["recoverable", "fatal", "shutdown"], shown);
    }

    [Fact]
    public void ParallelReportsOfSameIncident_AreWrittenAndShownOnce() {
        var actions = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var error = new Exception("shared");
        int writes = 0, dialogs = 0;
        var coordinator = new CrashReportCoordinator((e, c, f) => { Interlocked.Increment(ref writes); return Create(e, c, f); },
            _ => dialogs++, () => Assert.Fail("No debe cerrar"), actions.Enqueue);
        Parallel.For(0, 30, _ => coordinator.Report(error, "Mismo incidente", false));
        Assert.True(actions.TryDequeue(out var drain));
        drain();
        Assert.Equal(1, writes);
        Assert.Equal(1, dialogs);
        Assert.Empty(actions);
    }

    [Fact]
    public void LoggingPipeline_PreservesOriginalExceptionAndFatalEscalation() {
        var actions = new Queue<Action>();
        var shown = new List<CrashReport>();
        bool stopped = false;
        var coordinator = new CrashReportCoordinator(Create, shown.Add, () => stopped = true, actions.Enqueue);
        using var logger = new LoggerConfiguration().WriteTo.Sink(new UnexpectedErrorSink(coordinator.Report)).CreateLogger();
        var error = new InvalidOperationException("db");
        var appLogger = logger.ForContext("SourceContext", "RealFenixFailures.Application.Engine");
        appLogger.Warning(error, "No se pudo persistir");
        appLogger.Fatal(error, "No se pudo compensar");
        actions.Dequeue()();
        Assert.True(stopped);
        Assert.Same(error, Assert.Single(shown).Exception);
        Assert.True(shown[0].IsFatal);
    }

    [Fact]
    public void IndividualLogs_AreUnique_AndContainUtcSeverityAndInnerExceptions() {
        var directory = Path.Combine(Path.GetTempPath(), "RffCrashTests", Guid.NewGuid().ToString("N"));
        try {
            var error = new InvalidOperationException("outer", new IOException("inner"));
            var report = CrashLogWriter.BuildReport(error, "Guardar sesión", false);
            var first = CrashLogWriter.WriteReport(report, false, directory)!;
            var second = CrashLogWriter.WriteReport(report, false, directory)!;
            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.Equal(directory, Path.GetDirectoryName(first));
            var written = File.ReadAllText(first);
            Assert.Contains("ERROR — permite continuar", written);
            Assert.Contains("Fecha (UTC)", written);
            Assert.Contains("outer", written);
            Assert.Contains("inner", written);
        } finally {
            var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RffCrashTests")) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(directory);
            Assert.StartsWith(allowedRoot, target, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
    }
}
