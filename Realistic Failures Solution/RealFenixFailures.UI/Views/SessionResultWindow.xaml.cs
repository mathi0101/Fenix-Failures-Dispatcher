using RealFenixFailures.UI.Converters;
using RealFenixFailures.UI.ViewModels.Realistic;
using System.Windows;
using System.Windows.Input;

namespace RealFenixFailures.UI.Views;

/// <summary>
/// Modal de resultados de una sesión Realista finalizada (diseño objetivo §13, mock aprobado):
/// resumen, cronología completa de eventos, fallas lanzadas con su consolidación y desgaste por componente.
/// </summary>
public partial class SessionResultWindow : Window {
    public SessionResultWindow(SessionResultWindowViewModel viewModel) {
        InitializeComponent();
        Resources["InverseBoolToVis"] = new InverseBoolToVisibilityConverter();
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ClickCount == 1) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
