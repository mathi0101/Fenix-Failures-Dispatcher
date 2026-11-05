using RealFenixFailures.UI.Localization;
using System.Windows;
using System.Windows.Forms;

namespace RealFenixFailures.UI.Services;

/// <summary>
/// Administra el icono de la bandeja del sistema mientras la app corre en
/// segundo plano: permite restaurar la ventana principal o cerrar la app
/// definitivamente desde su menú contextual. El menú se reconstruye al cambiar
/// el idioma (issue #12: la bandeja también se traduce en vivo).
/// </summary>
public sealed class TrayIconService : IDisposable {
    private readonly Window _mainWindow;
    private readonly Action _onExitRequested;
    private readonly NotifyIcon _notifyIcon;
    private bool _balloonShown;

    public TrayIconService(Window mainWindow, Action onExitRequested) {
        _mainWindow = mainWindow;
        _onExitRequested = onExitRequested;

        _notifyIcon = new NotifyIcon {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();

        RebuildContextMenu();
        Loc.Instance.LanguageChanged += RebuildContextMenu;
    }

    /// <summary>Construye el menú contextual con los textos del idioma activo.</summary>
    private void RebuildContextMenu() {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(Loc.T("Tray_Open"), null, (_, _) => RestoreMainWindow());
        contextMenu.Items.Add(Loc.T("Tray_Exit"), null, (_, _) => _onExitRequested());
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.Text = Loc.T("Tray_TipTitle");
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
            tipText: Loc.T("Tray_TipText"),
            tipIcon: ToolTipIcon.Info);
    }

    public void Dispose() {
        Loc.Instance.LanguageChanged -= RebuildContextMenu;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
