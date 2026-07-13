using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests.Infrastructure;

public sealed class ThermalPrinterIntegrationTests
{
    [Fact]
    public async Task EscPosPrinterService_EncodesAccentedTextAsSingleByteLatin1_WithWpc1252CodePageSelected()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "COM7", // Serial target — deterministic regardless of what's installed on the test machine.
            null,
            null,
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Jabón Cañón", new Money(1_000m), new Money(2_000m),
            new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext);
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                Guid.NewGuid(),
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_000m, 2_000m, null)]),
            TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(resolver, transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        byte[] payload = transport.LastPayload!;

        // ESC @ (init) immediately followed by ESC t 16 (select WPC1252 code table).
        Assert.Equal([0x1B, 0x40, 0x1B, 0x74, 0x10], payload[..5]);

        // "ó" must appear as its single WPC1252/Latin1 byte (0xF3), never the 2-byte UTF-8 sequence.
        Assert.Contains((byte)0xF3, payload);
        byte[] utf8Sequence = Encoding.UTF8.GetBytes("ó");
        Assert.DoesNotContain(payload, b => b == utf8Sequence[0]);
    }

    [Fact]
    public async Task EscPosPrinterService_PrintsCustomerNameAndLoyaltyPointsDiscount()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio", null, null, null, null, null, null, null, false, null, "COM7", null, null, null, "COP"));

        var customer = new Customer("Maria Perez", "123456789");
        customer.UpdateContactInfo("Maria Perez", "123456789", "3001234567", "maria@example.com", "Calle 10 # 20-30");
        fixture.DbContext.Customers.Add(customer);
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Crema facial", new Money(1_000m), new Money(10_000m),
            new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext);
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                Guid.NewGuid(),
                customer.Id,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [
                    new CreateSalePaymentRequest(PaymentMethod.LoyaltyPoints, 2_000m, null, null),
                    new CreateSalePaymentRequest(PaymentMethod.Cash, 8_000m, 8_000m, null)
                ]),
            TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(resolver, transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        string text = Encoding.Latin1.GetString(transport.LastPayload!);
        Assert.Contains("Maria Perez", text);
        Assert.Contains("123456789", text);
        Assert.Contains("3001234567", text);
        Assert.Contains("maria@example.com", text);
        Assert.Contains("Direccion: Calle 10 # 20-30", text);
        Assert.Contains("Desc. puntos:", text);
        Assert.Contains("-$2.000", text);
    }

    [Fact]
    public async Task PrinterSettingsResolver_PrefersPersistedPrinterConfiguration()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "COM7",
            "C:\\printer",
            null,
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var runtime = new PrinterRuntimeOptions
        {
            Command = "192.168.1.10:9100",
            WorkingDirectory = "C:\\runtime"
        };

        var resolver = new PrinterSettingsResolver(runtime, fixture.DbContext);

        ThermalPrinterTarget result = await resolver.ResolveRequiredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("COM7", result.Command);
        Assert.Equal("C:\\printer", result.WorkingDirectory);
        Assert.Equal(ThermalPrinterTargetKind.Serial, result.Kind);
    }

    [Fact]
    public async Task PrinterSettingsResolver_ExplicitPortOverridesPrinterName()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "EPSON TM-T20III",
            null,
            "192.168.1.50:9100",
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);

        ThermalPrinterTarget result = await resolver.ResolveRequiredAsync(TestContext.Current.CancellationToken);

        Assert.Equal("192.168.1.50:9100", result.Command);
        Assert.Equal(ThermalPrinterTargetKind.Tcp, result.Kind);
    }

    [Fact]
    public async Task LabelPrintService_UsesResolvedThermalTarget()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "COM5",
            null,
            null,
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new LabelPrintService(resolver, transport);

        await service.PrintAsync(
            [new LabelRequest("Cafe molido", 12000m, "770123456789", BarcodeSymbology.Ean13)],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("COM5", transport.LastTarget?.Command);
        Assert.NotNull(transport.LastPayload);
        Assert.NotEmpty(transport.LastPayload!);
    }

    [Fact]
    public async Task LabelPrintService_RejectsNonDirectThermalCommand()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "C:\\tools\\print-receipt.cmd",
            "C:\\tools",
            null,
            null,
            "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new LabelPrintService(resolver, transport);

        var ex = await Assert.ThrowsAsync<ApplicationException>(() => service.PrintAsync(
            [new LabelRequest("Cafe molido", 12000m, "CODE128-01", BarcodeSymbology.Code128)],
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("impresión térmica directa", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(transport.LastPayload);
    }

    private sealed class FakeThermalPrinterTransport : IThermalPrinterTransport
    {
        public ThermalPrinterTarget? LastTarget { get; private set; }
        public byte[]? LastPayload { get; private set; }

        public Task SendAsync(ThermalPrinterTarget target, byte[] payload, CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDbContextFactory : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDbContextFactory(SqliteConnection connection, AppDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public AppDbContext DbContext { get; }

        public static async Task<TestDbContextFactory> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new TestDbContextFactory(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
