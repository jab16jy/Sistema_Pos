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
        if (e.Key != Key.Enter || DataContext is not CashierViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        await viewModel.HandleScannerInputAsync();
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }
}
