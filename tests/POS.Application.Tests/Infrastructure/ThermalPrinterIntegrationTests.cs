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
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
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
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
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
    public async Task EscPosPrinterService_PrintsItemDiscountGrossTotalAndLoyaltyTier()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio", null, null, null, null, null, null, null, false, null, "COM7", null, null, null, "COP"));

        var tier = new LoyaltyTier("Oro", minPoints: 100m, discountPercent: 10m);
        fixture.DbContext.LoyaltyTiers.Add(tier);

        var customer = new Customer("Maria Perez", "123456789");
        customer.SetLoyaltyTier(tier.Id);
        fixture.DbContext.Customers.Add(customer);
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Crema facial", new Money(1_000m), new Money(10_000m),
            new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                customer.Id,
                [new CreateSaleItemRequest(product.Id, 1m, 1_000m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 9_000m, 9_000m, null)]),
            TestContext.Current.CancellationToken);

        var resolver = new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext);
        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(resolver, transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        string text = Encoding.Latin1.GetString(transport.LastPayload!);
        Assert.Contains("Nivel: Oro (10% dcto)", text);
        Assert.Contains("Dcto: -$1.000", text);
        Assert.Contains("Total bruto:", text);
        Assert.Contains("Desc. productos:", text);
    }

    // The trailing gap is what the customer tears off and keeps, so it is specified in centimetres.
    // Counting blank lines would silently shrink or grow it whenever the line spacing changes, which
    // is exactly what happened when the body text was enlarged — hence ESC J, measured in dots.
    [Fact]
    public async Task EscPosPrinterService_EndsWithASixCentimetreFeed_AndEnlargedBodyText()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio", null, null, null, null, null, null, null, false, null, "COM7", null, null, null, "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Jabon", new Money(500m), new Money(2_000m),
            new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_000m, 2_000m, null)]),
            TestContext.Current.CancellationToken);

        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(
            new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext), transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        byte[] payload = transport.LastPayload!;

        // Body text is double height at single width: taller glyphs, same 32 characters per line.
        Assert.Contains(payload.Zip(payload.Skip(1), payload.Skip(2)).Select(t => (t.First, t.Second, t.Third)),
            triple => triple is (0x1D, 0x21, 0x01));

        // Line spacing is raised to fit them; the 48-dot glyphs would clip under the ~30 dot default.
        int spacing = IndexOfSequence(payload, [0x1B, 0x33]);
        Assert.True(spacing >= 0, "Receipt must set an explicit line spacing.");
        Assert.True(payload[spacing + 2] >= 48, $"Line spacing {payload[spacing + 2]} is shorter than a double-height glyph.");

        // Every ESC J before the cut must add up to 6 cm at 203 dpi.
        int totalFeedDots = 0;
        for (int i = 0; i + 2 < payload.Length; i++)
        {
            if (payload[i] == 0x1B && payload[i + 1] == 0x4A)
                totalFeedDots += payload[i + 2];
        }

        Assert.Equal(60 * 8, totalFeedDots);
        Assert.Equal<byte[]>([0x1D, 0x56, 0x00], payload[^3..]);
    }

    // 58 mm paper holds 32 characters. A 33-character row does not get cut off — the printer wraps
    // it, so the last digit of a line total lands alone on the next line.
    [Fact]
    public async Task EscPosPrinterService_KeepsEveryRowWithinTheThirtyTwoColumnPaper()
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            "Negocio", null, null, null, null, null, null, null, false, null, "COM7", null, null, null, "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // A line total in the millions is the widest amount the layout has to survive.
        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Vitrina refrigerada industrial", new Money(500_000m), new Money(1_250_000m),
            new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                null,
                [new CreateSaleItemRequest(product.Id, 3m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 3_750_000m, 3_750_000m, null)]),
            TestContext.Current.CancellationToken);

        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(
            new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext), transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        string text = Encoding.Latin1.GetString(transport.LastPayload!);
        Assert.Contains("$3.750.000", text);

        foreach (string line in StripCommands(text).Split('\n'))
        {
            Assert.True(line.Length <= 32, $"Line of {line.Length} characters overflows the paper: '{line}'");
        }
    }

    [Theory]
    [InlineData("Tienda EPOS", true)]        // 11 characters — fits the 16-column double-width line.
    [InlineData("Distribuidora EPOS", false)] // 18 would wrap, stranding "OS" on its own line.
    public async Task EscPosPrinterService_UsesTheDoubleWidthHeadline_OnlyWhenTheNameFits(
        string businessName, bool expectsDoubleWidth)
    {
        await using var fixture = await TestDbContextFactory.CreateAsync();
        fixture.DbContext.BusinessSettings.Add(new BusinessSettings(
            businessName, null, null, null, null, null, null, null, false, null, "COM7", null, null, null, "COP"));
        await fixture.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            fixture.DbContext, "Jabon", new Money(500m), new Money(2_000m),
            new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(fixture.DbContext),
            new SaleRepository(fixture.DbContext),
            fixture.DbContext,
            authorizer: TestAuthorizer.Create(dbContext: fixture.DbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_000m, 2_000m, null)]),
            TestContext.Current.CancellationToken);

        var transport = new FakeThermalPrinterTransport();
        var service = new EscPosPrinterService(
            new PrinterSettingsResolver(new PrinterRuntimeOptions(), fixture.DbContext), transport, fixture.DbContext);

        await service.PrintReceiptAsync(sale.SaleId, TestContext.Current.CancellationToken);

        // GS ! 0x11 is double width AND height; 0x01 keeps the width so the name still fits.
        int headline = IndexOfSequence(transport.LastPayload!, [0x1D, 0x21, 0x11]);
        Assert.Equal(expectsDoubleWidth, headline >= 0);
    }

    /// <summary>Drops the ESC/GS control sequences so only what the paper shows is measured.</summary>
    private static string StripCommands(string payload)
    {
        var text = new StringBuilder();
        for (int i = 0; i < payload.Length; i++)
        {
            char c = payload[i];
            if (c == '\x1B' || c == '\x1D')
            {
                // Every sequence this receipt emits is ESC/GS + selector + at most one argument,
                // except the raster image, whose length is carried in its own header.
                char selector = i + 1 < payload.Length ? payload[i + 1] : '\0';
                if (selector == 'v')
                {
                    int widthBytes = payload[i + 4] | (payload[i + 5] << 8);
                    int height = payload[i + 6] | (payload[i + 7] << 8);
                    i += 7 + widthBytes * height;
                    continue;
                }

                i += selector is '@' ? 1 : 2;
                continue;
            }

            text.Append(c);
        }

        return text.ToString();
    }

    private static int IndexOfSequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle))
                return i;
        }

        return -1;
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
