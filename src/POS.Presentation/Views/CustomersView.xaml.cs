using System.Windows;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class CustomersView : System.Windows.Controls.UserControl
{
    public CustomersView()
    {
        InitializeComponent();
    }

    private void PagarTodoSaldoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CustomersViewModel viewModel || !viewModel.PagarTodoSaldoCommand.CanExecute(null))
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            $"¿Estás seguro de pagar todo el saldo pendiente ({viewModel.OutstandingBalanceFormatted}) de todas las facturas de este cliente?",
            "Confirmar pago total",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            viewModel.PagarTodoSaldoCommand.Execute(null);
        }
    }
}
