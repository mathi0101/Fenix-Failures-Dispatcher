using System.Runtime.CompilerServices;

namespace RealFenixFailures.UI.Diagnostics;

public sealed record CrashReport(Exception Exception, string Message, bool IsFatal, string? LogPath, string Details);

/// <summary>Serializa diálogos, evita reportar dos veces la misma excepción y conserva escaladas a fatal.</summary>
public sealed class CrashReportCoordinator(
    Func<Exception, string, bool, CrashReport> createReport,
    Action<CrashReport> present,
    Action shutdown,
    Action<Action> schedule) {
    private sealed class Incident(CrashReport report) { public CrashReport Report = report; public bool Completed; }
    private readonly object _sync = new();
    private readonly ConditionalWeakTable<Exception, Incident> _seen = new();
    private readonly Queue<Incident> _pending = new();
    private bool _scheduled;
    private bool _draining;
    private bool _shuttingDown;

    public void Report(Exception exception, string message, bool fatal) {
        bool shouldSchedule;
        var key = exception.GetBaseException();
        lock (_sync) {
            if (_shuttingDown) return;
            if (_seen.TryGetValue(key, out var previous)) {
                if (!fatal || previous.Report.IsFatal) return;
                previous.Report = Create(exception, message, true);
                // Si ya se mostró como recuperable, hace falta presentar la escalada a fatal.
                if (previous.Completed) { previous.Completed = false; _pending.Enqueue(previous); }
            } else {
                var incident = new Incident(Create(exception, message, fatal));
                _seen.Add(key, incident);
                _pending.Enqueue(incident);
            }
            shouldSchedule = !_scheduled;
            _scheduled = true;
        }
        if (shouldSchedule) {
            try { schedule(DrainPending); }
            catch { DrainPending(); }
        }
    }

    private CrashReport Create(Exception exception, string message, bool fatal) {
        try { return createReport(exception, message, fatal); }
        catch { return new CrashReport(exception, message, fatal, null, exception.ToString()); }
    }

    public void DrainPending() {
        lock (_sync) {
            if (_draining || _shuttingDown) return;
            _draining = true;
        }
        while (true) {
            Incident incident;
            lock (_sync) {
                if (_pending.Count == 0) { _scheduled = false; _draining = false; return; }
                incident = _pending.Dequeue();
            }
            var displayed = incident.Report;
            bool fatal;
            while (true) {
                try { present(displayed); }
                catch { /* El presentador tiene su propio fallback. */ }
                lock (_sync) {
                    // La escalada puede llegar mientras ShowDialog bombea mensajes.
                    if (!displayed.IsFatal && incident.Report.IsFatal) {
                        displayed = incident.Report;
                        continue;
                    }
                    fatal = displayed.IsFatal;
                    incident.Completed = true;
                    if (fatal) { _shuttingDown = true; _pending.Clear(); _draining = false; }
                    break;
                }
            }
            if (fatal) {
                try { shutdown(); } catch { }
                return;
            }
        }
    }
}
