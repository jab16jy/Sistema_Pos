using System.Windows;
using System.Windows.Input;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class LoyaltyView
{
    public LoyaltyView()
    {
        InitializeComponent();
    }

    private void OnColorPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LoyaltyViewModel viewModel)
        {
            var dialog = new ColorPickerDialog(viewModel.TierColor)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                viewModel.TierColor = dialog.SelectedColorHex;
            }
        }
    }
}

