﻿using RealFenixFailures.UI.Services;
using RealFenixFailures.UI.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views;

public partial class MainWindow : Window {

    private readonly MainWindowViewModel _viewModel;
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

    public MainWindow(MainWindowViewModel viewModel) {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => {
            await _viewModel.InitializeAsync();
            await ShowUnfinishedSessionsDialogAsync();
        };
        Closing += MainWindow_Closing;
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

    private void MainWindow_Closing(object? sender, CancelEventArgs e) {
        if (IsExitAllowed)
            return;

        e.Cancel = true;

        if (_isCloseDialogOpen)
            return;

        _isCloseDialogOpen = true;
        try {
            var dialog = new CloseChoiceWindow { Owner = this };
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

    private void OpenDonationPage(object sender, RoutedEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo(SupportLinks.Donation) { UseShellExecute = true });
        }
        catch (Exception ex) {
            Diagnostics.CrashReporter.ReportUnexpected(ex, "No se pudo abrir el enlace de donación.");
        }
    }
}
