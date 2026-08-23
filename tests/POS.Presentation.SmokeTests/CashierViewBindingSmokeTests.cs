using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using POS.Application.DTOs.Products;
using POS.Presentation;
using POS.Presentation.Shared.Cashier;
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
                // Default ShutdownMode is OnLastWindowClose; each ShowAndClose below would tear the
                // Dispatcher down after the first window if left unset (App.xaml.cs does the same
                // for the login window, which also closes with none of the shell windows open yet).
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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

                // A product configured with many presentations (now up to 50, see
                // BusinessSettings.MaximumProductPresentations) must wrap onto extra lines
                // instead of overflowing the row's WrapPanel unbounded.
                var quoteView = new QuoteView { DataContext = new QuoteSearchResultsStub(BuildManyPresentationsRow()) };
                AssertPresentationsWrap(quoteView);

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

    private static void AssertPresentationsWrap(UIElement view)
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

        var wrapPanel = FindVisualChild<System.Windows.Controls.WrapPanel>(window)
            ?? throw new InvalidOperationException("Expected a WrapPanel rendering the presentation buttons.");
        Assert.True(
            wrapPanel.ActualWidth <= 300,
            $"Presentations WrapPanel should wrap within 300px, but measured {wrapPanel.ActualWidth}px.");

        window.Close();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var found = FindVisualChild<T>(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static CashierProductSearchResultViewModel BuildManyPresentationsRow()
    {
        var presentations = Enumerable.Range(1, 20)
            .Select(i => new ProductPresentationDto($"Caja x{i}", i + 1, 1_000m * i))
            .ToList();
        var product = new ProductLookupResult(
            Guid.NewGuid(), "Producto con muchas presentaciones", "0001", 5_000m, 100m, 0m, presentations);
        return new CashierProductSearchResultViewModel(product);
    }

    private sealed class QuoteSearchResultsStub(CashierProductSearchResultViewModel row)
    {
        public ObservableCollection<CashierProductSearchResultViewModel> SearchResults { get; } = [row];
    }
}
