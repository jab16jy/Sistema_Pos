using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class RuntimeGapDomainTests
{
    [Fact]
    public void ProductUpdateDetails_WhenProductIsInactive_RejectsUpdate()
    {
        var product = CreateProduct();
        product.Deactivate();

        Assert.Throws<InvalidOperationException>(() => product.UpdateDetails(
            "Updated",
            Guid.NewGuid(),
            new Money(1_200m),
            new Money(1_800m),
            new StockQuantity(3m)));
    }

    [Fact]
    public void BusinessSettings_NormalizesCurrencyAndOptionalText()
    {
        var settings = new BusinessSettings(
            " Esthetic Medical Store ",
            "  Main Street 1 ",
            " 900123 ",
            " smtp.office365.com ",
            587,
            " ventas ",
            " secret ",
            true,
            " ventas@store.com ",
            " ",
            null,
            " C:/Backups ",
            "cop");

        Assert.Equal("Esthetic Medical Store", settings.BusinessName);
        Assert.Equal("Main Street 1", settings.BusinessAddress);
        Assert.Equal("900123", settings.TaxId);
        Assert.Equal("smtp.office365.com", settings.SmtpHost);
        Assert.Equal(587, settings.SmtpPort);
        Assert.Equal("ventas", settings.SmtpUser);
        Assert.Equal("secret", settings.SmtpPassword);
        Assert.True(settings.SmtpUseSsl);
        Assert.Equal("ventas@store.com", settings.SmtpSenderAddress);
        Assert.Null(settings.PrinterCommand);
        Assert.Equal("C:/Backups", settings.BackupDirectory);
        Assert.Equal("COP", settings.CurrencyCode);
    }

    [Fact]
    public void SaleReturn_WithMissingReason_RejectsCreation()
    {
        Assert.Throws<ArgumentException>(() => new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), " "));
    }

    [Fact]
    public void MarkReturned_WhenSaleCompleted_ChangesStatus()
    {
        var sale = new Sale(Guid.NewGuid());
        var product = CreateProduct();
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(1_500m), new Money(1_500m)));
        sale.Complete();

        sale.MarkReturned();

        Assert.Equal(SaleStatus.Returned, sale.Status);
    }

    private static Product CreateProduct() => new(
        "Rice",
        Guid.NewGuid(),
        new Money(1_000m),
        new Money(1_500m),
        new StockQuantity(10m),
        new StockQuantity(2m));
}
