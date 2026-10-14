using RealFenixFailures.UI.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views;

public partial class MainWindow : Window {
    // Reemplazar por el link de donación real (ej. Ko-fi: https://ko-fi.com/tuusuario)
    private const string DonationUrl = "https://ko-fi.com/mathiasgonzalez0101";

    private readonly MainWindowViewModel _viewModel;
    public MainWindow(MainWindowViewModel viewModel) {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
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
            Process.Start(new ProcessStartInfo(DonationUrl) { UseShellExecute = true });
        }
        catch (Exception ex) {
            MessageBox.Show($"No se pudo abrir el navegador: {ex.Message}",
                "Donar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}