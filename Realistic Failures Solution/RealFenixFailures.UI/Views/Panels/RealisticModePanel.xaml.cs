using RealFenixFailures.UI.ViewModels.Realistic;
using RealFenixFailures.UI.Views;
using System.Windows;
using System.Windows.Controls;

namespace RealFenixFailures.UI.Views.Panels;
/// <summary>
/// Interaction logic for TrainingModePanel.xaml
/// </summary>
public partial class RealisticModePanel : UserControl {
    public RealisticModePanel() {
        InitializeComponent();
    }

    private void OpenRiskProfileWindow(object sender, RoutedEventArgs e) {
        if (RealisticRoot.DataContext is not RealisticModeViewModel vm) return;
        if (vm.IsEngineActive || vm.IsOperationPending) return;
        var window = new RealisticRiskProfileWindow {
            Owner = Window.GetWindow(this),
            DataContext = vm.CreateRiskProfileDialog()
        };
        window.ShowDialog();
    }
}
