using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Quotes;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class QuoteHistoryViewModelTests
{
    // --- 3.1a: QuoteSummaryRow carries customer email and phone from QuoteSummary ---

    [Fact]
    public void QuoteSummaryRow_FromSummary_IncludesEmailAndPhone()
    {
        var summary = new QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-001",
            CustomerName: "Ana López",
            CustomerEmail: "ana@example.com",
            CustomerPhone: "+57 300 000 0001",
            Total: 100_000m,
            Status: QuoteStatus.Active,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.Equal("ana@example.com", row.CustomerEmail);
        Assert.Equal("+57 300 000 0001", row.CustomerPhone);
    }

    [Fact]
    public void QuoteSummaryRow_FromSummary_WhenNoContactData_HasNullContactFields()
    {
        var summary = new QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-002",
            CustomerName: "Sin Contacto",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 50_000m,
            Status: QuoteStatus.Draft,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.Null(row.CustomerEmail);
        Assert.Null(row.CustomerPhone);
    }

    // --- 3.1b: Send blocked when no contact data ---

    [Fact]
    public async Task SendByEmail_WhenNoCustomerEmail_SetsBlockedStatusWithoutCallingUseCase()
    {
        var sendLog = new InMemoryDocumentSendLogRepository();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(Guid.NewGuid(), "COT-003", "Sin Email", null, "+57 300 000 0000", 80_000m, QuoteStatus.Active, "2026-12-31")
            ],
            sendLog: sendLog);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.SendByEmailAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(0, sendLog.LogCount);
        Assert.Contains("correo", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendByWhatsApp_WhenNoCustomerPhone_SetsBlockedStatusWithoutCallingUseCase()
    {
        var sendLog = new InMemoryDocumentSendLogRepository();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(Guid.NewGuid(), "COT-004", "Sin Tel", "test@example.com", null, 80_000m, QuoteStatus.Active, "2026-12-31")
            ],
            sendLog: sendLog);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.SendByWhatsAppAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(0, sendLog.LogCount);
        Assert.Contains("teléfono", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- 3.1c: Send succeeds when contact data present ---

    [Fact]
    public async Task SendByEmail_WhenEmailPresent_CallsUseCaseAndUpdatesStatus()
    {
        var sendLog = new InMemoryDocumentSendLogRepository();
        var quoteId = Guid.NewGuid();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(quoteId, "COT-005", "Cliente OK", "ok@example.com", "+57 300 000 0002", 90_000m, QuoteStatus.Active, "2026-12-31")
            ],
            sendLog: sendLog);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.SendByEmailAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(1, sendLog.LogCount);
        Assert.Equal("ok@example.com", sendLog.LastRecipient);
        Assert.Contains("enviado", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- 3.1d: Eligible quote conversion drives state ---

    [Fact]
    public async Task ConvertToSale_WhenEligibleActiveQuote_CallsUseCaseAndUpdatesStatus()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-006", product.Id);
        quoteRepo.Store(quote);

        // Build a summary that references the actual quote Id so ConvertQuoteToSaleUseCase can resolve it
        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-006", "Cliente Convertible", "convert@example.com", null, 50_000m, QuoteStatus.Active, "2026-12-31")
        };

        var vm = BuildHistoryVmWithProduct(summaries: summaries, quoteRepo: quoteRepo, product: product);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.ConvertToSaleAsync(row, cashierUserId: Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Contains("POS-", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToSale_WhenAlreadyConverted_DoesNotCallUseCaseAndShowsMessage()
    {
        var quoteId = Guid.NewGuid();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(quoteId, "COT-007", "Cliente Ya Conv.", null, null, 30_000m, QuoteStatus.Converted, "2026-12-31")
            ]);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.ConvertToSaleAsync(row, cashierUserId: Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Contains("ya fue convertida", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToSaleCommand_UsesAuthenticatedOperatorSessionInsteadOfEmptyGuid()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var saleRepo = new InMemorySaleRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-008", product.Id);
        quoteRepo.Store(quote);
        var cashierId = Guid.NewGuid();
        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-008", "Cliente Convertible", null, null, 2_000m, QuoteStatus.Draft, "2026-12-31")
        };

        var vm = BuildHistoryVmWithProduct(summaries, quoteRepo, product, saleRepo, new FakeOperatorSession(cashierId));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.ConvertToSaleCommand.Execute(vm.Quotes.Single());
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(cashierId, saleRepo.LastSale?.CashierUserId);
    }

    [Fact]
    public async Task ConvertToSale_WithEmptyCashierId_BlocksBeforeCreatingSale()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-009", product.Id);
        quoteRepo.Store(quote);
        var vm = BuildHistoryVmWithProduct(
            [new QuoteSummary(quote.Id, "COT-009", "Cliente", null, null, 2_000m, QuoteStatus.Draft, "2026-12-31")],
            quoteRepo,
            product);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        await vm.ConvertToSaleAsync(vm.Quotes.Single(), Guid.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("cajero autenticado", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportPdfAsync_WhenPdfServiceAvailable_RaisesFileExportRequested()
    {
        var quoteId = Guid.NewGuid();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(quoteId, "COT-011", "Cliente PDF", null, null, 40_000m, QuoteStatus.Draft, "2026-12-31")
            ],
            pdfService: new FakePdfService());
        (string FileName, byte[] Data)? exported = null;
        vm.FileExportRequested += (_, args) => exported = args;
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        await vm.ExportPdfAsync(vm.Quotes.Single(), TestContext.Current.CancellationToken);

        Assert.Equal("COT-011.pdf", exported?.FileName);
        Assert.NotNull(exported?.Data);
    }

    // --- Helpers ---

    private static Domain.Entities.Quote CreateActiveQuote(string quoteNumber)
    {
        return new Domain.Entities.Quote(
            quoteNumber,
            "Cliente Convertible",
            30,
            customerId: null,
            customerPhone: null,
            customerEmail: "convert@example.com");
    }

    private static Domain.Entities.Quote CreateActiveQuoteWithItem(string quoteNumber, Guid productId)
    {
        var quote = new Domain.Entities.Quote(
            quoteNumber,
            "Cliente Convertible",
            30,
            customerId: null,
            customerPhone: null,
            customerEmail: "convert@example.com");
        quote.AddItem(new Domain.Entities.QuoteItem(
            productId,
            "Producto Test",
            new Money(2_000m),
            new Quantity(1m),
            0m));
        return quote;
    }

    private static QuoteHistoryViewModel BuildHistoryVmWithProduct(
        IReadOnlyList<QuoteSummary> summaries,
        InMemoryQuoteRepository quoteRepo,
        Product product,
        InMemorySaleRepository? saleRepo = null,
        ICurrentOperatorSession? session = null)
    {
        var log = new InMemoryDocumentSendLogRepository();
        var quoteEntities = BuildQuoteEntitiesFromSummaries(summaries, quoteRepo);
        var historyRepo = new StaticQuoteHistoryRepository(quoteEntities);

        var getHistoryUseCase = new GetQuoteHistoryUseCase(historyRepo);
        var convertUseCase = new ConvertQuoteToSaleUseCase(
            quoteRepo,
            new CreateSaleUseCase(new StaticProductRepository(product), saleRepo ?? new InMemorySaleRepository(), new InMemoryUnitOfWork()),
            new InMemoryUnitOfWork());
        var sendUseCase = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            log,
            new InMemoryUnitOfWork());

        return new QuoteHistoryViewModel(getHistoryUseCase, convertUseCase, sendUseCase, session);
    }

    private static QuoteHistoryViewModel BuildHistoryVm(
        IReadOnlyList<QuoteSummary>? summaries = null,
        InMemoryDocumentSendLogRepository? sendLog = null,
        InMemoryQuoteRepository? quoteRepo = null,
        IPdfService? pdfService = null)
    {
        var log = sendLog ?? new InMemoryDocumentSendLogRepository();
        var repo = quoteRepo ?? new InMemoryQuoteRepository();
        var product = new Product("Prod Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(100m), new StockQuantity(5m));

        // Build Quote entities that match each summary (id, email, phone) so GetQuoteHistoryUseCase maps correctly.
        var quoteEntities = BuildQuoteEntitiesFromSummaries(summaries ?? [], repo);
        var historyRepo = new StaticQuoteHistoryRepository(quoteEntities);

        var getHistoryUseCase = new GetQuoteHistoryUseCase(historyRepo);
        var convertUseCase = new ConvertQuoteToSaleUseCase(
            repo,
            new CreateSaleUseCase(new StaticProductRepository(product), new InMemorySaleRepository(), new InMemoryUnitOfWork()),
            new InMemoryUnitOfWork());
        var sendUseCase = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            log,
            new InMemoryUnitOfWork());

        return new QuoteHistoryViewModel(getHistoryUseCase, convertUseCase, sendUseCase, pdfService: pdfService);
    }

    private static IReadOnlyList<Domain.Entities.Quote> BuildQuoteEntitiesFromSummaries(
        IReadOnlyList<QuoteSummary> summaries, InMemoryQuoteRepository repo)
    {
        var result = new List<Domain.Entities.Quote>();
        foreach (var s in summaries)
        {
            // Prefer a quote already stored in the repo (matching by QuoteNumber), so Id is consistent.
            var existingByNumber = repo.GetByQuoteNumber(s.QuoteNumber);
            if (existingByNumber is not null)
            {
                result.Add(existingByNumber);
            }
            else
            {
                var q = new Domain.Entities.Quote(
                    s.QuoteNumber,
                    s.CustomerName,
                    30,
                    customerId: null,
                    customerPhone: s.CustomerPhone,
                    customerEmail: s.CustomerEmail);

                // Mirror the summary's terminal status on the Quote entity so that
                // GetQuoteHistoryUseCase maps the correct QuoteStatus into QuoteSummaryRow.
                if (s.Status == QuoteStatus.Converted)
                {
                    q.Convert(Guid.NewGuid());
                }
                else if (s.Status == QuoteStatus.Expired)
                {
                    q.Expire();
                }

                result.Add(q);
            }
        }
        return result;
    }

    // --- Fake repositories ---

    private sealed class StaticQuoteHistoryRepository : IQuoteRepository
    {
        private readonly IReadOnlyList<Domain.Entities.Quote> _quotes;

        public StaticQuoteHistoryRepository(IReadOnlyList<Domain.Entities.Quote> quotes)
        {
            _quotes = quotes;
        }

        public Task<Domain.Entities.Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_quotes.FirstOrDefault(q => q.Id == id));

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Domain.Entities.Quote quote, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Domain.Entities.Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<Domain.Entities.Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status,
            int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Domain.Entities.Quote>, int)>((_quotes, _quotes.Count));

        public Task<IReadOnlyList<Domain.Entities.Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Domain.Entities.Quote>>(_quotes
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly Dictionary<Guid, Domain.Entities.Quote> _store = new();
        private readonly Dictionary<string, Domain.Entities.Quote> _byNumber = new();

        public void Store(Domain.Entities.Quote quote)
        {
            _store[quote.Id] = quote;
            _byNumber[quote.QuoteNumber] = quote;
        }

        public Domain.Entities.Quote? GetByQuoteNumber(string quoteNumber)
            => _byNumber.TryGetValue(quoteNumber, out var q) ? q : null;

        public Task<Domain.Entities.Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var q) ? q : null);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            Store(quote);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            Store(quote);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Domain.Entities.Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status,
            int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Domain.Entities.Quote>, int)>(([.. _store.Values], _store.Count));

        public Task<IReadOnlyList<Domain.Entities.Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Domain.Entities.Quote>>(_store.Values
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
    }

    private sealed class InMemoryDocumentSendLogRepository : IDocumentSendLogRepository
    {
        private readonly List<DocumentSendLog> _logs = [];

        public int LogCount => _logs.Count;
        public string? LastRecipient => _logs.LastOrDefault()?.RecipientContact;

        public Task AddAsync(DocumentSendLog log, CancellationToken ct = default)
        {
            _logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentSendLog>>(_logs.Where(l => l.DocumentId == documentId).ToList());

        public Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<DocumentSendLog?>(_logs.FirstOrDefault(l => l.Id == id));
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        public Domain.Entities.Sale? LastSale { get; private set; }

        public Task AddAsync(Domain.Entities.Sale sale, CancellationToken ct = default)
        {
            LastSale = sale;
            return Task.CompletedTask;
        }

        public Task<Domain.Entities.Sale?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Domain.Entities.Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class StaticProductRepository : IProductRepository
    {
        private readonly Product _product;

        public StaticProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
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

    private sealed class FakeOperatorSession(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "cashier";
        public UserRole Role => UserRole.Cashier;
        public bool IsAuthenticated => OperatorId != Guid.Empty;
    }

    // --- RED: settlement plan forwarded on quote conversion ---

    [Fact]
    public async Task ConvertToSale_WithSettlementPlan_FullPayment_ProducesInvoiceAndNoReceivable()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Item", Guid.NewGuid(), new Money(10_000m), new Money(20_000m), new StockQuantity(10m), new StockQuantity(1m));
        var quote = CreateActiveQuoteWithItem("COT-010", product.Id);
        quoteRepo.Store(quote);

        var receivables = new InMemoryReceivableRepository();
        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-010", "Test Client", null, null, 20_000m, QuoteStatus.Active, "2026-12-31")
        };

        var vm = BuildHistoryVmWithProductAndReceivables(summaries, quoteRepo, product, receivables);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        // Full cash payment — no deferred amount
        await vm.ConvertToSaleAsync(row, Guid.NewGuid(), new SaleSettlementPlan(20_000m, PaymentMethod.Cash, 0m), TestContext.Current.CancellationToken);

        Assert.Contains("POS-", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(receivables.Stored);
    }

    private static QuoteHistoryViewModel BuildHistoryVmWithProductAndReceivables(
        IReadOnlyList<QuoteSummary> summaries,
        InMemoryQuoteRepository quoteRepo,
        Product product,
        InMemoryReceivableRepository receivables)
    {
        var log = new InMemoryDocumentSendLogRepository();
        var quoteEntities = BuildQuoteEntitiesFromSummaries(summaries, quoteRepo);
        var historyRepo = new StaticQuoteHistoryRepository(quoteEntities);

        var getHistoryUseCase = new GetQuoteHistoryUseCase(historyRepo);
        var convertUseCase = new ConvertQuoteToSaleUseCase(
            quoteRepo,
            new CreateSaleUseCase(new StaticProductRepository(product), new InMemorySaleRepository(), new InMemoryUnitOfWork(), receivables),
            new InMemoryUnitOfWork());
        var sendUseCase = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            log,
            new InMemoryUnitOfWork());

        return new QuoteHistoryViewModel(getHistoryUseCase, convertUseCase, sendUseCase);
    }

    private sealed class InMemoryReceivableRepository : IReceivableRepository
    {
        public List<Receivable> Stored { get; } = [];

        public Task AddAsync(Receivable receivable, CancellationToken ct = default)
        {
            Stored.Add(receivable);
            return Task.CompletedTask;
        }

        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Receivable?>(Stored.FirstOrDefault(r => r.Id == id));

        public Task UpdateAsync(Receivable receivable, CancellationToken ct = default) => Task.CompletedTask;
    }
}
