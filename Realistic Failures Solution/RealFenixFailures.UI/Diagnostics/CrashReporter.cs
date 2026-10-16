using System.Windows.Threading;

namespace RealFenixFailures.UI.Diagnostics;

/// <summary>
/// Manejo global de excepciones no controladas a nivel de aplicación: registra
/// un crash log en <c>logs/crash</c> y muestra un cartel de error general.
/// </summary>
public static class CrashReporter {
    private static bool _isHandlingFatal;

    /// <summary>
    /// Conecta los handlers globales de excepciones no controladas. Debe
    /// llamarse lo antes posible en el arranque (primer paso de OnStartup).
    /// </summary>
    public static void AttachGlobalHandlers(System.Windows.Application app) {
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Registra la excepción, muestra el cartel de error general y, si se pide,
    /// cierra la aplicación después de que el usuario cierre el cartel.
    /// No lanza: cualquier fallo interno queda acotado al propio reporte.
    /// </summary>
    public static void ReportFatal(Exception exception, string message, bool shutdown = false) {
        try {
            string? logPath = null;
            try {
                logPath = CrashLogWriter.Write(exception, message);
            }
            catch {
                // El registro no debe enmascarar el manejo del error original.
            }

            if (!_isHandlingFatal) {
                _isHandlingFatal = true;
                try {
                    FatalErrorDialog.ShowGeneral(message, logPath, exception);
                }
                finally {
                    _isHandlingFatal = false;
                }
            }
            // Si ya hay un cartel en pantalla (reentrada desde el handler del
            // dispatcher), no se encadenan diálogos: el log ya quedó escrito.
        }
        catch {
            // El reporte nunca debe enmascarar el error original.
        }

        if (shutdown)
            TryShutdown();
    }

    /// <summary>
    /// Excepción no controlada en el hilo de interfaz (incluye errores en
    /// handlers async void del dispatcher). Es fatal para el flujo de la app:
    /// registra, muestra el cartel y cierra la aplicación de forma controlada.
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        e.Handled = true;
        ReportFatal(
            e.Exception,
            "Ocurrió un error inesperado y la aplicación debe cerrarse. Se generó un registro de crash.",
            shutdown: true);
    }

    /// <summary>
    /// Excepción no controlada fatal en cualquier hilo (AppDomain). El proceso
    /// termina igual al retornar el handler; se registra el crash y se intenta
    /// mostrar el cartel aunque sea en un hilo STA dedicado.
    /// </summary>
    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        if (e.ExceptionObject is not Exception exception)
            return;

        ReportFatal(
            exception,
            "Error fatal no controlado. La aplicación debe cerrarse. Se generó un registro de crash.",
            shutdown: false);
    }

    /// <summary>
    /// Excepción de tarea sin observador (fire-and-forget). NO es fatal: la app
    /// sigue corriendo, por eso solo se registra en el crash log sin cartel.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        e.SetObserved();
        try {
            CrashLogWriter.Write(
                e.Exception,
                "Excepción de tarea sin observador (fire-and-forget). No fatal: la app continúa.");
        }
        catch {
            // Ídem ReportFatal: un fallo de log no debe propagarse aquí.
        }
    }

    private static void TryShutdown() {
        try {
            System.Windows.Application.Current?.Shutdown();
        }
        catch {
            // Si el shutdown falla el proceso se detendrá igual al salir de Main.
        }
    }
}
