using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

}
