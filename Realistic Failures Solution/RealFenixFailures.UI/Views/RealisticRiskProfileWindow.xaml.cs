using RealFenixFailures.UI.ViewModels.Realistic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views;

public partial class RealisticRiskProfileWindow : Window {
    private int? _selectedProfile;

    public RealisticRiskProfileWindow() {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        if (DataContext is not RealisticModeViewModel vm) return;
        SelectProfile(vm.SelectedRiskLevel);
    }

    private void Profile_Checked(object sender, RoutedEventArgs e) {
        if (sender is not RadioButton rb) return;
        if (int.TryParse(rb.Tag as string, out var value) && value is >= 1 and <= 4)
            SelectProfile(value);
    }

    private void SelectProfile(int? profile) {
        _selectedProfile = profile;
        RadioLow.IsChecked = profile == 1;
        RadioModerate.IsChecked = profile == 2;
        RadioHigh.IsChecked = profile == 3;
        RadioSquawk7700.IsChecked = profile == 4;
        ConfirmButton.IsEnabled = profile.HasValue && profile is >= 1 and <= 4;
    }

    private void ConfirmProfile_Click(object sender, RoutedEventArgs e) {
        if (!_selectedProfile.HasValue) return;
        if (DataContext is not RealisticModeViewModel vm) return;
        if (!vm.SelectRiskCommand.CanExecute(_selectedProfile.Value.ToString())) return;
        vm.SelectRiskCommand.Execute(_selectedProfile.Value.ToString());
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
