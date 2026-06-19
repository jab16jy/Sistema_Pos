using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class QuoteTests
{
    [Fact]
    public void AddItem_WhenStatusIsConverted_ThrowsQuoteAlreadyConvertedException()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.Convert(Guid.NewGuid());

        var item = new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(1m), 0m);

        Assert.Throws<QuoteAlreadyConvertedException>(() => quote.AddItem(item));
    }

    [Fact]
    public void Convert_WhenAlreadyConverted_ThrowsQuoteAlreadyConvertedException()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.Convert(Guid.NewGuid());

        Assert.Throws<QuoteAlreadyConvertedException>(() => quote.Convert(Guid.NewGuid()));
    }

    [Fact]
    public void Convert_WhenActive_SetsStatusToConvertedAndSetsConvertedSaleId()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var saleId = Guid.NewGuid();

        quote.Convert(saleId);

        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(saleId, quote.ConvertedSaleId);
    }

    [Fact]
    public void Expire_WhenActive_SetsStatusToExpired()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);

        quote.Expire();

        Assert.Equal(QuoteStatus.Expired, quote.Status);
    }

    [Fact]
    public void Expire_WhenConverted_DoesNotChangeStatus()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var saleId = Guid.NewGuid();
        quote.Convert(saleId);

        quote.Expire();

        Assert.Equal(QuoteStatus.Converted, quote.Status);
    }

    [Fact]
    public void Total_IsCorrectSumOfItemSubtotals()
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        var item1 = new QuoteItem(Guid.NewGuid(), "Product A", new Money(1_000m), new Quantity(2m), 0m);
        var item2 = new QuoteItem(Guid.NewGuid(), "Product B", new Money(500m), new Quantity(3m), 10m);

        quote.AddItem(item1);
        quote.AddItem(item2);

        var expectedTotal = item1.Subtotal().Amount + item2.Subtotal().Amount;
        Assert.Equal(expectedTotal, quote.Total);
    }

    [Fact]
    public void QuoteItem_Subtotal_CalculatesCorrectly()
    {
        // UnitPrice=100, Quantity=2, Discount=10 → 100 * 2 * (1 - 10/100) = 200 * 0.90 = 180
        var item = new QuoteItem(Guid.NewGuid(), "Product A", new Money(100m), new Quantity(2m), 10m);

        var subtotal = item.Subtotal();

        Assert.Equal(180m, subtotal.Amount);
    }
}
