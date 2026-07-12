using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            CatalogSearchBox.SelectAll();
        }
    }

    private void CatalogSuggestions_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NewPurchase is not { SelectedCatalogItem: not null } viewModel)
        {
            return;
        }

        viewModel.AddSelectedCatalogItem();
        CatalogSearchBox.Focus();
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
}
