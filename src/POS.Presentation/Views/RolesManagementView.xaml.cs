using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Views;

public partial class RolesManagementView : System.Windows.Controls.UserControl
{
    public RolesManagementView()
    {
        InitializeComponent();
        DataContextChanged += (_, args) =>
        {
            if (args.NewValue is RolesManagementViewModel viewModel && viewModel.LoadRolesCommand.CanExecute(null))
            {
                viewModel.LoadRolesCommand.Execute(null);
            }
        };
    }
}
