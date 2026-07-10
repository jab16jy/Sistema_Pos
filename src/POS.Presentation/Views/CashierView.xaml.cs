using System.Windows.Controls;
using System.Windows.Input;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class CashierView : UserControl
{
    public CashierView()
    {
        InitializeComponent();
    }

    private async void SearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CashierViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            viewModel.MoveSelection(e.Key == Key.Down ? 1 : -1);
            if (viewModel.SelectedProduct is not null)
            {
                ResultsListBox.ScrollIntoView(viewModel.SelectedProduct);
            }

            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        try
        {
            await viewModel.HandleScannerInputAsync();
        }
        catch (Exception ex)
        {
            viewModel.ShowError($"Error al procesar la búsqueda: {ex.Message}");
        }

        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void ResultsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CashierViewModel viewModel || viewModel.SelectedProduct is null)
        {
            return;
        }

        viewModel.AddProductAndResetSearch(viewModel.SelectedProduct);
        SearchTextBox.Focus();
    }

    private async void CustomerSearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CashierViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            viewModel.MoveCustomerSelection(e.Key == Key.Down ? 1 : -1);
            if (viewModel.SelectedCustomerSearchResult is not null)
            {
                CustomerResultsListBox.ScrollIntoView(viewModel.SelectedCustomerSearchResult);
            }

            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (viewModel.CustomerSearchResults.Count == 0)
        {
            await viewModel.SearchCustomersAsync();
        }
        else
        {
            await viewModel.ConfirmCustomerSelectionAsync();
        }
    }
}
