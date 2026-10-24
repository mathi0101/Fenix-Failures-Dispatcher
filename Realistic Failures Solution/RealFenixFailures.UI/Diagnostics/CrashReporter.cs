using System.Windows.Threading;
using RealFenixFailures.UI.Views;

namespace RealFenixFailures.UI.Diagnostics;

/// <summary>Reporte único para errores inesperados: archivo individual, diálogo y cierre solo si es fatal.</summary>
public static class CrashReporter {
    private static int _handlersAttached;
    private static int _processHandlersAttached;
    private static readonly CrashReportCoordinator Coordinator = new(CreateReport, ShowReport, TryShutdown, Schedule);

    public static void AttachGlobalHandlers(System.Windows.Application app) {
        if (Interlocked.Exchange(ref _handlersAttached, 1) != 0) return;
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AttachProcessHandlers();
    }

    public static void AttachProcessHandlers() {
        if (Interlocked.Exchange(ref _processHandlersAttached, 1) != 0) return;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>Usado por el sink; encola el diálogo para permitir que el catch/finally termine primero.</summary>
    public static void ReportUnexpected(Exception exception, string message, bool fatal = false) =>
        Coordinator.Report(exception, message, fatal);

    public static void ReportFatal(Exception exception, string message) {
        Coordinator.Report(exception, message, true);
        Coordinator.DrainPending();
    }

    private static CrashReport CreateReport(Exception exception, string message, bool fatal) {
        var details = CrashLogWriter.BuildReport(exception, message, fatal);
        return new CrashReport(exception, message, fatal, CrashLogWriter.WriteReport(details, fatal), details);
    }

    private static void Schedule(Action action) {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
        else action();
    }

    private static void ShowReport(CrashReport report) {
        try { FatalErrorDialog.ShowGeneral(report); }
        catch {
            // Último recurso: ni los recursos ni un fallo del propio diálogo pueden ocultar el incidente.
            try {
                System.Windows.MessageBox.Show(report.Message + Environment.NewLine +
                    (report.IsFatal ? "La aplicación se cerrará." : "Podés continuar usando la aplicación.") +
                    Environment.NewLine + (report.LogPath ?? report.Details),
                    "Real Fenix Failures — Error inesperado", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            } catch { }
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        e.Handled = true;
        ReportFatal(e.Exception, "Ocurrió un error no controlado en la interfaz. La aplicación debe cerrarse.");
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        if (e.ExceptionObject is Exception exception) {
            // El CLR termina al retornar: no se puede depender de una cola del dispatcher
            // ni bloquearlo esperando un diálogo que ya esté abierto. Presentación síncrona/STA.
            const string context = "Error fatal no controlado. La aplicación debe cerrarse.";
            CrashReport report;
            try { report = CreateReport(exception, context, true); }
            catch { report = new CrashReport(exception, context, true, null, exception.ToString()); }
            ShowReport(report);
            TryShutdown();
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        e.SetObserved();
        ReportUnexpected(e.Exception, "Ocurrió un error inesperado en una tarea en segundo plano.");
    }

    private static void TryShutdown() {
        try {
            var app = System.Windows.Application.Current;
            if (app is null) return;
            void Exit() {
                foreach (var window in app.Windows.OfType<MainWindow>().ToArray()) window.IsExitAllowed = true;
                app.Shutdown(1);
            }
            if (app.Dispatcher.CheckAccess()) Exit();
            else if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(Exit);
        } catch { /* En errores AppDomain el runtime termina el proceso igualmente. */ }
    }
}
