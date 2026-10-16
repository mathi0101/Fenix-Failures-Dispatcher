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

    private FatalErrorDialog(string message, string? logPath, string report) {
        InitializeComponent();

        MessageText.Text = message;

        if (string.IsNullOrEmpty(logPath)) {
            LogInfoBorder.Visibility = Visibility.Collapsed;
            OpenLogButton.IsEnabled = false;
        }
        else {
            LogInfoText.Text =
                $"Se generó un registro de crash en:{Environment.NewLine}{logPath}";
            _logPath = logPath;
        }

        DetailsBox.Text = report;
    }

    /// <summary>
    /// Muestra el cartel y bloquea hasta que el usuario lo cierra.
    /// Si la llamada no viene del hilo de interfaz (o no hay Application,
    /// por ejemplo un crash de arranque previo al host WPF), lo muestra en un
    /// hilo STA dedicado para que el cartel aparezca igualmente.
    /// </summary>
    public static void ShowGeneral(string message, string? logPath, Exception exception) {
        var report = CrashLogWriter.BuildReport(exception, message);

        bool onUiThread = System.Windows.Application.Current is not null &&
                          System.Windows.Application.Current.Dispatcher.Thread == Thread.CurrentThread;
        if (onUiThread) {
            new FatalErrorDialog(message, logPath, report).ShowDialog();
            return;
        }

        var thread = new Thread(() => {
            var dialog = new FatalErrorDialog(message, logPath, report);
            dialog.ShowDialog();
        }) {
            IsBackground = false,
            Name = "FatalErrorDialog"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrEmpty(_logPath))
            return;

        try {
            Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
        }
        catch (Exception ex) {
            MessageBox.Show(
                $"No se pudo abrir el registro: {ex.Message}{Environment.NewLine}{_logPath}",
                "Registro de crash",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
