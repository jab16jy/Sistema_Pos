using System.Windows.Threading;

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
}
