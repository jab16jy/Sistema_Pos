using System.Windows;
using POS.Application.DTOs.Customers;
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

    private void AnularBeneficioButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.DataContext is not CustomerHistoryEntry entry)
        {
            return;
        }

        if (DataContext is not CustomersViewModel viewModel || !viewModel.CancelClaimCommand.CanExecute(entry))
        {
            return;
        }

        string benefitName = !string.IsNullOrWhiteSpace(entry.Reference)
            ? entry.Reference
            : "este beneficio";

        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            $"¿Estás seguro de que deseas anular el beneficio \"{benefitName}\"?\n\nEsta acción devolverá los puntos al saldo del cliente.",
            "Confirmar anulación de beneficio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            viewModel.CancelClaimCommand.Execute(entry);
        }
    }
}
