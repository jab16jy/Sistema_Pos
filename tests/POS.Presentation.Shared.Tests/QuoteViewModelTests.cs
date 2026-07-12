using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Quotes;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Quotes;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class QuoteViewModelTests
{
    [Fact]
    public async Task Confirm_WithoutCustomerName_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);

        AddCartItem(vm, Guid.NewGuid(), "Producto Test", 1_000m);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Ingresa el nombre del cliente", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_WithoutCartItems_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomerName = "Ana Gómez";

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Agrega al menos un producto", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_WithValidQuote_CreatesQuoteAndClearsCart()
    {
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out _, out _, product);
        vm.CustomerName = "Ana Gómez";
        AddCartItem(vm, product.Id, product.Name, 2_000m);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Contains("creada correctamente", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.CartItems);
        Assert.NotEmpty(vm.LastQuoteNumber);
    }

    [Fact]
    public async Task SendByEmail_WithoutEmail_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomerName = "Ana Gómez";
        AddCartItem(vm, Guid.NewGuid(), "Producto Test", 1_000m);

        await vm.SendByEmailAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Ingresa el email del cliente", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendByWhatsApp_WithoutPhone_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomerName = "Ana Gómez";
        AddCartItem(vm, Guid.NewGuid(), "Producto Test", 1_000m);

        await vm.SendByWhatsAppAsync(TestContext.Current.CancellationToken);

        Assert.Contains("teléfono", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchQuery_WhenChanged_AutocompletesAndShowsStockStatus()
    {
        var product = new Product("Producto Autocomplete", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(3m), new StockQuantity(1m));
        var vm = new QuoteViewModel(
            new StaticProductLookupService(product),
            new CreateQuoteUseCase(new InMemoryQuoteRepository(), new StaticProductRepository(product), new InMemoryUnitOfWork()));

        vm.SearchQuery = "Producto";
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var result = Assert.Single(vm.SearchResults);
        Assert.Equal(product.Name, result.Name);
        Assert.Equal("En stock", result.StockStatusLabel);
    }

    [Fact]
    public async Task SearchCommand_WhenLookupFails_SetsRecoverableValidationMessage()
    {
        var createQuote = new CreateQuoteUseCase(new InMemoryQuoteRepository(), new StaticProductRepository(null), new InMemoryUnitOfWork());
        var vm = new QuoteViewModel(new ThrowingProductLookupService(), createQuote);

        vm.SearchCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al buscar productos", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuoteSummaryRow_WithoutCustomerId_IsNotRegisteredAndHasBadge()
    {
        var summary = new Application.DTOs.Quotes.QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-200",
            CustomerName: "Cliente Casual",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Draft,
            ExpiresAt: "2026-12-31",
            CustomerId: null);

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.False(row.IsRegisteredCustomer);
        Assert.Equal("No registrado", row.CustomerBadgeLabel);
    }

    [Fact]
    public void QuoteSummaryRow_WithCustomerId_IsRegisteredAndHasNoBadge()
    {
        var summary = new Application.DTOs.Quotes.QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-201",
            CustomerName: "Cliente Registrado",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Draft,
            ExpiresAt: "2026-12-31",
            CustomerId: Guid.NewGuid());

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.True(row.IsRegisteredCustomer);
        Assert.Equal(string.Empty, row.CustomerBadgeLabel);
    }

    [Fact]
    public void QuoteSummaryRow_ActiveStatus_CanConvertToSaleAndHasSuccessChip()
    {
        var summary = new Application.DTOs.Quotes.QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-100",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Active,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.True(row.CanConvertToSale);
        Assert.Equal("success", row.StatusChipKind);
    }

    [Fact]
    public void QuoteSummaryRow_DraftStatus_CanConvertToSaleBecauseCreatedQuotesStartAsDraft()
    {
        var summary = new Application.DTOs.Quotes.QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-100-D",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Draft,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.True(row.CanConvertToSale);
        Assert.Equal("warning", row.StatusChipKind);
    }

    [Fact]
    public async Task Confirm_WithSelectedRegisteredCustomer_PersistsCustomerIdOnQuote()
    {
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product);
        var customerId = Guid.NewGuid();

        vm.SelectCustomerCommand.Execute(new CustomerSearchResult(customerId, "Ana Gómez", "123", "3001234567", "ana@example.com", "Main Street", true));
        AddCartItem(vm, product.Id, product.Name, 2_000m);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Domain.Entities.Quote quote = Assert.Single(quoteRepo.Quotes);
        Assert.Equal(customerId, quote.CustomerId);
    }

    [Fact]
    public void QuoteSummaryRow_ConvertedStatus_CannotConvertToSale()
    {
        var summary = new Application.DTOs.Quotes.QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-101",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Converted,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.False(row.CanConvertToSale);
        Assert.Equal("neutral", row.StatusChipKind);
    }

    private static void AddCartItem(QuoteViewModel vm, Guid productId, string name, decimal unitPrice)
        => vm.CartItems.Add(new QuoteCartItemViewModel(productId, name, unitPrice));

    private static QuoteViewModel CreateViewModel(out InMemoryQuoteRepository quoteRepo, out InMemoryUnitOfWork unitOfWork, Product? product = null)
    {
        quoteRepo = new InMemoryQuoteRepository();
        unitOfWork = new InMemoryUnitOfWork();
        var productRepo = new StaticProductRepository(product);
        var createQuote = new CreateQuoteUseCase(quoteRepo, productRepo, unitOfWork);
        var sendDocument = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            new InMemoryDocumentSendLogRepository(),
            unitOfWork);
        return new QuoteViewModel(new EmptyProductLookupService(), createQuote, sendDocument);
    }

    private sealed class EmptyProductLookupService : IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([]);
    }

    private sealed class ThrowingProductLookupService : IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló búsqueda");
    }

    private sealed class StaticProductLookupService(Product product) : IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([
                new ProductLookupResult(product.Id, product.Name, product.Barcode ?? string.Empty, product.SalePrice.Amount, product.CurrentStock, product.MinimumStock)
            ]);
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly Dictionary<Guid, Domain.Entities.Quote> _store = new();

        public Task<Domain.Entities.Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var q) ? q : null);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            _store[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            _store[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Domain.Entities.Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, Domain.Enums.QuoteStatus? status,
            int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Domain.Entities.Quote>, int)>(([.. _store.Values], _store.Count));

        public Task<IReadOnlyList<Domain.Entities.Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Domain.Entities.Quote>>(_store.Values
                .Where(q => q.CustomerId == customerId && q.Status is Domain.Enums.QuoteStatus.Draft or Domain.Enums.QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());

        public IReadOnlyCollection<Domain.Entities.Quote> Quotes => _store.Values;
    }

    private sealed class StaticProductRepository : IProductRepository
    {
        private readonly Product? _product;

        public StaticProductRepository(Product? product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_product is not null && id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class InMemoryDocumentSendLogRepository : IDocumentSendLogRepository
    {
        public Task AddAsync(DocumentSendLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentSendLog>>([]);

        public Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<DocumentSendLog?>(null);
    }

    private sealed class FakePdfService : IPdfService
    {
        public Task<byte[]> GenerateQuotePdfAsync(Guid quoteId, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task<bool> SendAsync(string to, string subject, string body, byte[] attachment, string attachmentName, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class FakeWhatsAppService : IWhatsAppService
    {
        public Task<bool> SendAsync(string phoneNumber, string message, string? attachmentPath, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
