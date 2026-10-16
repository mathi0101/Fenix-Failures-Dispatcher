using System.Windows;
using RealFenixFailures.UI.Diagnostics;

namespace RealFenixFailures.UI;

/// <summary>
/// Punto de entrada principal. Se define manualmente (ver StartupObject en el
/// .csproj) para poder mostrar el splash de carga antes de crear la aplicación
/// y capturar fallos fatales de arranque que de otra forma terminarían el
/// proceso en silencio sin dejar rastro.
/// </summary>
public static class Program {
    private static SplashScreen? _splash;

    /// <summary>
    /// Se pone en true cuando el arranque ya fracasó (fallo en el constructor
    /// de App). WPF puede invocar OnStartup igualmente desde el pump del
    /// diálogo de error; con este flag OnStartup no ejecuta nada y se evita un
    /// segundo reporte de crash con _host sin inicializar.
    /// </summary>
    public static bool StartupFailed { get; set; }

    [STAThread]
    public static void Main() {
        _splash = new SplashScreen("Assets/Branding/splash-screen.png");
        _splash.Show(true);

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
    /// Cierra el splash si sigue visible. En el arranque normal se cierra solo
    /// al mostrarse la primera ventana (Show(autoClose: true)); este cierre
    /// explícito cubre arranques fallidos y la rama de instancia única.
    /// </summary>
    public static void CloseSplash() {
        try {
            _splash?.Close(TimeSpan.Zero);
        }
        catch {
            // Un fallo del splash no debe enmascarar el motivo del cierre.
        }
        _splash = null;
    }
}
