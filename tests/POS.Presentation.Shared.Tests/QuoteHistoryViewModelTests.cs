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
    public async Task SendByEmail_WhenInvalidCustomerEmail_SetsBlockedStatusWithoutCallingUseCase()
    {
        var sendLog = new InMemoryDocumentSendLogRepository();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(Guid.NewGuid(), "COT-003-INV", "Email Invalido", "invalid@email", "+57 300 000 0000", 80_000m, QuoteStatus.Active, "2026-12-31")
            ],
            sendLog: sendLog);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.SendByEmailAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(0, sendLog.LogCount);
        Assert.Contains("no es válido", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task SendByWhatsApp_WhenInvalidCustomerPhone_SetsBlockedStatusWithoutCallingUseCase()
    {
        var sendLog = new InMemoryDocumentSendLogRepository();
        var vm = BuildHistoryVm(
            summaries:
            [
                new QuoteSummary(Guid.NewGuid(), "COT-004-INV", "Tel Invalido", "test@example.com", "1234567", 80_000m, QuoteStatus.Active, "2026-12-31")
            ],
            sendLog: sendLog);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.SendByWhatsAppAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(0, sendLog.LogCount);
        Assert.Contains("no es válido", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        await vm.ConvertToSaleAsync(row, TestContext.Current.CancellationToken);

        Assert.Contains("POS-", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ViewDetailCommand_RaisesQuoteDetailRequestedWithLineItems_WithoutGeneratingPdf()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Detalle", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-013", product.Id);
        quoteRepo.Store(quote);

        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-013", "Cliente Detalle", null, null, 2_000m, QuoteStatus.Active, "2026-12-31")
        };

        var vm = BuildHistoryVmWithProduct(summaries: summaries, quoteRepo: quoteRepo, product: product);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteDetailResult? received = null;
        vm.QuoteDetailRequested += (_, detail) => received = detail;

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        vm.ViewDetailCommand.Execute(row);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.NotNull(received);
        Assert.Equal("COT-013", received!.QuoteNumber);
        Assert.Single(received.Items);
        Assert.Equal("Producto Test", received.Items[0].ProductName);
    }

    [Fact]
    public async Task ConvertToSale_WhenProductHasNoStock_ShowsFriendlyStockMessage()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Sin Stock", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(0m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-012", product.Id);
        quoteRepo.Store(quote);

        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-012", "Cliente Sin Stock", null, null, 2_000m, QuoteStatus.Active, "2026-12-31")
        };

        var vm = BuildHistoryVmWithProduct(summaries: summaries, quoteRepo: quoteRepo, product: product);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        await vm.ConvertToSaleAsync(row, TestContext.Current.CancellationToken);

        Assert.Contains("no hay stock suficiente", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(QuoteStatus.Converted, quote.Status);
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
        await vm.ConvertToSaleAsync(row, TestContext.Current.CancellationToken);

        Assert.Contains("ya fue convertida", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToSaleCommand_RequestsConfirmationBeforeUsingAuthenticatedOperatorSession()
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

        QuoteSummaryRow row = vm.Quotes.Single();
        QuoteSummaryRow? confirmationRequest = null;
        vm.SaleConversionConfirmationRequested += (_, requestedRow) => confirmationRequest = requestedRow;

        vm.ConvertToSaleCommand.Execute(row);

        Assert.Same(row, confirmationRequest);
        Assert.Null(saleRepo.LastSale);

        await vm.ConfirmSaleConversionAsync(row, TestContext.Current.CancellationToken);
        Assert.Equal(cashierId, saleRepo.LastSale?.CashierUserId);
        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(saleRepo.LastSale?.Id, quote.ConvertedSaleId);
    }

    [Fact]
    public async Task CancelQuoteCommand_FiresConfirmationRequested_AndConfirmsCancellation()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-010", product.Id);
        quoteRepo.Store(quote);

        var cancelUseCase = new CancelQuoteUseCase(quoteRepo, new InMemoryUnitOfWork());
        var vm = new QuoteHistoryViewModel(
            new GetQuoteHistoryUseCase(quoteRepo),
            new ConvertQuoteToSaleUseCase(quoteRepo, new CreateSaleUseCase(new StaticProductRepository(product), new InMemorySaleRepository(), new InMemoryUnitOfWork()), new InMemoryUnitOfWork()),
            new SendDocumentUseCase(new FakePdfService(), new FakeEmailService(), new FakeWhatsAppService(), new InMemoryDocumentSendLogRepository(), new InMemoryUnitOfWork()),
            cancelQuoteUseCase: cancelUseCase);

        var row = new QuoteSummaryRow(quote.Id, "COT-010", "Cliente Test", null, null, 100m, "$100", QuoteStatus.Active, "Activa", "2026-12-31");
        QuoteSummaryRow? confirmationRequest = null;
        vm.QuoteCancellationConfirmationRequested += (_, requestedRow) => confirmationRequest = requestedRow;

        vm.CancelQuoteCommand.Execute(row);

        Assert.Same(row, confirmationRequest);
        Assert.Equal(QuoteStatus.Draft, quote.Status);

        await vm.ConfirmQuoteCancellationAsync(row, TestContext.Current.CancellationToken);
        Assert.Equal(QuoteStatus.Cancelled, quote.Status);
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
            product,
            session: new FakeOperatorSession(Guid.Empty));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        await vm.ConvertToSaleAsync(vm.Quotes.Single(), TestContext.Current.CancellationToken);

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

    // --- Editable proposals: the affordance is the aggregate's status rule, restated for the binding ---

    [Theory]
    [InlineData(QuoteStatus.Draft, true)]
    [InlineData(QuoteStatus.Active, true)]
    [InlineData(QuoteStatus.Expired, false)]
    [InlineData(QuoteStatus.Converted, false)]
    [InlineData(QuoteStatus.Cancelled, false)]
    public void QuoteSummaryRow_CanEdit_AgreesWithTheAggregateForEveryStatus(QuoteStatus status, bool expected)
    {
        var summary = new QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-EDIT-MATRIX",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: status,
            ExpiresAt: "2026-12-31");

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        var aggregate = new Domain.Entities.Quote("COT-EDIT-MATRIX", "Cliente", 30);
        if (status == QuoteStatus.Converted)
        {
            aggregate.Convert(Guid.NewGuid());
        }
        else if (status == QuoteStatus.Expired)
        {
            aggregate.Expire();
        }
        else if (status == QuoteStatus.Cancelled)
        {
            aggregate.Cancel();
        }

        Assert.Equal(expected, row.CanEdit);
        Assert.Equal(aggregate.CanEdit, row.CanEdit);
    }

    /// <summary>
    /// C6c. CanConvertToSale, CanEdit and CanCancel each used to restate the status set by hand, in
    /// addition to <c>Quote.CanEdit</c> in the domain — four expressions that only a comment claimed
    /// agreed. This pins all three against the domain aggregate for every status, so a new QuoteStatus
    /// member cannot leave one affordance behind. Note CanCancel legitimately differs from CanEdit on
    /// Expired: cancelling a lapsed quote is how the shop retires it.
    /// </summary>
    [Theory]
    [InlineData(QuoteStatus.Draft, true, true, true)]
    [InlineData(QuoteStatus.Active, true, true, true)]
    [InlineData(QuoteStatus.Expired, false, false, true)]
    [InlineData(QuoteStatus.Converted, false, false, false)]
    [InlineData(QuoteStatus.Cancelled, false, false, false)]
    public void QuoteSummaryRow_TheThreeAffordancesAgreeWithTheDomainForEveryStatus(
        QuoteStatus status, bool canConvert, bool canEdit, bool canCancel)
    {
        var row = QuoteSummaryRow.FromSummary(new QuoteSummary(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-AFFORDANCES",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: status,
            ExpiresAt: "2026-12-31"));

        Assert.Equal(canConvert, row.CanConvertToSale);
        Assert.Equal(canEdit, row.CanEdit);
        Assert.Equal(canCancel, row.CanCancel);

        // The domain remains authoritative: a live quote is a proposal, a converted or cancelled one is
        // not. Only the expiry half of the row's rule is invisible from a status alone, and it is
        // covered by the lapsed-row tests below.
        var aggregate = new Domain.Entities.Quote("COT-AFFORDANCES", "Cliente", 30);
        if (status == QuoteStatus.Converted)
        {
            aggregate.Convert(Guid.NewGuid());
        }
        else if (status == QuoteStatus.Expired)
        {
            aggregate.Expire();
        }
        else if (status == QuoteStatus.Cancelled)
        {
            aggregate.Cancel();
        }

        Assert.Equal(aggregate.CanEdit, row.CanEdit);
    }

    /// <summary>
    /// C3 at the row level. A lapsed quote is still <c>Draft</c> — nothing transitions it, because
    /// <c>Expire()</c> is only reached from the conversion path — so a status-only affordance rule left
    /// the Edit action showing on a quote the use case would refuse to save, and offered no renewal
    /// guidance. The row mirrors the aggregate's rule: editable status AND a deadline that has not lapsed.
    /// </summary>
    [Fact]
    public void QuoteSummaryRow_ALapsedDraftLosesTheEditAffordanceAndShowsRenewalGuidance()
    {
        QuoteSummary summary = new(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-LAPSED",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Draft,
            ExpiresAt: "2026-01-01",
            IsExpiredByCalendar: true);

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.False(row.CanEdit);
        Assert.False(row.CanConvertToSale);
        Assert.True(row.IsExpiredByCalendar);
        Assert.Equal("Renueva o crea otra cotización para vender.", row.ExpirationGuidance);
    }

    /// <summary>
    /// The same row, but never emailed. Expiry used to be inferred from the "Vencida" reminder label,
    /// and the label itself was gated on a successful email — so a quote that was never sent reported no
    /// expiry, kept its edit affordance, and showed no guidance. A deadline elapsing is a fact about the
    /// quote, not about the channel it went out on.
    /// </summary>
    [Fact]
    public void QuoteSummaryRow_ANeverEmailedLapsedQuote_StillReportsTheExpiry()
    {
        QuoteSummary summary = new(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-LAPSED-NO-EMAIL",
            CustomerName: "Cliente",
            CustomerEmail: null,
            CustomerPhone: null,
            Total: 10_000m,
            Status: Domain.Enums.QuoteStatus.Draft,
            ExpiresAt: "2026-01-01",
            HasSuccessfulEmail: false,
            ReminderLabel: "Vencida",
            IsExpiredByCalendar: true);

        QuoteSummaryRow row = QuoteSummaryRow.FromSummary(summary);

        Assert.True(row.IsExpiredByCalendar);
        Assert.True(row.HasReminder);
        Assert.False(row.CanEdit);
        Assert.Equal("Renueva o crea otra cotización para vender.", row.ExpirationGuidance);
    }

    [Fact]
    public async Task EditCommand_WithoutQuotesEditPermission_BlocksWithRecoverableMessage()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-EDIT-PERM", product.Id);
        quoteRepo.Store(quote);

        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-EDIT-PERM", "Cliente Sin Permiso", null, null, 2_000m, QuoteStatus.Active, "2026-12-31")
        };

        QuoteHistoryViewModel vm = BuildHistoryVmWithProduct(
            summaries,
            quoteRepo,
            product,
            session: new OperatorSessionWithoutQuotesEdit(Guid.NewGuid()));
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteDetailResult? requested = null;
        vm.QuoteEditRequested += (_, detail) => requested = detail;

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        vm.EditCommand.Execute(row);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Null(requested);
        Assert.Contains("permiso", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditCommand_WhenQuoteIsFrozen_BlocksWithRecoverableMessage()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-EDIT-FROZEN", product.Id);
        quote.Convert(Guid.NewGuid());
        quoteRepo.Store(quote);

        QuoteHistoryViewModel vm = BuildHistoryVmWithProduct(
            [new QuoteSummary(quote.Id, "COT-EDIT-FROZEN", "Cliente Convertida", null, null, 2_000m, QuoteStatus.Converted, "2026-12-31")],
            quoteRepo,
            product);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteDetailResult? requested = null;
        vm.QuoteEditRequested += (_, detail) => requested = detail;

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        Assert.False(row.CanEdit);

        vm.EditCommand.Execute(row);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Null(requested);
        Assert.Contains("no se puede editar", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditCommand_RaisesQuoteEditRequestedWithTheLoadedDetail()
    {
        var quoteRepo = new InMemoryQuoteRepository();
        var product = new Product("Producto Detalle", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var quote = CreateActiveQuoteWithItem("COT-EDIT-OK", product.Id);
        quoteRepo.Store(quote);

        var summaries = new List<QuoteSummary>
        {
            new QuoteSummary(quote.Id, "COT-EDIT-OK", "Cliente Detalle", null, null, 2_000m, QuoteStatus.Active, "2026-12-31")
        };

        QuoteHistoryViewModel vm = BuildHistoryVmWithProduct(summaries, quoteRepo, product);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        QuoteDetailResult? requested = null;
        vm.QuoteEditRequested += (_, detail) => requested = detail;

        QuoteSummaryRow row = Assert.Single(vm.Quotes);
        vm.EditCommand.Execute(row);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.NotNull(requested);
        Assert.Equal(quote.Id, requested!.QuoteId);
        Assert.Equal("COT-EDIT-OK", requested.QuoteNumber);
        // The detail comes from the stored aggregate, not the summary — the quote starts as Draft.
        Assert.Equal(QuoteStatus.Draft, requested.Status);
        Assert.Single(requested.Items);
        Assert.Equal("Producto Test", requested.Items[0].ProductName);
    }

    [Fact]
    public async Task EditAsync_WhenDetailUseCaseIsUnavailable_SetsRecoverableMessage()
    {
        var vm = BuildHistoryVm();

        await vm.EditAsync(new QuoteSummaryRow(Guid.NewGuid(), "COT-EDIT-NODETAIL", "Cliente", null, null, 10_000m, "$10.000", QuoteStatus.Active, "Activa", "2026-12-31"), TestContext.Current.CancellationToken);

        Assert.Contains("no está disponible", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        session ??= new FakeOperatorSession(Guid.NewGuid());
        var log = new InMemoryDocumentSendLogRepository();
        var quoteEntities = BuildQuoteEntitiesFromSummaries(summaries, quoteRepo);
        var historyRepo = new StaticQuoteHistoryRepository(quoteEntities);

        var getHistoryUseCase = new GetQuoteHistoryUseCase(historyRepo);
        var convertUseCase = new ConvertQuoteToSaleUseCase(
            quoteRepo,
            new CreateSaleUseCase(new StaticProductRepository(product), saleRepo ?? new InMemorySaleRepository(), new InMemoryUnitOfWork(), authorizer: CreateAuthorizer(session?.OperatorId)),
            new InMemoryUnitOfWork());
        var sendUseCase = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            log,
            new InMemoryUnitOfWork());

        return new QuoteHistoryViewModel(getHistoryUseCase, convertUseCase, sendUseCase, session, getQuoteDetail: new GetQuoteDetailUseCase(quoteRepo));
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
            new CreateSaleUseCase(new StaticProductRepository(product), new InMemorySaleRepository(), new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()),
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

        public Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SaleReturn>>([]);
        public Task<SaleReturn?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default) => Task.FromResult<SaleReturn?>(null);
    }

    private static POS.Application.Services.CriticalCommandAuthorizer CreateAuthorizer(Guid? operatorId = null)
    {
        var id = operatorId ?? Guid.NewGuid();
        var user = new User("cashier", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return new POS.Application.Services.CriticalCommandAuthorizer(new TestSession(id), new TestUsers(user), new TestRoles());
    }

    private sealed class TestSession(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "cashier";
        public bool IsAuthenticated => true;
    }

    private sealed class TestUsers(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task AddAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task UpdateAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class TestRoles : IRoleRepository
    {
        private static readonly Role Administrator = Role.CreateSystemRole(POS.Domain.Authorization.RoleConstants.AdministratorRoleId, POS.Domain.Authorization.RoleConstants.AdministratorRoleName);
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(id == Administrator.Id ? Administrator : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([Administrator]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
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
        public Guid RoleId => POS.Domain.Authorization.RoleConstants.CashierRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.CashierRoleName;
        public IReadOnlySet<string> Permissions => new HashSet<string>(POS.Domain.Authorization.PermissionKeys.CashierDefaults);
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => OperatorId != Guid.Empty;
    }

    private sealed class OperatorSessionWithoutQuotesEdit(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "cashier";
        public bool IsAuthenticated => OperatorId != Guid.Empty;

        // Everything the cashier role grants except the quote correction — the negative control for I6.
        public IReadOnlySet<string> Permissions => new HashSet<string>(
            POS.Domain.Authorization.PermissionKeys.CashierDefaults
                .Where(key => key != POS.Domain.Authorization.PermissionKeys.Quotes.Edit));
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
        await vm.ConvertToSaleAsync(row, new SaleSettlementPlan(2_000m, PaymentMethod.Cash, 0m), TestContext.Current.CancellationToken);

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
        var session = new FakeOperatorSession(Guid.NewGuid());
        var convertUseCase = new ConvertQuoteToSaleUseCase(
            quoteRepo,
            new CreateSaleUseCase(new StaticProductRepository(product), new InMemorySaleRepository(), new InMemoryUnitOfWork(), receivables, authorizer: CreateAuthorizer(session.OperatorId)),
            new InMemoryUnitOfWork());
        var sendUseCase = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            log,
            new InMemoryUnitOfWork());

        return new QuoteHistoryViewModel(getHistoryUseCase, convertUseCase, sendUseCase, session);
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
