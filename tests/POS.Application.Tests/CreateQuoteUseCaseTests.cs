using POS.Application.DTOs.Quotes;
using POS.Application.UseCases.Quotes;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class CreateQuoteUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidRequest_CreatesQuoteAndReturnsResult()
    {
        var product = new Product(
            "Widget",
            Guid.NewGuid(),
            new Money(500m),
            new Money(1_000m),
            new StockQuantity(20m),
            new StockQuantity(2m));

        var products = new InMemoryProductRepository(product);
        var quotes = new InMemoryQuoteRepository("COT-2026-0001");
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new CreateQuoteUseCase(quotes, products, unitOfWork);

        var result = await useCase.ExecuteAsync(new CreateQuoteRequest(
            null,
            "Test Customer",
            null,
            null,
            30,
            null,
            [new CreateQuoteItemRequest(product.Id, 2m, 0m)]),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.QuoteId);
        Assert.Equal("COT-2026-0001", result.QuoteNumber);
        Assert.Single(quotes.Quotes);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownProduct_ThrowsInvalidOperationException()
    {
        var products = new InMemoryProductRepository(null);
        var quotes = new InMemoryQuoteRepository("COT-2026-0001");
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new CreateQuoteUseCase(quotes, products, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(new CreateQuoteRequest(
                null,
                "Test Customer",
                null,
                null,
                30,
                null,
                [new CreateQuoteItemRequest(Guid.NewGuid(), 1m, 0m)]),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_WithBoxPresentation_SnapshotsBoxPriceAndEquivalenceWithoutChangingStock()
    {
        var product = new Product(
            "Boxed widget",
            Guid.NewGuid(),
            new Money(500m),
            new Money(1_000m),
            new StockQuantity(20m),
            new StockQuantity(2m),
            presentations: [("Caja", 6, new Money(5_400m))]);
        var products = new InMemoryProductRepository(product);
        var quotes = new InMemoryQuoteRepository("COT-2026-0002");
        var useCase = new CreateQuoteUseCase(quotes, products, new InMemoryUnitOfWork());

        await useCase.ExecuteAsync(new CreateQuoteRequest(
            null,
            "Test Customer",
            null,
            null,
            30,
            null,
            [new CreateQuoteItemRequest(product.Id, 2m, 0m, "Caja")]),
            TestContext.Current.CancellationToken);

        QuoteItem item = Assert.Single(Assert.Single(quotes.Quotes).Items);
        Assert.Equal("Caja", item.PresentationName);
        Assert.Equal(6, item.UnitsPerPresentation);
        Assert.Equal(5_400m, item.UnitPrice.Amount);
        Assert.Equal(20m, product.CurrentStock);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsLimit_Throws()
    {
        var product = new Product("Widget", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var quotes = new InMemoryQuoteRepository("COT-2026-0003");
        var unitOfWork = new InMemoryUnitOfWork();

        var settings = new BusinessSettings(
            "Test Business",
            null, null, null, null, null, null, null, false, null, null, null, null, null,
            "COP",
            maximumCashierDiscountPercent: 20m);
        var settingsRepo = new SettingsRepositoryFake(settings);
        var useCase = new CreateQuoteUseCase(quotes, products, unitOfWork, settingsRepo);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(new CreateQuoteRequest(
                null,
                "Test Customer",
                null,
                null,
                30,
                null,
                [new CreateQuoteItemRequest(product.Id, 2m, 25m)]),
                TestContext.Current.CancellationToken));

        Assert.Contains("exceeds the configured maximum of 20%", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountWithinLimit_CreatesQuoteWithDiscount()
    {
        var product = new Product("Widget", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var quotes = new InMemoryQuoteRepository("COT-2026-0004");
        var unitOfWork = new InMemoryUnitOfWork();

        var settings = new BusinessSettings(
            "Test Business",
            null, null, null, null, null, null, null, false, null, null, null, null, null,
            "COP",
            maximumCashierDiscountPercent: 20m);
        var settingsRepo = new SettingsRepositoryFake(settings);
        var useCase = new CreateQuoteUseCase(quotes, products, unitOfWork, settingsRepo);

        await useCase.ExecuteAsync(new CreateQuoteRequest(
            null,
            "Test Customer",
            null,
            null,
            30,
            null,
            [new CreateQuoteItemRequest(product.Id, 2m, 15m)]),
            TestContext.Current.CancellationToken);

        QuoteItem item = Assert.Single(Assert.Single(quotes.Quotes).Items);
        Assert.Equal(15m, item.Discount);
    }

    [Fact]
    public async Task ExecuteAsync_ForCustomerWithTierDiscount_PersistsOnlyManualDiscountNotTier()
    {
        // Regression guard: CreateQuoteUseCase must never resolve/inject the customer's loyalty
        // tier discount, even when a CustomerId is supplied — quote discounts are manual-only by
        // design (see the comment in CreateQuoteUseCase.ExecuteAsync). If this test starts
        // failing because someone wired ILoyaltyRepository in, that's a deliberate design change,
        // not a bug fix — update the comment there consciously before touching this assertion.
        var product = new Product("Widget", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var quotes = new InMemoryQuoteRepository("COT-2026-0005");
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new CreateQuoteUseCase(quotes, products, unitOfWork);

        var customerId = Guid.NewGuid();
        await useCase.ExecuteAsync(new CreateQuoteRequest(
            customerId,
            "Tiered Customer",
            null,
            null,
            30,
            null,
            [new CreateQuoteItemRequest(product.Id, 2m, 10m)]),
            TestContext.Current.CancellationToken);

        QuoteItem item = Assert.Single(Assert.Single(quotes.Quotes).Items);
        Assert.Equal(10m, item.Discount);
    }

    private sealed class SettingsRepositoryFake : ISettingsRepository
    {
        private readonly BusinessSettings _settings;
        public SettingsRepositoryFake(BusinessSettings settings) => _settings = settings;
        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<BusinessSettings?>(_settings);
        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product? _product;

        public InMemoryProductRepository(Product? product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_product?.Id == id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_product?.Barcode == barcode ? _product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly string _nextQuoteNumber;
        public List<Quote> Quotes { get; } = [];

        public InMemoryQuoteRepository(string nextQuoteNumber) => _nextQuoteNumber = nextQuoteNumber;

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Quotes.FirstOrDefault(q => q.Id == id));

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult(_nextQuoteNumber);

        public Task AddAsync(Quote quote, CancellationToken ct = default)
        {
            Quotes.Add(quote);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from,
            DateOnly? to,
            string? customerName,
            QuoteStatus? status,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            IReadOnlyList<Quote> items = [.. Quotes];
            return Task.FromResult<(IReadOnlyList<Quote> Items, int Total)>((items, Quotes.Count));
        }

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Quote>>(Quotes
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
