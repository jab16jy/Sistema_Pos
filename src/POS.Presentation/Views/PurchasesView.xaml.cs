using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Purchases;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class PurchasesView : UserControl
{
    public PurchasesView()
    {
        InitializeComponent();
    }

    private void OnCatalogSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && CatalogListBox.ItemsSource is System.Collections.IEnumerable itemsSource)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(itemsSource);
            string filterText = textBox.Text;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = obj =>
                {
                    if (obj is SupplierCatalogItemResult item)
                    {
                        return item.ProductName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                               (item.ProductReference?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (item.SupplierSku?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false);
                    }
                    return false;
                };
            }
        }
    }

    private void OnAddCatalogItemDirectly(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SupplierCatalogItemResult item)
        {
            if (DataContext is PurchasesViewModel vm)
            {
                vm.SelectedCatalogItem = item;
                if (vm.AddDraftItemCommand.CanExecute(null))
                {
                    vm.AddDraftItemCommand.Execute(null);
                }
            }
        }
    }

    private void CatalogProductSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchasesViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            if (viewModel.CatalogProductResults.Count == 0)
            {
                return;
            }

            int currentIndex = CatalogProductSuggestionsListBox.SelectedIndex;
            int delta = e.Key == Key.Down ? 1 : -1;
            int nextIndex = Math.Clamp(currentIndex + delta, 0, viewModel.CatalogProductResults.Count - 1);

            CatalogProductSuggestionsListBox.SelectedIndex = nextIndex;
            if (CatalogProductSuggestionsListBox.SelectedItem is not null)
            {
                CatalogProductSuggestionsListBox.ScrollIntoView(CatalogProductSuggestionsListBox.SelectedItem);
            }
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            
            // If there are selected products via checkboxes, add all of them
            if (viewModel.SelectedCatalogProductIds.Count > 0)
            {
                if (viewModel.AddAllSelectedCommand.CanExecute(null))
                {
                    viewModel.AddAllSelectedCommand.Execute(null);
                }
            }
            else if (CatalogProductSuggestionsListBox.SelectedItem is ProductLookupResult selectedProduct)
            {
                // Otherwise, add the currently highlighted product
                if (viewModel.AddProductToCatalogDirectlyCommand.CanExecute(selectedProduct))
                {
                    viewModel.AddProductToCatalogDirectlyCommand.Execute(selectedProduct);
                }
            }
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }
    }

    private void OnCatalogProductCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is ProductLookupResult product)
        {
            if (DataContext is PurchasesViewModel viewModel)
            {
                viewModel.ToggleProductSelectionCommand.Execute(product);
                // Update checkbox visual state based on selection
                checkBox.IsChecked = viewModel.IsProductSelected(product.ProductId);
            }
        }
    }
}
