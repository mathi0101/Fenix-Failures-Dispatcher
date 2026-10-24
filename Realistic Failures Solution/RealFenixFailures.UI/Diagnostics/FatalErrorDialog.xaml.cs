using System.Diagnostics;
using System.Windows;

namespace RealFenixFailures.UI.Diagnostics;

/// <summary>
/// Cartel de error general que se muestra ante una excepción no controlada o
/// un fallo de arranque. Es autocontenido (no depende de los recursos de la
/// aplicación) para poder mostrarse incluso cuando la app no llegó a iniciar.
/// </summary>
public partial class FatalErrorDialog : Window {
    private readonly string? _logPath;

    public FatalErrorDialog(CrashReport report) {
        InitializeComponent();

        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 60);
        MessageText.Text = report.Message;
        BehaviorText.Text = report.IsFatal
            ? "La aplicación se cerrará al confirmar o cerrar esta ventana."
            : "Podés continuar usando la aplicación al aceptar. Revisá el estado de la operación que falló.";
        ConfirmButton.Content = report.IsFatal ? "Cerrar aplicación" : "Aceptar";
        SeverityBar.Fill = new System.Windows.Media.SolidColorBrush(report.IsFatal
            ? System.Windows.Media.Color.FromRgb(239, 68, 68) : System.Windows.Media.Color.FromRgb(245, 158, 11));
        var logPath = report.LogPath;

        if (string.IsNullOrEmpty(logPath)) {
            LogInfoText.Text = "No se pudo guardar el archivo de diagnóstico. El detalle técnico sigue disponible abajo.";
            OpenLogButton.IsEnabled = false;
        }
        else {
            LogInfoText.Text =
                $"Se generó un registro de crash en:{Environment.NewLine}{logPath}";
            _logPath = logPath;
        }

        DetailsBox.Text = report.Details;
    }

    /// <summary>
    /// Muestra el cartel y bloquea hasta que el usuario lo cierra.
    /// Si la llamada no viene del hilo de interfaz (o no hay Application,
    /// por ejemplo un crash de arranque previo al host WPF), lo muestra en un
    /// hilo STA dedicado para que el cartel aparezca igualmente.
    /// </summary>
    public static void ShowGeneral(CrashReport report) {
        bool onUiThread = System.Windows.Application.Current is not null &&
                          System.Windows.Application.Current.Dispatcher.Thread == Thread.CurrentThread;
        if (onUiThread) {
            var dialog = new FatalErrorDialog(report);
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner && owner != dialog) {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            dialog.ShowDialog();
            return;
        }

        Exception? dialogError = null;
        var thread = new Thread(() => {
            try { new FatalErrorDialog(report).ShowDialog(); }
            catch (Exception ex) { dialogError = ex; }
        }) {
            IsBackground = false,
            Name = "FatalErrorDialog"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (dialogError is not null) throw new InvalidOperationException("No se pudo mostrar el diálogo de diagnóstico", dialogError);
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrEmpty(_logPath))
            return;

        try {
            Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
        }
        catch (Exception ex) {
            LogInfoText.Text = $"No se pudo abrir el registro: {ex.Message}{Environment.NewLine}{_logPath}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
