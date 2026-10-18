using System.Windows;

namespace RealFenixFailures.UI.Views;

/// <summary>
/// Modal que se muestra al intentar cerrar la ventana principal: pregunta si
/// se quiere cerrar la app por completo o dejarla corriendo en segundo plano.
/// </summary>
public partial class CloseChoiceWindow : Window {
    public enum CloseChoice {
        Cancel,
        Background,
        Shutdown,
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    public CloseChoiceWindow() {
        InitializeComponent();
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
