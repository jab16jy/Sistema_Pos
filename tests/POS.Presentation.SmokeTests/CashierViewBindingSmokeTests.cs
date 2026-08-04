using System.Windows;
using POS.Presentation;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;
using POS.Presentation.Views;
using Xunit;

namespace POS.Presentation.SmokeTests;

public sealed class CashierViewBindingSmokeTests
{
    /// <summary>
    /// Loads the views whose markup pulls resources out of App.xaml, so bad XAML or an unresolvable
    /// StaticResource fails here instead of at the till.
    /// </summary>
    /// <remarks>
    /// Every view shares one test because WPF allows a single <see cref="Application"/> per
    /// AppDomain — a second test constructing its own App throws before it can assert anything.
    /// </remarks>
    [Fact]
    public void Views_LoadPresentationBindings()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App();
                application.InitializeComponent();

                var cashier = new CashierView
                {
                    DataContext = new CashierViewModel(new PrototypeProductLookupService())
                };
                ShowAndClose(cashier);

                // No DataContext: binding paths do not throw, but the tier benefit list, the
                // redemption checkbox and the benefit catalogue pane fail to load if their markup or
                // resources are wrong.
                ShowAndClose(new LoyaltyView());

                // Same for the benefit claim panel and the redemption history rows.
                ShowAndClose(new CustomersView());

                // The product form and the quote lines render the user-defined presentation rows;
                // a missing StaticResource or a single-child panel given two children only shows up
                // when the markup is actually loaded.
                ShowAndClose(new ProductsView());
                ShowAndClose(new QuoteView());

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

    private static void ShowAndClose(UIElement view)
    {
        var window = new Window
        {
            Content = view,
            Width = 1200,
            Height = 800,
            ShowInTaskbar = false
        };

        window.Show();
        window.UpdateLayout();
        window.Close();
    }
}
