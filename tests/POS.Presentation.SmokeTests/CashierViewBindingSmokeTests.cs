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

                // A product configured with many presentations (up to 50, see
                // BusinessSettings.MaximumProductPresentations) must not make its result row taller.
                double manyRow = MeasureResultRowHeight(BuildManyPresentationsRow());
                double singleRow = MeasureResultRowHeight(BuildSinglePresentationRow());
                Assert.True(
                    manyRow <= singleRow + 1,
                    $"A row with 20 presentations ({manyRow}px) must stay as tall as one with a single presentation ({singleRow}px).");

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

    /// <summary>
    /// A product with many presentations must not make its result row taller than a product with one.
    /// </summary>
    /// <remarks>
    /// Compared rather than bounded, because the search list sits in a star-sized row: its
    /// <c>ItemsControl.ActualHeight</c> is the space the layout handed it, not the row's own height, so
    /// any fixed ceiling there measures the layout slot and passes for the wrong reason. The previous
    /// guard capped the presentation panel at 300px, which is what forced the buttons to wrap and is
    /// the pile-up this test exists to catch.
    /// </remarks>
    private static double ResolveResultRowHeight(UIElement view, System.Collections.IEnumerable searchResults)
    {
        // Matched by ItemsSource, not "the first ItemsControl": the quotes view also lists customers
        // and cart lines, and a plain first-match measures the wrong list and passes for the wrong reason.
        var results = FindVisualChild<System.Windows.Controls.ItemsControl>(
                view,
                candidate => ReferenceEquals(candidate.ItemsSource, searchResults))
            ?? throw new InvalidOperationException("Expected the ItemsControl bound to the search results.");

        var row = FindVisualChild<System.Windows.Controls.ContentPresenter>(results)
            ?? throw new InvalidOperationException("Expected a generated container for the single search result.");

        Assert.NotNull(FindVisualChild<System.Windows.Controls.ComboBox>(results));

        return row.ActualHeight;

        return row.ActualHeight;
    }

    private static double MeasureResultRowHeight(CashierProductSearchResultViewModel row)
    {
        var stub = new QuoteSearchResultsStub(row);
        var quoteView = new QuoteView { DataContext = stub };
        var window = new Window
        {
            Content = quoteView,
            Width = 1200,
            Height = 800,
            ShowInTaskbar = false
        };

        window.Show();
        window.UpdateLayout();
        double height = ResolveResultRowHeight(quoteView, stub.SearchResults);
        window.Close();
        return height;
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? matches = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && (matches is null || matches(typed)))
            {
                return typed;
            }

            var found = FindVisualChild(child, matches);
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

    private static CashierProductSearchResultViewModel BuildSinglePresentationRow()
    {
        var product = new ProductLookupResult(
            Guid.NewGuid(), "Producto de una sola presentación", "0002", 5_000m, 100m, 0m, null);
        return new CashierProductSearchResultViewModel(product);
    }

    private sealed class QuoteSearchResultsStub(CashierProductSearchResultViewModel row)
    {
        public ObservableCollection<CashierProductSearchResultViewModel> SearchResults { get; } = [row];
    }
}
