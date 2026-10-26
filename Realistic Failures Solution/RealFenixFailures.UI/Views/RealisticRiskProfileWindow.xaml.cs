using RealFenixFailures.UI.ViewModels.Realistic;
using System.Windows;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views;

public partial class RealisticRiskProfileWindow : Window {
    public RealisticRiskProfileWindow() {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) {
        if (DataContext is not RiskProfileWindowViewModel vm) return;
        await vm.RefreshAsync();
    }

    private void ConfirmProfile_Click(object sender, RoutedEventArgs e) {
        if (DataContext is not RiskProfileWindowViewModel vm) return;
        if (!vm.Confirm()) return;
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
