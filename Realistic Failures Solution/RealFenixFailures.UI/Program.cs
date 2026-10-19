using System.Threading;
using System.Windows;
using System.Windows.Threading;
using RealFenixFailures.UI.Diagnostics;
using RealFenixFailures.UI.Views;

namespace RealFenixFailures.UI;

/// <summary>
/// Punto de entrada principal. Se define manualmente (ver StartupObject en el
/// .csproj) para poder mostrar el splash de carga antes de crear la aplicación
/// y capturar fallos fatales de arranque que de otra forma terminarían el
/// proceso en silencio sin dejar rastro.
/// </summary>
public static class Program {
    private static SplashWindow? _splash;
    private static Dispatcher? _splashDispatcher;

    /// <summary>
    /// Se pone en true cuando el arranque ya fracasó (fallo en el constructor
    /// de App). WPF puede invocar OnStartup igualmente desde el pump del
    /// diálogo de error; con este flag OnStartup no ejecuta nada y se evita un
    /// segundo reporte de crash con _host sin inicializar.
    /// </summary>
    public static bool StartupFailed { get; set; }

    [STAThread]
    public static void Main() {
        // El splash corre en un hilo STA dedicado con su propio Dispatcher:
        // mientras el hilo principal construye el host y arranca los servicios,
        // el pump del hilo del splash sigue libre y la animación de frames no se
        // congela (un DispatcherTimer del propio hilo principal no llega a
        // dispararse durante el arranque). La ventana lleva Cursor="Arrow", así
        // que el cursor se ve normal sobre el splash (sin IDC_APPSTARTING).
        StartSplash();

        try {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception exception) {
            // Fallo antes de que la aplicación quedara operativa (por ejemplo,
            // durante la construcción del host o al cargar recursos de App.xaml).
            StartupFailed = true;
            CloseSplash();
            CrashReporter.ReportFatal(
                exception,
                "La aplicación no pudo iniciarse correctamente. Se generó un registro de crash en la carpeta de logs.");
        }
    }

    /// <summary>
    /// Lanza el hilo STA del splash y su pump de mensajes.
    /// </summary>
    private static void StartSplash() {
        var splashThread = new Thread(() => {
            _splash = new SplashWindow();
            _splashDispatcher = Dispatcher.CurrentDispatcher;
            _splash.Show();
            Dispatcher.Run();
        }) {
            Name = "SplashThread",
            IsBackground = true,
        };
        splashThread.SetApartmentState(ApartmentState.STA);
        splashThread.Start();
    }

    /// <summary>
    /// Cierra el splash si sigue visible, en el hilo que le corresponde. En el
    /// arranque normal lo hace App.OnStartup al mostrar la ventana principal;
    /// este cierre explícito cubre arranques fallidos y la rama de instancia
    /// única.
    /// </summary>
    public static void CloseSplash() {
        try {
            var dispatcher = _splashDispatcher;
            if (dispatcher == null)
                return;

            dispatcher.BeginInvoke(() => {
                _splash?.Close();
                _splash = null;
                _splashDispatcher = null;
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            });
        }
        catch {
            // Un fallo del splash no debe enmascarar el motivo del cierre.
        }
    }
}
