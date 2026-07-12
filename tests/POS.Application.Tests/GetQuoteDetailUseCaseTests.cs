using POS.Application.DTOs.Quotes;
using POS.Application.UseCases.Quotes;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class GetQuoteDetailUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithExistingQuote_ReturnsDetailWithLineItems()
    {
        var quote = new Quote("COT-300", "Cliente Detalle", 30, customerId: Guid.NewGuid());
        var productId = Guid.NewGuid();
        quote.AddItem(new QuoteItem(productId, "Producto A", new Money(1_000m), new Quantity(3m), 10m));
        var quotes = new InMemoryQuoteRepository([quote]);
        var useCase = new GetQuoteDetailUseCase(quotes);

        QuoteDetailResult? result = await useCase.ExecuteAsync(quote.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("COT-300", result!.QuoteNumber);
        Assert.NotNull(result.CustomerId);
        Assert.Single(result.Items);
        Assert.Equal("Producto A", result.Items[0].ProductName);
        Assert.Equal(2_700m, result.Items[0].Subtotal);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownQuote_ReturnsNull()
    {
        var quotes = new InMemoryQuoteRepository([]);
        var useCase = new GetQuoteDetailUseCase(quotes);

        QuoteDetailResult? result = await useCase.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly List<Quote> _quotes;

        public InMemoryQuoteRepository(List<Quote> quotes) => _quotes = quotes;

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_quotes.FirstOrDefault(q => q.Id == id));

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status,
            int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Quote>, int)>(([.. _quotes], _quotes.Count));

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Quote>>(_quotes
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
    }
}
