using POS.Application.DTOs.Quotes;
using POS.Application.Interfaces;
using POS.Application.UseCases.Quotes;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class UpdateQuoteUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsTheCorrectionWithoutIssuingANewQuoteNumber()
    {
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        var stored = SeedDraftQuote(quotes, product);
        Guid quoteId = stored.Id;
        DateTime createdAt = stored.CreatedAt;
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(quotes, new InMemoryProductRepository(product), unitOfWork, SessionWithQuotesEdit());

        QuoteResult result = await useCase.ExecuteAsync(
            new UpdateQuoteRequest(quoteId, "Ana Gomez", null, "ana@example.com", 15, "  Pedido urgente  ", [new CreateQuoteItemRequest(product.Id, 3m, 0m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(quoteId, result.QuoteId);
        Assert.Equal("COT-2026-0001", result.QuoteNumber);
        Assert.Single(quotes.Quotes);
        Assert.Equal(1, unitOfWork.SaveCount);

        Quote reloaded = Assert.Single(quotes.Quotes);
        Assert.Equal("Ana Gomez", reloaded.CustomerName);
        Assert.Equal("ana@example.com", reloaded.CustomerEmail);
        Assert.Equal("Pedido urgente", reloaded.Observations);
        Assert.Equal(15, reloaded.ValidityDays);
        Assert.Equal(createdAt, reloaded.CreatedAt);
        Assert.Equal(createdAt.AddDays(15), reloaded.ExpiresAt);
        Assert.Equal(3m * 1_000m, result.Total);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsLimit_ThrowsAndLeavesTheQuoteUntouched()
    {
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(
            quotes,
            new InMemoryProductRepository(product),
            unitOfWork,
            SessionWithQuotesEdit(),
            new SettingsRepositoryFake(MaximumDiscount: 20m));

        // I5: an edit is not a way around the ceiling the cashier is held to at the till.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(stored.Id, "Ana Gomez", null, null, 30, null, [new CreateQuoteItemRequest(product.Id, 1m, 25m)]),
                TestContext.Current.CancellationToken));

        Assert.Contains("exceeds the configured maximum of 20%", exception.Message);
        Assert.Equal("Test Customer", stored.CustomerName);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_RepricingALineReusesTheSamePresentationResolutionAsCreation()
    {
        // A quote and the invoice it becomes must never disagree on price or pack equivalence, so
        // both use cases resolve lines through QuoteLineBuilder. This pins the edited line to the
        // same box snapshot creation would have taken.
        var product = CreateProduct("Boxed widget", presentations: [("Caja", 6, new Money(5_400m))]);
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        var useCase = new UpdateQuoteUseCase(quotes, new InMemoryProductRepository(product), new InMemoryUnitOfWork(), SessionWithQuotesEdit());

        await useCase.ExecuteAsync(
            new UpdateQuoteRequest(stored.Id, "Test Customer", null, null, 30, null, [new CreateQuoteItemRequest(product.Id, 2m, 0m, "Caja")]),
            TestContext.Current.CancellationToken);

        QuoteItem item = Assert.Single(Assert.Single(quotes.Quotes).Items);
        Assert.Equal("Caja", item.PresentationName);
        Assert.Equal(6, item.UnitsPerPresentation);
        Assert.Equal(5_400m, item.UnitPrice.Amount);
        Assert.Equal(10_800m, Assert.Single(quotes.Quotes).Total);
        // I7: nothing here is a commitment, so stock must be untouched.
        Assert.Equal(20m, product.CurrentStock);
    }

    [Theory]
    [InlineData(QuoteStatus.Cancelled)]
    [InlineData(QuoteStatus.Expired)]
    public async Task ExecuteAsync_WhenQuoteIsFrozen_RejectsBeforeAnyWrite(QuoteStatus status)
    {
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        SetStatus(stored, status);
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(quotes, new InMemoryProductRepository(product), unitOfWork, SessionWithQuotesEdit());

        await Assert.ThrowsAsync<QuoteNotEditableException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(stored.Id, "Ana Gomez", null, null, 60, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]),
                TestContext.Current.CancellationToken));

        Assert.Equal(status, stored.Status);
        Assert.Equal("Test Customer", stored.CustomerName);
        Assert.Equal(30, stored.ValidityDays);
        Assert.Single(stored.Items);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuoteIsConverted_RejectsBeforeAnyWrite()
    {
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        stored.Convert(Guid.NewGuid());
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(quotes, new InMemoryProductRepository(product), unitOfWork, SessionWithQuotesEdit());

        var exception = await Assert.ThrowsAsync<QuoteNotEditableException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(stored.Id, "Ana Gomez", null, null, 60, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]),
                TestContext.Current.CancellationToken));

        Assert.Contains("convertida a venta", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuoteDoesNotExist_Throws()
    {
        var product = CreateProduct("Widget");
        var useCase = new UpdateQuoteUseCase(
            new InMemoryQuoteRepository(),
            new InMemoryProductRepository(product),
            new InMemoryUnitOfWork(),
            SessionWithQuotesEdit());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(Guid.NewGuid(), "Ana Gomez", null, null, 30, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_AppliedThreeTimesInARow_KeepsOneQuoteOneNumberOneCreationDate()
    {
        // Negative control 4.
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        Guid quoteId = stored.Id;
        DateTime createdAt = stored.CreatedAt;
        var useCase = new UpdateQuoteUseCase(quotes, new InMemoryProductRepository(product), new InMemoryUnitOfWork(), SessionWithQuotesEdit());

        await useCase.ExecuteAsync(new UpdateQuoteRequest(quoteId, "Ana", null, null, 10, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]), TestContext.Current.CancellationToken);
        await useCase.ExecuteAsync(new UpdateQuoteRequest(quoteId, "Ana Gomez", null, null, 20, null, [new CreateQuoteItemRequest(product.Id, 2m, 0m)]), TestContext.Current.CancellationToken);
        await useCase.ExecuteAsync(new UpdateQuoteRequest(quoteId, "Ana Gomez Soto", null, null, 45, null, [new CreateQuoteItemRequest(product.Id, 4m, 0m)]), TestContext.Current.CancellationToken);

        Quote quote = Assert.Single(quotes.Quotes);
        Assert.Equal(quoteId, quote.Id);
        Assert.Equal("COT-2026-0001", quote.QuoteNumber);
        Assert.Equal(createdAt, quote.CreatedAt);
        Assert.Equal(createdAt.AddDays(45), quote.ExpiresAt);
        Assert.Equal(4_000m, quote.Total);
    }

    private static Product CreateProduct(string name, (string Name, int Units, Money Price)[]? presentations = null) =>
        new(name, Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m), presentations: presentations);

    private static Quote SeedDraftQuote(InMemoryQuoteRepository quotes, Product product)
    {
        var quote = new Quote("COT-2026-0001", "Test Customer", 30);
        quote.AddItem(new QuoteItem(product.Id, "Widget", new Money(500m), new Quantity(2m), 0m));
        quotes.Add(quote);
        return quote;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTheQuotesEditPermission_RejectsBeforeAnyReadOrWrite()
    {
        // I6 enforced at the trust boundary, not only on the view-model command. QuoteHistoryViewModel
        // hides the edit affordance, but the use case is what writes UnitPrice/Quantity/Discount, so
        // the permission has to be checked here: an affordance is not access control, and any other
        // caller reaching this constructor would otherwise inherit an unauthenticated money write.
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(
            quotes,
            new InMemoryProductRepository(product),
            unitOfWork,
            new SessionFake(isAuthenticated: true, PermissionKeys.Customers.View));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(stored.Id, "Ana Gomez", null, null, 60, null, [new CreateQuoteItemRequest(product.Id, 5m, 0m)]),
                TestContext.Current.CancellationToken));

        // Rejected before the quote was even loaded, and with nothing written.
        Assert.Equal("Test Customer", stored.CustomerName);
        Assert.Equal(30, stored.ValidityDays);
        Assert.Single(stored.Items);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAuthenticatedSession_RejectsTheSameWay()
    {
        // A session that carries the key but was never authenticated is not a grant: the operator is
        // nobody. This is why the check goes through EnsureHasPermission rather than
        // HasPermission alone.
        var product = CreateProduct("Widget");
        var quotes = new InMemoryQuoteRepository();
        Quote stored = SeedDraftQuote(quotes, product);
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new UpdateQuoteUseCase(
            quotes,
            new InMemoryProductRepository(product),
            unitOfWork,
            new SessionFake(isAuthenticated: false, PermissionKeys.Quotes.Edit));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() =>
            useCase.ExecuteAsync(
                new UpdateQuoteRequest(stored.Id, "Ana Gomez", null, null, 60, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]),
                TestContext.Current.CancellationToken));

        Assert.Equal("Test Customer", stored.CustomerName);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private static ICurrentOperatorSession SessionWithQuotesEdit() =>
        new SessionFake(isAuthenticated: true, PermissionKeys.Quotes.Edit);

    private static void SetStatus(Quote quote, QuoteStatus status) =>
        typeof(Quote)
            .GetProperty(nameof(Quote.Status), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
            .SetValue(quote, status);

    private sealed class SessionFake(bool isAuthenticated, params string[] permissions) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username => "cashier";
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(permissions, StringComparer.Ordinal);
        public bool IsAuthenticated => isAuthenticated;
    }

    private sealed class SettingsRepositoryFake(decimal MaximumDiscount) : ISettingsRepository
    {
        private readonly BusinessSettings _settings = new(
            "Test Business",
            null, null, null, null, null, null, null, false, null, null, null, null, null,
            "COP",
            maximumCashierDiscountPercent: MaximumDiscount);

        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<BusinessSettings?>(_settings);
        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProductRepository(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(product.Id == id ? product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(product.Barcode == barcode ? product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        public List<Quote> Quotes { get; } = [];

        public void Add(Quote quote) => Quotes.Add(quote);

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Quotes.FirstOrDefault(q => q.Id == id));

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-2026-9999");

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
            => Task.FromResult<IReadOnlyList<Quote>>([]);
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