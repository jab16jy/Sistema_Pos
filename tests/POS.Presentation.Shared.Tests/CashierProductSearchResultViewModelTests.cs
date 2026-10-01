using POS.Application.DTOs.Products;
using POS.Presentation.Shared.Cashier;
using Xunit;

namespace POS.Presentation.Shared.Tests;

/// <summary>
/// Presentations used to render in whatever order they were entered in Productos. The client wants
/// unit-type presentations (Units == 1) first, then box-type presentations biggest-to-smallest.
/// </summary>
public sealed class CashierProductSearchResultViewModelTests
{
    [Fact]
    public void AvailablePresentations_OrdersUnitTypeFirstThenBoxTypeBiggestToSmallest()
    {
        var product = new ProductLookupResult(
            Guid.NewGuid(), "Ampolla 350", "7701", 10_000m, 24m, 0m,
            [
                new ProductPresentationDto("Cuarto de caja", 3, 30_000m),
                new ProductPresentationDto("Caja", 12, 110_000m),
                new ProductPresentationDto("Individual B", 1, 9_000m),
                new ProductPresentationDto("Media caja", 6, 57_000m),
                new ProductPresentationDto("Individual A", 1, 8_500m),
            ]);

        var viewModel = new CashierProductSearchResultViewModel(product);

        // Entry order was Individual B, then Individual A; a stable sort preserves that within the
        // unit-type group. Box-type presentations follow, biggest-to-smallest by Units.
        Assert.Equal(
            [null, "Individual B", "Individual A", "Caja", "Media caja", "Cuarto de caja"],
            viewModel.AvailablePresentations.Select(p => p.Name));
    }

    [Fact]
    public void HasMultiplePresentations_IsFalseOnlyWhenTheLooseUnitIsTheSoleOption()
    {
        var single = new CashierProductSearchResultViewModel(
            new ProductLookupResult(Guid.NewGuid(), "Cable", "7702", 1_000m, 5m, 0m, []));
        var several = new CashierProductSearchResultViewModel(
            new ProductLookupResult(Guid.NewGuid(), "Ampolla", "7701", 10_000m, 24m, 0m,
                [new ProductPresentationDto("Caja", 12, 110_000m)]));

        Assert.False(single.HasMultiplePresentations);
        Assert.True(several.HasMultiplePresentations);
    }
}
