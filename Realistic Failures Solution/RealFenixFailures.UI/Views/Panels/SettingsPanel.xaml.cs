using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views.Panels;

/// <summary>
/// Panel de configuración embebido en MainWindow (overlay centrado).
/// No es una ventana independiente: pide cierre con el evento CloseRequested
/// (botón X o tecla ESC) y el host decide cómo responder.
/// </summary>
public partial class SettingsPanel : UserControl {

    public event EventHandler? CloseRequested;

    public SettingsPanel() {
        InitializeComponent();
        PreviewKeyDown += SettingsPanel_PreviewKeyDown;
    }

    private void SettingsPanel_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            e.Handled = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClosePanel(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Activa la pestaña Actualizaciones (acceso directo desde la status bar).</summary>
    public void ShowUpdatesTab() => SettingsTabs.SelectedItem = UpdatesTab;
}
