using System.Windows.Input;
using System.Windows.Threading;
using POS.Presentation.Shared.Shell;

namespace POS.Presentation.Views;

public partial class NavigationShellView : System.Windows.Window
{
    private readonly DispatcherTimer _clockTimer;

    public NavigationShellView()
    {
        InitializeComponent();
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) =>
        {
            UpdateClock();
            _clockTimer.Start();
        };
        Unloaded += (_, _) => _clockTimer.Stop();
    }

    private void UpdateClock()
    {
        ClockTextBlock.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not POS.Presentation.Shared.ViewModels.MainShellViewModel vm)
            return;

        var modules = vm.FuncbarModules;
        if (modules.Count == 0)
            return;

        var currentIndex = modules.IndexOf(vm.CurrentFuncbarModule);
        int nextIndex;

        if (e.Delta > 0)
        {
            // Scroll up → previous module
            nextIndex = currentIndex <= 0 ? modules.Count - 1 : currentIndex - 1;
        }
        else
        {
            // Scroll down → next module
            nextIndex = currentIndex >= modules.Count - 1 ? 0 : currentIndex + 1;
        }

        if (nextIndex >= 0 && nextIndex < modules.Count)
        {
            vm.SelectModuleCommand.Execute(modules[nextIndex]);
        }
    }
}
