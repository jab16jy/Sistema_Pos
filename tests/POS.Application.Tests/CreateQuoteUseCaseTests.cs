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
