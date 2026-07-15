using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CashierCancelTests
{
    [Fact]
    public async Task CancelCurrentSale_ClearsOnlyTheUnsavedCart()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.True(viewModel.CanCancelCurrentSale);
        viewModel.CancelCurrentSale();

        Assert.Empty(viewModel.CartItems);
        Assert.Contains("descartada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }
}
