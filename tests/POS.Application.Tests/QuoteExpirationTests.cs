using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class QuoteExpirationTests
{
    private static void SetQuoteExpiresAt(Quote quote, DateTime expiresAt)
    {
        var property = typeof(Quote).GetProperty(nameof(Quote.ExpiresAt));
        if (property != null)
        {
            property.SetValue(quote, expiresAt);
        }
    }

    private static void SetQuoteStatus(Quote quote, QuoteStatus status)
    {
        var property = typeof(Quote).GetProperty(nameof(Quote.Status));
        if (property != null)
        {
            property.SetValue(quote, status);
        }
    }

    [Fact]
    public async Task GetCustomerPayableQuotesUseCase_CorrectlyFiltersByUtc5Expiration()
    {
        // GIVEN
        var customerId = Guid.NewGuid();

        // Colombian today's date at 00:00:00 (represented in UTC, i.e., +5 hours)
        var colombianTodayUtc = DateTime.UtcNow.AddHours(-5).Date.AddHours(5);

        var expiredQuote = new Quote("COT-EXP", "Customer", 5, customerId);
        SetQuoteExpiresAt(expiredQuote, colombianTodayUtc.AddDays(-1));
        SetQuoteStatus(expiredQuote, QuoteStatus.Active);

        var activeQuoteToday = new Quote("COT-TODAY", "Customer", 5, customerId);
        SetQuoteExpiresAt(activeQuoteToday, colombianTodayUtc);
        SetQuoteStatus(activeQuoteToday, QuoteStatus.Active);

        var activeQuoteTomorrow = new Quote("COT-TOMORROW", "Customer", 5, customerId);
        SetQuoteExpiresAt(activeQuoteTomorrow, colombianTodayUtc.AddDays(1));
        SetQuoteStatus(activeQuoteTomorrow, QuoteStatus.Active);

        var quoteRepo = new FakeQuoteRepository(new[] { expiredQuote, activeQuoteToday, activeQuoteTomorrow });
        var useCase = new GetCustomerPayableQuotesUseCase(quoteRepo);

        // WHEN
        var result = await useCase.ExecuteAsync(customerId, CancellationToken.None);

        // THEN
        Assert.Equal(2, result.Count);
        Assert.Contains(result, q => q.QuoteNumber == "COT-TODAY");
        Assert.Contains(result, q => q.QuoteNumber == "COT-TOMORROW");
        Assert.DoesNotContain(result, q => q.QuoteNumber == "COT-EXP");
    }

    [Fact]
    public async Task ConvertQuoteToSaleUseCase_AllowsConversionOnExpirationDateInUtc5()
    {
        // GIVEN
        var customerId = Guid.NewGuid();
        var colombianTodayUtc = DateTime.UtcNow.AddHours(-5).Date.AddHours(5);

        var quote = new Quote("COT-TODAY", "Customer", 5, customerId);
        SetQuoteExpiresAt(quote, colombianTodayUtc);
        SetQuoteStatus(quote, QuoteStatus.Active);

        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(1_500m), new StockQuantity(100m), new StockQuantity(5m));
        quote.AddItem(new QuoteItem(product.Id, product.Name, new Money(1_500m), new Quantity(2m), 0m));

        var quoteRepo = new FakeQuoteRepository(new[] { quote });
        var productRepo = new FakeProductRepository(new[] { product });
        var saleRepo = new FakeSaleRepository();
        var uow = new FakeUnitOfWork();
        var createSale = new CreateSaleUseCase(productRepo, saleRepo, uow, authorizer: TestAuthorizer.Create());
        var useCase = new ConvertQuoteToSaleUseCase(quoteRepo, createSale, uow);

        var request = new ConvertQuoteToSaleRequest(
            quote.Id,
            new[] { new CreateSalePaymentRequest(PaymentMethod.Cash, 3_000m, 3_000m, null) });

        // WHEN
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        // THEN
        Assert.NotNull(result);
        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Single(saleRepo.Sales);
        Assert.True(saleRepo.Sales[0].Items.All(i => i.ProductId == product.Id));
    }

    [Fact]
    public async Task ConvertQuoteToSaleUseCase_RejectsConversionIfExpiredYesterdayInUtc5()
    {
        // GIVEN
        var customerId = Guid.NewGuid();
        var colombianTodayUtc = DateTime.UtcNow.AddHours(-5).Date.AddHours(5);

        var quote = new Quote("COT-EXP", "Customer", 5, customerId);
        SetQuoteExpiresAt(quote, colombianTodayUtc.AddDays(-1));
        SetQuoteStatus(quote, QuoteStatus.Active);

        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(1_500m), new StockQuantity(100m), new StockQuantity(5m));
        quote.AddItem(new QuoteItem(product.Id, product.Name, new Money(1_500m), new Quantity(2m), 0m));

        var quoteRepo = new FakeQuoteRepository(new[] { quote });
        var productRepo = new FakeProductRepository(new[] { product });
        var saleRepo = new FakeSaleRepository();
        var uow = new FakeUnitOfWork();
        var createSale = new CreateSaleUseCase(productRepo, saleRepo, uow, authorizer: TestAuthorizer.Create());
        var useCase = new ConvertQuoteToSaleUseCase(quoteRepo, createSale, uow);

        var request = new ConvertQuoteToSaleRequest(
            quote.Id,
            new[] { new CreateSalePaymentRequest(PaymentMethod.Cash, 3_000m, 3_000m, null) });

        // WHEN & THEN
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(request, CancellationToken.None));
        Assert.Contains("vencida", exception.Message);
        Assert.Equal(QuoteStatus.Expired, quote.Status);
    }

    [Fact]
    public async Task CancelQuoteUseCase_CancelsActiveQuote()
    {
        var quote = new Quote("COT-CANCEL", "Customer", 5, Guid.NewGuid());
        var quoteRepo = new FakeQuoteRepository(new[] { quote });
        var uow = new FakeUnitOfWork();
        var useCase = new CancelQuoteUseCase(quoteRepo, uow);

        await useCase.ExecuteAsync(quote.Id, CancellationToken.None);

        Assert.Equal(QuoteStatus.Cancelled, quote.Status);
    }

    private sealed class FakeQuoteRepository : IQuoteRepository
    {
        private readonly List<Quote> _quotes;

        public FakeQuoteRepository(IEnumerable<Quote> quotes)
        {
            _quotes = quotes.ToList();
        }

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_quotes.FirstOrDefault(q => q.Id == id));
        }

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
        {
            var list = _quotes.Where(q => q.CustomerId == customerId && (q.Status == QuoteStatus.Draft || q.Status == QuoteStatus.Active)).ToList();
            return Task.FromResult<IReadOnlyList<Quote>>(list);
        }

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default) => Task.FromResult("COT-0001");
        public Task AddAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status, int page, int pageSize, CancellationToken ct = default)
        {
            return Task.FromResult<(IReadOnlyList<Quote>, int)>((_quotes, _quotes.Count));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public FakeProductRepository(IEnumerable<Product> products)
        {
            _products = products.ToList();
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.FirstOrDefault(p => p.Id == id));
        }

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<IReadOnlyList<Product>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));
        }

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await operation(cancellationToken);
        }
    }
}
