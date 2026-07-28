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

    /// <summary>
    /// Opens the tier's configuration when its row is clicked.
    /// </summary>
    /// <remarks>
    /// Driven by the click rather than by SelectedTier changing, because clicking the row that is
    /// already selected raises no selection change — and after a refresh the first tier is selected,
    /// so that would be a dead click on the most obvious target. ButtonUp runs after the ListBox has
    /// applied the selection, so the view model already points at the clicked tier.
    /// </remarks>
    private void OnTierItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LoyaltyViewModel { SelectedTier: not null } viewModel)
        {
            viewModel.IsTierEditorOpen = true;
        }
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

