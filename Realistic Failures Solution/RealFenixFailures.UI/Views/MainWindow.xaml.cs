﻿using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RealFenixFailures.UI.Views;

public partial class MainWindow : Window {

    private readonly MainWindowViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;
    private bool _isCloseDialogOpen;
    private readonly System.Windows.Threading.DispatcherTimer _utcClockTimer;

    /// <summary>
    /// Servicio de bandeja del sistema, inyectado desde <see cref="App.OnStartup"/>.
    /// Se usa para avisar (balloon) cuando la ventana pasa a segundo plano.
    /// </summary>
    public TrayIconService? TrayIcon { get; set; }

    /// <summary>
    /// Cuando es false (default), cerrar la ventana la oculta y la app sigue
    /// corriendo en segundo plano (icono de bandeja). Solo "Salir" desde la
    /// bandeja la pone en true para permitir el cierre real.
    /// </summary>
    public bool IsExitAllowed { get; set; }

    public MainWindow(MainWindowViewModel viewModel, ILogger<MainWindow> logger) {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        DataContext = _viewModel;
        Loaded += async (_, _) => {
            await _viewModel.InitializeAsync();
            await ShowUnfinishedSessionsDialogAsync();
        };
        Closing += MainWindow_Closing;
        // Aviso de fin de vuelo (issue #15): el panel Realista pide restaurar la app sin robar foco.
        _viewModel.Realistic.FlightCompletionPromptRequested += Realistic_FlightCompletionPromptRequested;
        SettingsPanelHost.CloseRequested += (_, _) => _viewModel.IsSettingsOpen = false;

        UpdateUtcClock();
        _utcClockTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _utcClockTimer.Tick += (_, _) => UpdateUtcClock();
        _utcClockTimer.Start();
    }

    /// <summary>Hora UTC real de pared (Zulu), siempre UTC aunque el escenario DEBUG corra escalado.</summary>
    private void UpdateUtcClock() {
        UtcClockText.Text = DateTime.UtcNow.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
    }

    /// <summary>
    /// Recuperación de sesiones sin finalizar (diseño objetivo §6/§13): si al arrancar quedó una sesión
    /// realista abierta, muestra el diálogo con Finalizar / Descartar / Dejar para después y refresca el panel.
    /// </summary>
    private async Task ShowUnfinishedSessionsDialogAsync() {
        await _viewModel.LoadUnfinishedSessionsAsync();
        if (_viewModel.UnfinishedSessions.Count == 0) return;

        var dialog = new UnfinishedSessionsWindow(_viewModel) { Owner = this };
        dialog.ShowDialog();
        await _viewModel.RefreshAfterRecoveryAsync();
    }

    #region Aviso de fin de vuelo (issue #15)

    private void Realistic_FlightCompletionPromptRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(RestoreForCompletionNotice);

    /// <summary>
    /// Hace visible la app sin robar el foco (MSFS puede quedar en VR/fullscreen): restaura desde la
    /// bandeja con <c>ShowActivated=false</c>, sube la ventana al tope del Z-order sin activarla y hace
    /// parpadear el botón de la barra de tareas. Sin el <c>HWND_TOP</c>, Windows muestra la ventana
    /// detrás de la que tenga el foco (un proceso en segundo plano no puede tomar el primer plano).
    /// El modal de confirmación de finalización aparece directo en el panel Realista.
    /// </summary>
    private void RestoreForCompletionNotice() {
        if (IsExitAllowed) return;
        _logger.LogInformation("Aviso de fin de vuelo: restaurando la ventana principal desde segundo plano (sin robar foco).");
        var previousShowActivated = ShowActivated;
        ShowActivated = false;
        try {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            BringToFrontWithoutActivation();
        } finally {
            ShowActivated = previousShowActivated;
        }
        FlashTaskbarButton();
    }

    /// <summary>Sube la ventana al tope del Z-order sin activarla: visible sin robar foco.</summary>
    private void BringToFrontWithoutActivation() {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        _ = SetWindowPos(hwnd, HwndTop, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow);
    }

    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private void FlashTaskbarButton() {
        var info = new FLASHWINFO {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = new WindowInteropHelper(this).EnsureHandle(),
            dwFlags = FLASHW_ALL | FLASHW_NOFG,
            uCount = 3,
            dwTimeout = 0
        };
        _ = FlashWindowEx(ref info);
    }

    private struct FLASHWINFO {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private const uint FLASHW_ALL = 0x3;
    private const uint FLASHW_NOFG = 0xC;

    #endregion

    private void MainWindow_Closing(object? sender, CancelEventArgs e) {
        _viewModel.Realistic.FlightCompletionPromptRequested -= Realistic_FlightCompletionPromptRequested;
        if (IsExitAllowed)
            return;

        e.Cancel = true;

        if (_isCloseDialogOpen)
            return;

        _isCloseDialogOpen = true;
        try {
            var dialog = new CloseChoiceWindow(allowFullExit: !_viewModel.IsAnyModeActive) { Owner = this };
            var accepted = dialog.ShowDialog() == true;
            if (!accepted)
                return;

            switch (dialog.Choice) {
                case CloseChoiceWindow.CloseChoice.Shutdown:
                    // Cierre definitivo: con ShutdownMode=OnExplicitShutdown,
                    // cerrar la ventana no apaga la app (quedaría corriendo en
                    // la bandeja); hace falta el shutdown explícito, que además
                    // cierra la ventana al salir.
                    IsExitAllowed = true;
                    Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
                    break;

                case CloseChoiceWindow.CloseChoice.Background:
                    Hide();
                    TrayIcon?.NotifyMinimizedToTray();
                    break;
            }
        } finally {
            _isCloseDialogOpen = false;
        }
    }

    /// <summary>
    /// Petición de salida completa (desde la bandeja): con una sesión activa solo muestra
    /// el aviso de que hay que terminar la sesión primero; sin sesión activa, habilita el
    /// cierre y apaga la app. Devuelve true si la salida fue aceptada.
    /// </summary>
    public bool TryRequestFullExit() {
        if (_viewModel.IsAnyModeActive) {
            if (_isCloseDialogOpen)
                return false;

            _isCloseDialogOpen = true;
            try {
                new CloseChoiceWindow(allowFullExit: false) { Owner = this }.ShowDialog();
            } finally {
                _isCloseDialogOpen = false;
            }
            return false;
        }

        IsExitAllowed = true;
        Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
        return true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ClickCount == 2)
            MaximizeWindow(sender, e);
        else
            DragMove();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseWindow(object sender, RoutedEventArgs e) =>
        Close();

    private void OpenUpdatesTab(object sender, RoutedEventArgs e) {
        _viewModel.IsSettingsOpen = true;
        SettingsPanelHost.ShowUpdatesTab();
    }

    private void OpenDonationPage(object sender, RoutedEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo(SupportLinks.Donation) { UseShellExecute = true });
        }
        catch (Exception ex) {
            Diagnostics.CrashReporter.ReportUnexpected(ex, RealFenixFailures.UI.Localization.Loc.T("Mw_DonationLinkFailed"));
        }
    }
}
