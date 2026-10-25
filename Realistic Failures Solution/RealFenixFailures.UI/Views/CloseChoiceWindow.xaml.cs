using System.Windows;

namespace RealFenixFailures.UI.Views;

/// <summary>
/// Modal que se muestra al intentar cerrar la ventana principal: pregunta si
/// se quiere cerrar la app por completo o dejarla corriendo en segundo plano.
/// Con <c>allowFullExit: false</c> (hay una sesión en curso) el cierre completo
/// viene bloqueado y solo se permite mandar la app a segundo plano.
/// </summary>
public partial class CloseChoiceWindow : Window {
    public enum CloseChoice {
        Cancel,
        Background,
        Shutdown,
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    public CloseChoiceWindow(bool allowFullExit = true) {
        InitializeComponent();
        if (!allowFullExit) {
            ShutdownButton.IsEnabled = false;
            ShutdownButton.ToolTip = "El cierre completo está bloqueado mientras el modo Realistic tiene una sesión activa.";
            ShutdownLockIcon.Visibility = Visibility.Visible;
            ShutdownSubtitle.Text = "Bloqueado: hay una sesión en curso. Para salir de la app, terminá la sesión primero.";
            ShutdownSubtitle.FontWeight = FontWeights.SemiBold;
            ShutdownSubtitle.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5B741"));
        }
    }

    private void Background_Click(object sender, RoutedEventArgs e) {
        Choice = CloseChoice.Background;
        DialogResult = true;
    }

    private void Shutdown_Click(object sender, RoutedEventArgs e) {
        Choice = CloseChoice.Shutdown;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
