using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class CashierView : UserControl
{
    private bool _isSuggestionNavigated;

    public CashierView()
    {
        InitializeComponent();
        DataContextChanged += CashierView_DataContextChanged;
    }

    private void CashierView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CashierViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is CashierViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CashierViewModel.IsAwaitingSaleConfirmation) or nameof(CashierViewModel.IsAwaitingPrintChoice))
        {
            if (sender is CashierViewModel vm && (vm.IsAwaitingSaleConfirmation || vm.IsAwaitingPrintChoice))
            {
                ScrollRightPanelToBottom();
            }
        }
    }

    private void ScrollRightPanelToBottom()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            RightPanelScrollViewer?.ScrollToBottom();
        }));
    }

    private async void CashierView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CashierViewModel viewModel)
        {
            await viewModel.LoadHeldSalesAsync();
            if (viewModel.IsAwaitingSaleConfirmation || viewModel.IsAwaitingPrintChoice)
            {
                ScrollRightPanelToBottom();
            }
        }
    }

    private void CashierView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CashierViewModel viewModel)
        {
            return;
        }

        ICommand? command = e.Key switch
        {
            Key.F5 => viewModel.ConfirmSaleCommand,
            Key.F6 => viewModel.AssociateCustomerCommand,
            Key.F7 => viewModel.HoldSaleCommand,
            _ => null
        };

        if (command is not null)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && viewModel.CanCancelCurrentSale)
        {
            e.Handled = true;
            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this),
                "¿Descartar la venta actual? Los productos del carrito se eliminarán.",
                "Cancelar venta",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                viewModel.CancelCurrentSale();
            }
        }
    }

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _isSuggestionNavigated = false;
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
            _isSuggestionNavigated = true;
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
            await viewModel.HandleSearchEnterAsync(_isSuggestionNavigated);
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

    private async void CustomerResultItem_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CashierViewModel viewModel)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: CustomerSearchResult customer })
        {
            await viewModel.SelectCustomerAsync(customer);
        }
    }

    private void ReceivedAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsTextAllowed(e.Text);
    }

    private void ReceivedAmount_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && DataContext is CashierViewModel viewModel)
        {
            viewModel.CommitReceivedAmount();
        }
    }

    private void ReceivedAmount_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is CashierViewModel viewModel)
        {
            viewModel.CommitReceivedAmount();
        }
    }

    private void ReceivedAmount_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            if (!IsTextAllowed(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static bool IsTextAllowed(string text)
    {
        return text.All(char.IsDigit);
    }
}
