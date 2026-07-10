using System.Windows.Controls;

namespace POS.Presentation.Views;

public partial class ShiftsView : UserControl
{
    public ShiftsView()
    {
        InitializeComponent();
    }

    // Binding SelectedItem from the VM (e.g. the Reportes "Ir a Turnos" redirect) does not
    // auto-scroll like keyboard/mouse selection does — ListBox only does that on its own for
    // user-driven selection changes, so it needs an explicit ScrollIntoView here.
    private void ShiftsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ShiftsListBox.ScrollIntoView(e.AddedItems[0]);
        }
    }
}
