using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class ProductsView
{
    public ProductsView()
    {
        InitializeComponent();
    }

    private void AssignmentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AssignmentGrid != null && AssignmentGrid.SelectedItems.Count <= 1 && GroupPopup != null)
        {
            GroupPopup.IsOpen = false;
        }
    }

    private void AssignmentGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && AssignmentGrid != null)
        {
            var selectedItems = AssignmentGrid.SelectedItems.Cast<ProductCategoryAssignmentItem>().ToList();
            if (selectedItems.Count > 0)
            {
                // Toggle selection state: if the first item is selected, unselect all. Otherwise, select all.
                bool targetState = !selectedItems[0].IsSelected;
                foreach (var item in selectedItems)
                {
                    item.IsSelected = targetState;
                }
                e.Handled = true;
            }
        }
    }

    private void AssignmentGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (AssignmentGrid != null && AssignmentGrid.SelectedItems.Count > 1 && GroupPopup != null)
        {
            GroupPopup.IsOpen = true;
        }
    }

    private void GroupSelectedPopup_Click(object sender, RoutedEventArgs e)
    {
        if (AssignmentGrid != null)
        {
            var selectedItems = AssignmentGrid.SelectedItems.Cast<ProductCategoryAssignmentItem>().ToList();
            foreach (var item in selectedItems)
            {
                item.IsSelected = true;
            }
        }
        if (GroupPopup != null)
        {
            GroupPopup.IsOpen = false;
        }
    }

    private void UngroupSelected_Click(object sender, RoutedEventArgs e)
    {
        if (AssignmentGrid != null)
        {
            var selectedItems = AssignmentGrid.SelectedItems.Cast<ProductCategoryAssignmentItem>().ToList();
            foreach (var item in selectedItems)
            {
                item.IsSelected = false;
            }
        }
    }
}
