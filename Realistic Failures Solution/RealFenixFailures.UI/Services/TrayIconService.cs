using System.Windows;
using System.Windows.Forms;

namespace RealFenixFailures.UI.Services;

/// <summary>
/// Administra el icono de la bandeja del sistema mientras la app corre en
/// segundo plano: permite restaurar la ventana principal o cerrar la app
/// definitivamente desde su menú contextual.
/// </summary>
public sealed class TrayIconService : IDisposable {
    private readonly Window _mainWindow;
    private readonly Action _onExitRequested;
    private readonly NotifyIcon _notifyIcon;
    private bool _balloonShown;

    public TrayIconService(Window mainWindow, Action onExitRequested) {
        _mainWindow = mainWindow;
        _onExitRequested = onExitRequested;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Abrir", null, (_, _) => RestoreMainWindow());
        contextMenu.Items.Add("Salir", null, (_, _) => _onExitRequested());

        _notifyIcon = new NotifyIcon {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "Realistic Failures - en segundo plano",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    public void RestoreMainWindow() {
        _mainWindow.Dispatcher.Invoke(() => {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    public void NotifyMinimizedToTray() {
        if (_balloonShown)
            return;

        _balloonShown = true;
        _notifyIcon.ShowBalloonTip(
            timeout: 3000,
            tipTitle: "Realistic Failures",
            tipText: "La app sigue ejecutándose en segundo plano. Para cerrarla del todo, usá \"Salir\" desde este icono.",
            tipIcon: ToolTipIcon.Info);
    }

    public void Dispose() {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
