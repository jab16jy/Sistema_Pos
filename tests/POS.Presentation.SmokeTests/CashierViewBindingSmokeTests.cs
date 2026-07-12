using System.Windows;
using POS.Presentation;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;
using POS.Presentation.Views;
using Xunit;

namespace POS.Presentation.SmokeTests;

public sealed class CashierViewBindingSmokeTests
{
    [Fact]
    public void CashierView_LoadsPresentationToggleBindings()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App();
                application.InitializeComponent();
                var view = new CashierView
                {
                    DataContext = new CashierViewModel(new PrototypeProductLookupService())
                };
                var window = new Window
                {
                    Content = view,
                    Width = 800,
                    Height = 600,
                    ShowInTaskbar = false
                };

                window.Show();
                window.UpdateLayout();
                window.Close();
                application.Shutdown();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }
}
