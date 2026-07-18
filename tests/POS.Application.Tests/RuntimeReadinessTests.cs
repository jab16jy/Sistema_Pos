using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests;

public sealed class RuntimeReadinessTests
{
    [Fact]
    public void AddPosInfrastructure_RegistersRuntimeReadinessContracts()
    {
        var services = new ServiceCollection();

        services.AddPosInfrastructure("Data Source=:memory:");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWhatsAppService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPrintService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductLookupService>());
    }

    [Fact]
    public async Task MigrateAsync_AppliesSmtpSettingsMigration_SoBusinessSettingsIsQueryable()
    {
        // Guards against the PersistSmtpSettings migration losing its [Migration] designer:
        // if EF stops discovering it, MigrateAsync won't create the SMTP columns and this query throws.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var dbContext = new AppDbContext(options);

        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Selects SmtpHost & friends; throws "no such column" if the migration was skipped.
        BusinessSettings? settings = await dbContext.BusinessSettings.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(settings);
    }

    [Fact]
    public async Task MigrateAsync_AppliesLoyaltyCompensationAndTierForegroundMigrationToExistingTier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var dbContext = new AppDbContext(options);

        await dbContext.Database.MigrateAsync("20260713063301_AddLoyaltyTierColor", TestContext.Current.CancellationToken);
        Guid tierId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO LOYALTY_TIERS (Id, Name, MinPoints, DiscountPercent, PointsMultiplier, Color) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
            [tierId.ToString(), "Existing", "0.0", "0.0", "1.0", "#64748B"],
            TestContext.Current.CancellationToken);

        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        LoyaltyTier tier = await dbContext.LoyaltyTiers.SingleAsync(item => item.Id == tierId, TestContext.Current.CancellationToken);
        Assert.Equal(LoyaltyTierTextForeground.White, tier.TextForeground);
    }

    [Fact]
    public async Task SmtpEmailService_WhenConfigurationIsMissing_FailsWithRecoverableMessage()
    {
        var service = new SmtpEmailService(new SmtpRuntimeOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(
                "client@example.com",
                "Subject",
                "Body",
                [1, 2, 3],
                "document.pdf",
                TestContext.Current.CancellationToken));

        Assert.Contains("SMTP", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppDesktopWebService_WhenNumberIsInvalid_FailsWithRecoverableMessage()
    {
        var service = new WhatsAppDesktopWebService(new FakeExternalLinkLauncher());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(
                "12",
                "Documento",
                null,
                TestContext.Current.CancellationToken));

        Assert.Contains("WhatsApp", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppDesktopWebService_FallsBackToWebAndRevealsAttachment()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "pos-whatsapp-test-" + Guid.NewGuid());
        string documentsDirectory = Path.Combine(tempDirectory, "Documents");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(documentsDirectory);
        string attachmentPath = Path.Combine(tempDirectory, "invoice.pdf");
        await File.WriteAllTextAsync(attachmentPath, "pdf", TestContext.Current.CancellationToken);
        var launcher = new FakeExternalLinkLauncher(desktopResult: false, webResult: true);
        var service = new WhatsAppDesktopWebService(launcher, () => documentsDirectory);

        try
        {
            bool success = await service.SendAsync(
                "+57 300 123 4567",
                "Documento listo",
                attachmentPath,
                TestContext.Current.CancellationToken);

            Assert.True(success);
            Assert.Contains("whatsapp://send/", launcher.AttemptedUris[0].ToString(), StringComparison.Ordinal);
            Assert.Contains("phone=573001234567", launcher.AttemptedUris[0].ToString(), StringComparison.Ordinal);
            Assert.Contains("https://wa.me/573001234567?text=", launcher.AttemptedUris[1].ToString(), StringComparison.Ordinal);
            string expectedDocumentPath = Path.Combine(documentsDirectory, "POS WhatsApp", "invoice.pdf");
            Assert.Equal(expectedDocumentPath, launcher.RevealedFile);
            Assert.True(File.Exists(expectedDocumentPath));
            // The local file path must never leak into the customer-facing message (it exposed
            // the Windows username/folder structure); RevealedFile above confirms the operator
            // still gets the file opened locally to attach manually.
            Assert.DoesNotContain("POS%20WhatsApp", launcher.AttemptedUris[1].OriginalString, StringComparison.Ordinal);
            Assert.DoesNotContain("invoice.pdf", launcher.AttemptedUris[1].OriginalString, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WhatsAppDesktopWebService_WhenDocumentsCopyFails_KeepsOriginalAttachmentPath()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "pos-whatsapp-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDirectory);
        string attachmentPath = Path.Combine(tempDirectory, "invoice.pdf");
        string unavailableDocumentsPath = Path.Combine(tempDirectory, "documents-file");
        await File.WriteAllTextAsync(attachmentPath, "pdf", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(unavailableDocumentsPath, "not a directory", TestContext.Current.CancellationToken);

        try
        {
            string? preparedPath = WhatsAppDesktopWebService.PrepareAttachmentForManualAttach(
                attachmentPath,
                unavailableDocumentsPath);

            Assert.Equal(attachmentPath, preparedPath);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SmtpEmailService_UsesPersistedSettingsWhenRuntimeFallbackIsMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        dbContext.BusinessSettings.Add(new BusinessSettings(
            "Store",
            null,
            null,
            null,
            "smtp.persisted.local",
            2525,
            null,
            null,
            false,
            "ventas@store.local",
            null,
            null,
            null,
            null,
            "COP"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new SmtpEmailService(new SmtpRuntimeOptions(), dbContext);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync("client@example.com", "Subject", "Body", [1], "doc.pdf", TestContext.Current.CancellationToken));

        Assert.Contains("SMTP", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("smtp.persisted.local", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BusinessSettings_PersistsSmtpPasswordProtectedAndReadsItBack()
    {
        const string dummyPassword = "unit-test-app-password";
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using (var dbContext = CreateDbContext(connection))
        {
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            dbContext.BusinessSettings.Add(new BusinessSettings(
                "Store",
                null,
                null,
                null,
                "smtp.gmail.com",
                587,
                "mailer@store.local",
                dummyPassword,
                true,
                "mailer@store.local",
                null,
                null,
                null,
                null,
                "COP"));
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        string? storedPassword;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SmtpPassword FROM BUSINESS_SETTINGS LIMIT 1";
            storedPassword = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.NotEqual(dummyPassword, storedPassword);
            Assert.StartsWith("dpapi:v1:", storedPassword, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(dummyPassword, storedPassword);
        }

        await using var readContext = CreateDbContext(connection);
        BusinessSettings settings = await readContext.BusinessSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(dummyPassword, settings.SmtpPassword);
    }

    [Fact]
    public async Task BusinessSettings_ReadsLegacyPlaintextSmtpPassword()
    {
        const string legacyPassword = "legacy-test-password";
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BUSINESS_SETTINGS
                    (Id, BusinessName, SmtpHost, SmtpPort, SmtpUser, SmtpPassword, SmtpUseSsl, SmtpSenderAddress, CurrencyCode)
                VALUES
                    ($id, $businessName, $host, $port, $user, $password, $ssl, $sender, $currency)
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$businessName", "Store");
            command.Parameters.AddWithValue("$host", "smtp.gmail.com");
            command.Parameters.AddWithValue("$port", 587);
            command.Parameters.AddWithValue("$user", "mailer@store.local");
            command.Parameters.AddWithValue("$password", legacyPassword);
            command.Parameters.AddWithValue("$ssl", true);
            command.Parameters.AddWithValue("$sender", "mailer@store.local");
            command.Parameters.AddWithValue("$currency", "COP");
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        BusinessSettings settings = await dbContext.BusinessSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(legacyPassword, settings.SmtpPassword);
    }

    [Fact]
    public async Task SmtpEmailService_LiveGmailSend_UsesPersistedUiConfigurationWhenExplicitlyEnabled()
    {
        string? recipient = Environment.GetEnvironmentVariable("POS_SMTP_LIVE_TEST_TO");
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PosLocal",
            "pos.db");

        Assert.True(File.Exists(dbPath), "No se encontró la base local de la app para leer la configuración SMTP persistida.");

        await using var dbContext = CreateDbContext(new SqliteConnection($"Data Source={dbPath}"));
        BusinessSettings? settings = await dbContext.BusinessSettings.AsNoTracking().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(settings);
        Assert.Equal("smtp.gmail.com", settings.SmtpHost);
        Assert.Equal(587, settings.SmtpPort);
        Assert.True(settings.SmtpUseSsl);
        Assert.False(string.IsNullOrWhiteSpace(settings.SmtpUser));
        Assert.False(string.IsNullOrWhiteSpace(settings.SmtpPassword));
        Assert.False(string.IsNullOrWhiteSpace(settings.SmtpSenderAddress));

        var service = new SmtpEmailService(new SmtpRuntimeOptions(), dbContext);

        bool sent = await service.SendAsync(
            recipient,
            "Prueba SMTP Gmail — Esthetic Medical Store",
            "Este correo confirma que la configuración SMTP persistida en Esthetic Medical Store puede enviar con Gmail.",
            [],
            "prueba.txt",
            TestContext.Current.CancellationToken);

        Assert.True(sent);
    }

    [Fact]
    public async Task ProductLookupService_SearchAsync_ReturnsActivePersistedProductByNameOrBarcode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product active = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Vitamin C", new Money(8_000m), new Money(12_000m), new StockQuantity(7m), new StockQuantity(2m), TestContext.Current.CancellationToken, new Barcode("VITC-001"));
        Product inactive = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Vitamin D", new Money(8_000m), new Money(12_000m), new StockQuantity(7m), new StockQuantity(2m), TestContext.Current.CancellationToken, new Barcode("VITD-001"));
        inactive.Deactivate();

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProductLookupService(dbContext);

        var nameResults = await service.SearchAsync("Vitamin", TestContext.Current.CancellationToken);
        var barcodeResults = await service.SearchAsync("VITC-001", TestContext.Current.CancellationToken);

        var result = Assert.Single(nameResults);
        Assert.Equal(active.Id, result.ProductId);
        Assert.Equal("Vitamin C", result.Name);
        Assert.Equal("VITC-001", result.Barcode);
        Assert.Equal(12_000m, result.UnitPrice);
        Assert.Equal(7m, result.AvailableStock);
        Assert.Equal(active.Id, Assert.Single(barcodeResults).ProductId);
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        private readonly bool _desktopResult;
        private readonly bool _webResult;

        public FakeExternalLinkLauncher(bool desktopResult = true, bool webResult = true)
        {
            _desktopResult = desktopResult;
            _webResult = webResult;
        }

        public List<Uri> AttemptedUris { get; } = [];
        public string? RevealedFile { get; private set; }

        public bool TryOpen(Uri uri)
        {
            AttemptedUris.Add(uri);
            return uri.Scheme == "whatsapp" ? _desktopResult : _webResult;
        }

        public void RevealFile(string filePath)
        {
            RevealedFile = filePath;
        }
    }

    [Fact]
    public async Task ReportService_GenerateDailySalesReportAsync_ReturnsNonEmptyPdfBytes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Serum", new Money(20_000m), new Money(35_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            authorizer: TestAuthorizer.Create(dbContext: dbContext));
        await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 35_000m, 40_000m, null)]),
            TestContext.Current.CancellationToken);

        var service = new ReportService(dbContext);

        byte[] bytes = await service.GenerateDailySalesReportAsync(
            DateOnly.FromDateTime(DateTime.UtcNow),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public async Task PdfService_GenerateInvoicePdfAsync_ReturnsNonEmptyStyledInvoicePdfBytes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        dbContext.BusinessSettings.Add(new BusinessSettings(
            "Esthetic Medical Store",
            "Calle 123 #45-67",
            "900123456-7",
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            "COP"));

        Product product = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Serum Premium", new Money(20_000m), new Money(45_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            authorizer: TestAuthorizer.Create(dbContext: dbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 45_000m, 50_000m, null)]),
            TestContext.Current.CancellationToken);

        var service = new PdfService(dbContext);

        byte[] bytes = await service.GenerateInvoicePdfAsync(sale.SaleId, TestContext.Current.CancellationToken);

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 1_000);
    }

    [Fact]
    public async Task PdfService_GeneratesInvoiceAndQuoteWithDiscountsLoyaltyTierAndPointRedemption()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var tier = new LoyaltyTier("Oro", 100m, 10m);
        var customer = new Customer("Cliente Leal", "123456789");
        customer.SetLoyaltyTier(tier.Id);
        dbContext.AddRange(tier, customer);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Serum Premium", new Money(20_000m), new Money(45_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var quote = new Quote("COT-001", customer.FullName, 15, customer.Id, "3001234567", "cliente@example.com");
        quote.AddItem(new QuoteItem(product.Id, product.Name, new Money(45_000m), new Quantity(1m), 10m));
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        var sale = await saleUseCase.ExecuteAsync(
            new CreateSaleRequest(
                customer.Id,
                [new CreateSaleItemRequest(product.Id, 1m, 4_500m)],
                [
                    new CreateSalePaymentRequest(PaymentMethod.Cash, 36_000m, 40_000m, null),
                    new CreateSalePaymentRequest(PaymentMethod.LoyaltyPoints, 4_500m, null, null)
                ]),
            TestContext.Current.CancellationToken);

        var service = new PdfService(dbContext);
        byte[] invoice = await service.GenerateInvoicePdfAsync(sale.SaleId, TestContext.Current.CancellationToken);
        byte[] quotePdf = await service.GenerateQuotePdfAsync(quote.Id, TestContext.Current.CancellationToken);

        Assert.Equal((byte)'%', invoice[0]);
        Assert.Equal((byte)'%', quotePdf[0]);
        Assert.True(invoice.Length > 1_000);
        Assert.True(quotePdf.Length > 1_000);
    }

    [Fact]
    public async Task PrintService_WhenPrinterIsMissing_FailsWithRecoverableMessage()
    {
        var service = new PrintService(new PrinterRuntimeOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrintReceiptAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Contains("impresora", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not implemented", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
