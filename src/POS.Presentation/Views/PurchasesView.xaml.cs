using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

/// <summary>
/// Visual interaction only (keyboard, focus, numeric input filtering).
/// All decisions are delegated to <see cref="NewPurchaseViewModel"/>.
/// </summary>
public partial class PurchasesView : UserControl
{
    public PurchasesView()
    {
        InitializeComponent();
    }

    private NewPurchaseViewModel? NewPurchase => (DataContext as PurchasesViewModel)?.NewPurchase;

    private void CatalogSearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (NewPurchase is not { } viewModel)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            viewModel.MoveCatalogSelection(e.Key == Key.Down ? 1 : -1);
            if (viewModel.SelectedCatalogItem is not null)
            {
                CatalogSuggestionsListBox.ScrollIntoView(viewModel.SelectedCatalogItem);
            }

            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (viewModel.AddSelectedCatalogItem())
        {
            CatalogSearchBox.Focus();
        }
    }

    private void CatalogSuggestions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NewPurchase is not { SelectedCatalogItem: not null } viewModel)
        {
            return;
        }

        if (viewModel.AddSelectedCatalogItem())
        {
            CatalogSearchBox.Focus();
        }
    }

    private void CatalogAddButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (string.IsNullOrWhiteSpace(NewPurchase?.CatalogSearchText))
            {
                CatalogSearchBox.Focus();
            }
        });
    }

    private void PurchaseHistoryDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchasesViewModel viewModel && PurchaseHistoryDataGrid.SelectedItem is { } purchase)
        {
            viewModel.OpenPurchaseDetailCommand.Execute(purchase);
        }
    }

    private void Decimal_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDecimalTextAllowed(sender as TextBox, e.Text);
    }

    private void Decimal_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void Decimal_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)) ||
            !IsDecimalTextAllowed(sender as TextBox, (string)e.DataObject.GetData(typeof(string))))
        {
            e.CancelCommand();
        }
    }

    private static bool IsDecimalTextAllowed(TextBox? box, string text)
    {
        bool hasSeparator = box?.Text.Contains(',') == true || box?.Text.Contains('.') == true;
        foreach (char character in text)
        {
            if (char.IsDigit(character))
            {
                continue;
            }

            if ((character is ',' or '.') && !hasSeparator)
            {
                hasSeparator = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private void CatalogProductSearchTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (CatalogProductAssignmentsDataGrid.Visibility == Visibility.Visible && CatalogProductAssignmentsDataGrid.Items.Count > 0)
            {
                CatalogProductAssignmentsDataGrid.Focus();
                CatalogProductAssignmentsDataGrid.SelectedIndex = 0;

                // Enfocar la fila seleccionada del DataGrid para que se pueda navegar con las flechas
                var row = (DataGridRow)CatalogProductAssignmentsDataGrid.ItemContainerGenerator.ContainerFromIndex(0);
                if (row != null)
                {
                    row.Focus();
                }
                e.Handled = true;
            }
        }
    }

    private void CatalogProductAssignmentsDataGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject dep = (DependencyObject)e.OriginalSource;
        while (dep != null && dep is not DataGridRow && dep is not DataGridCell)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (dep is DataGridRow row && row.DataContext is SupplierProductAssignmentItem item)
        {
            item.IsSelected = !item.IsSelected;
            e.Handled = true;
        }
    }

    private void CatalogProductAssignmentsDataGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (CatalogProductAssignmentsDataGrid.SelectedItem is SupplierProductAssignmentItem item)
            {
                item.IsSelected = !item.IsSelected;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Up && CatalogProductAssignmentsDataGrid.SelectedIndex == 0)
        {
            CatalogProductSearchTextBox.Focus();
            e.Handled = true;
        }
    }
}
