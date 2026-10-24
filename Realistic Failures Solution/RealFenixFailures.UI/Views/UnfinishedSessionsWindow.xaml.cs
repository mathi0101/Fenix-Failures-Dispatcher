using RealFenixFailures.UI.ViewModels;
using System.Windows;

namespace RealFenixFailures.UI.Views;

/// <summary>
/// Diálogo de recuperación de sesiones sin finalizar (diseño objetivo §6/§13): se muestra al arrancar
/// la app si quedó una sesión realista abierta por un cierre brusco. «Finalizar» consolida + da resultados,
/// «Descartar» queda en historial sin desgaste, «Dejar para después» no toca nada.
/// </summary>
public partial class UnfinishedSessionsWindow : Window {
    private readonly MainWindowViewModel _viewModel;

    public UnfinishedSessionsWindow(MainWindowViewModel viewModel) {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.UnfinishedSessionsResolved += (_, _) => Close();
    }

    private void LeaveForLater_Click(object sender, RoutedEventArgs e) => Close();
}
