using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
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
    public async Task SendByEmail_WithInvalidEmail_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomerName = "Ana Gómez";
        vm.CustomerEmail = "invalidemail";
        AddCartItem(vm, Guid.NewGuid(), "Producto Test", 1_000m);

        await vm.SendByEmailAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Ingresa un correo electrónico válido", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task SendByWhatsApp_WithInvalidPhone_ShowsRecoverableValidationMessage()
    {
        var vm = CreateViewModel(out _, out _);
        vm.CustomerName = "Ana Gómez";
        vm.CustomerPhone = "12345";
        AddCartItem(vm, Guid.NewGuid(), "Producto Test", 1_000m);

        await vm.SendByWhatsAppAsync(TestContext.Current.CancellationToken);

        Assert.Contains("mínimo 8 dígitos", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void ApplyCartDiscountCommand_WithCartItems_SetsDiscountOnAllLinesAndUpdatesTotal()
    {
        var vm = CreateViewModel(out _, out _);
        AddCartItem(vm, Guid.NewGuid(), "Producto A", 1_000m);
        AddCartItem(vm, Guid.NewGuid(), "Producto B", 2_000m);

        vm.CartDiscountPercentInput = 10m;
        vm.ApplyCartDiscountCommand.Execute(null);

        Assert.All(vm.CartItems, item => Assert.Equal(10m, item.Discount));
        Assert.True(vm.HasManualDiscount);
        Assert.Equal(2_700m, vm.Total);
    }

    [Fact]
    public void ClearCartDiscountCommand_AfterApplying_ResetsDiscountAndInput()
    {
        var vm = CreateViewModel(out _, out _);
        AddCartItem(vm, Guid.NewGuid(), "Producto A", 1_000m);
        vm.CartDiscountPercentInput = 15m;
        vm.ApplyCartDiscountCommand.Execute(null);

        vm.ClearCartDiscountCommand.Execute(null);

        Assert.All(vm.CartItems, item => Assert.Equal(0m, item.Discount));
        Assert.False(vm.HasManualDiscount);
        Assert.Equal(0m, vm.CartDiscountPercentInput);
        Assert.Equal(1_000m, vm.Total);
    }

    [Fact]
    public void ApplyCartDiscountCommand_WithEmptyCart_CannotExecute()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.False(vm.ApplyCartDiscountCommand.CanExecute(null));
    }

    [Fact]
    public void ClearCartDiscountCommand_WithoutManualDiscount_CannotExecute()
    {
        var vm = CreateViewModel(out _, out _);
        AddCartItem(vm, Guid.NewGuid(), "Producto A", 1_000m);

        Assert.False(vm.ClearCartDiscountCommand.CanExecute(null));
    }

    // --- Edit mode: the create form is reused as the edit form ---

    [Fact]
    public void LoadForEdit_HydratesEveryFieldAndEveryCartLine()
    {
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vm = CreateViewModel(out _, out _);

        var detail = new QuoteDetailResult(
            QuoteId: Guid.NewGuid(),
            QuoteNumber: "COT-300",
            CustomerId: customerId,
            CustomerName: "Ana Gómez",
            CustomerEmail: "ana@example.com",
            CustomerPhone: "+57 300 000 0001",
            Status: Domain.Enums.QuoteStatus.Active,
            ExpiresAt: "2026-12-31",
            Total: 9_000m,
            Items:
            [
                new QuoteDetailItemResult(productId, "Producto Caja", 2m, 5_000m, 10m, 9_000m, "Caja", 6)
            ],
            Observations: "Entregar en la mañana",
            ValidityDays: 45);

        vm.LoadForEdit(detail);

        Assert.True(vm.IsEditing);
        Assert.Equal("COT-300", vm.EditedQuoteNumber);
        Assert.Equal("Guardar cambios", vm.ConfirmButtonLabel);
        Assert.Equal("Ana Gómez", vm.CustomerName);
        Assert.Equal("+57 300 000 0001", vm.CustomerPhone);
        Assert.Equal("ana@example.com", vm.CustomerEmail);
        Assert.Equal(customerId, vm.SelectedCustomerId);
        Assert.Equal(45, vm.ValidityDays);
        Assert.Equal("Entregar en la mañana", vm.Observations);

        QuoteCartItemViewModel line = Assert.Single(vm.CartItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal("Producto Caja", line.ProductName);
        Assert.Equal(5_000m, line.UnitPrice);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(10m, line.Discount);
        Assert.Equal("Caja", line.PresentationName);
        Assert.Equal(6, line.UnitsPerPresentation);
        Assert.Equal(9_000m, line.Subtotal);
        Assert.Equal(9_000m, vm.Total);
    }

    /// <summary>
    /// C4a. "Descartar" is bound to CancelEditCommand, whose CanExecute closes over IsEditing, and
    /// RelayCommand has no CommandManager behind it — WPF evaluates CanExecute once, at bind time, and
    /// never re-queries. So the assertion has to go through CanExecute, not through CancelEdit(): the
    /// old test called the method directly, which bypasses exactly the thing that was broken and is why
    /// the button could never enable.
    /// </summary>
    [Fact]
    public void CancelEditCommand_TracksIsEditingThroughCanExecute()
    {
        var vm = CreateViewModel(out _, out _);

        // Bound state: the form is creating a new quote, so discarding an edit is meaningless.
        Assert.False(vm.CancelEditCommand.CanExecute(null));

        vm.LoadForEdit(new QuoteDetailResult(
            Guid.NewGuid(), "COT-CANCEL-CANEXEC", null, "Ana Gómez", null, null,
            Domain.Enums.QuoteStatus.Active, "2026-12-31", 1_000m,
            [new QuoteDetailItemResult(Guid.NewGuid(), "Producto", 1m, 1_000m, 0m, 1_000m)],
            null, 30));

        Assert.True(vm.IsEditing);
        Assert.True(vm.CancelEditCommand.CanExecute(null));

        vm.CancelEdit();

        Assert.False(vm.IsEditing);
        Assert.False(vm.CancelEditCommand.CanExecute(null));
    }

    /// <summary>
    /// CanExecute re-reads the predicate on every call, so the test above would pass even if the raise
    /// were missing — it pins the value, not the notification. WPF only ever reads it again if
    /// CanExecuteChanged fires, so that is the half that actually mattered and it needs its own assertion.
    /// </summary>
    [Fact]
    public void CancelEditCommand_RaisesCanExecuteChanged_WhenIsEditingFlips()
    {
        var vm = CreateViewModel(out _, out _);
        int raised = 0;
        ((System.Windows.Input.ICommand)vm.CancelEditCommand).CanExecuteChanged += (_, _) => raised++;

        vm.LoadForEdit(new QuoteDetailResult(
            Guid.NewGuid(), "COT-CANCEL-RAISE", null, "Ana Gómez", null, null,
            Domain.Enums.QuoteStatus.Active, "2026-12-31", 1_000m,
            [new QuoteDetailItemResult(Guid.NewGuid(), "Producto", 1m, 1_000m, 0m, 1_000m)],
            null, 30));
        int afterLoad = raised;

        vm.CancelEdit();

        Assert.True(afterLoad > 0, "Entering edit mode must re-query the command's CanExecute.");
        Assert.True(raised > afterLoad, "Leaving edit mode must re-query the command's CanExecute.");
    }

    [Fact]
    public void CancelEdit_AfterHydrating_ReturnsToCreateModeAndDropsTheEditForm()
    {
        var vm = CreateViewModel(out _, out _);

        vm.LoadForEdit(new QuoteDetailResult(
            Guid.NewGuid(), "COT-301", null, "Ana Gómez", null, null,
            Domain.Enums.QuoteStatus.Active, "2026-12-31", 1_000m,
            [new QuoteDetailItemResult(Guid.NewGuid(), "Producto", 1m, 1_000m, 0m, 1_000m)],
            null, 30));
        Assert.Single(vm.CartItems);

        vm.CancelEdit();

        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.EditedQuoteNumber);
        Assert.Equal("Confirmar cotización", vm.ConfirmButtonLabel);
        Assert.Empty(vm.CartItems);
    }

    [Fact]
    public async Task Confirm_InEditMode_UpdatesTheSameQuoteAndNeverCreatesASecondOne()
    {
        var product = new Product("Producto Caja", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        var stored = new Domain.Entities.Quote("COT-302", "Nombre Viejo", 30);
        stored.AddItem(new Domain.Entities.QuoteItem(product.Id, "Producto Caja", new Money(2_000m), new Quantity(1m), 0m));
        await quoteRepo.AddAsync(stored, TestContext.Current.CancellationToken);

        vm.LoadForEdit(new QuoteDetailResult(
            stored.Id, stored.QuoteNumber, null, "Nombre Viejo", null, null,
            Domain.Enums.QuoteStatus.Draft, "2026-12-31", 2_000m,
            [new QuoteDetailItemResult(product.Id, "Producto Caja", 1m, 2_000m, 0m, 2_000m)],
            null, 30));

        vm.CustomerName = "Nombre Corregido";
        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, quoteRepo.AddCount);
        Assert.Equal(1, quoteRepo.UpdateCount);
        Domain.Entities.Quote quote = Assert.Single(quoteRepo.Quotes);
        Assert.Equal(stored.Id, quote.Id);
        Assert.Equal("COT-302", quote.QuoteNumber);
        Assert.Equal("Nombre Corregido", quote.CustomerName);
        Assert.Equal(stored.CreatedAt, quote.CreatedAt);
        Assert.Contains("actualizada correctamente", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("COT-302", vm.LastQuoteNumber);

        // Back in create mode, and the corrected quote stays visible.
        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.EditedQuoteNumber);
        Assert.Equal("Confirmar cotización", vm.ConfirmButtonLabel);
    }

    [Fact]
    public async Task Confirm_InEditMode_WhenUpdateUseCaseIsMissing_SetsRecoverableMessageAndKeepsTheForm()
    {
        var vm = CreateViewModel(out _, out _);
        vm.LoadForEdit(new QuoteDetailResult(
            Guid.NewGuid(), "COT-303", null, "Ana Gómez", null, null,
            Domain.Enums.QuoteStatus.Active, "2026-12-31", 1_000m,
            [new QuoteDetailItemResult(Guid.NewGuid(), "Producto", 1m, 1_000m, 0m, 1_000m)],
            null, 30));

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Contains("no está disponible", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.IsEditing);
        Assert.Single(vm.CartItems);
    }

    [Fact]
    public async Task Confirm_InCreateMode_StillCreatesInsteadOfUpdating()
    {
        var product = new Product("Producto Test", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        vm.CustomerName = "Ana Gómez";
        AddCartItem(vm, product.Id, product.Name, 2_000m);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, quoteRepo.AddCount);
        Assert.Equal(0, quoteRepo.UpdateCount);
        Assert.NotEmpty(vm.LastQuoteNumber);
    }

    /// <summary>
    /// C2, the money-path divergence. The form hydrates from the quote's FROZEN snapshot price while the
    /// save path re-resolves the same line through QuoteLineBuilder against the live catalogue. When
    /// those differ, the total the operator approved is not the total that gets stored — and the stored
    /// one is what later becomes the invoice.
    ///
    /// This pins the three figures the operator can observe to agreeing on the persisted value: the form
    /// total left on screen, the amount stated in the success message, and the reloaded quote's own
    /// Total. Before the fix the message carried no amount at all and the form showed the emptied cart.
    /// </summary>
    [Fact]
    public async Task Confirm_InEditMode_WhenTheCataloguePriceMoved_FormMessageAndPersistedTotalAllAgree()
    {
        // The product's catalogue price is 2_000; the quote froze its line at 1_000. A 2-line edit
        // therefore re-prices from 2_000 to 4_000 while the hydrated form showed 2_000.
        var product = new Product("Producto Cambiado", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        var stored = new Domain.Entities.Quote("COT-303-DIVERGE", "Cliente", 30);
        stored.AddItem(new Domain.Entities.QuoteItem(product.Id, "Producto Cambiado", new Money(1_000m), new Quantity(2m), 0m));
        await quoteRepo.AddAsync(stored, TestContext.Current.CancellationToken);

        vm.LoadForEdit(new QuoteDetailResult(
            stored.Id, stored.QuoteNumber, null, "Cliente", null, null,
            Domain.Enums.QuoteStatus.Draft, "2026-12-31", 2_000m,
            [new QuoteDetailItemResult(product.Id, "Producto Cambiado", 2m, 1_000m, 0m, 2_000m)],
            null, 30));

        // The pre-save form total really is the frozen one — this is the number the operator sees.
        Assert.Equal(2_000m, vm.Total);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Domain.Entities.Quote persisted = Assert.Single(quoteRepo.Quotes);
        Assert.Equal(4_000m, persisted.Total);

        // 1. The form total left on screen is the persisted figure, not the emptied cart's zero.
        Assert.Equal(4_000m, vm.Total);
        Assert.Equal("$4.000", vm.TotalFormatted);

        // 2. The success message states the persisted amount, and names the change rather than hiding it.
        Assert.Contains("4.000", vm.ValidationMessage, StringComparison.Ordinal);
        Assert.Contains("catálogo cambió", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_InEditMode_WhenThePriceDidNotMove_TheMessageStillCarriesTheTotal()
    {
        var product = new Product("Producto Igual", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        var stored = new Domain.Entities.Quote("COT-304-STABLE", "Cliente", 30);
        stored.AddItem(new Domain.Entities.QuoteItem(product.Id, "Producto Igual", new Money(2_000m), new Quantity(1m), 0m));
        await quoteRepo.AddAsync(stored, TestContext.Current.CancellationToken);

        vm.LoadForEdit(new QuoteDetailResult(
            stored.Id, stored.QuoteNumber, null, "Cliente", null, null,
            Domain.Enums.QuoteStatus.Draft, "2026-12-31", 2_000m,
            [new QuoteDetailItemResult(product.Id, "Producto Igual", 1m, 2_000m, 0m, 2_000m)],
            null, 30));

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2_000m, Assert.Single(quoteRepo.Quotes).Total);
        Assert.Equal(2_000m, vm.Total);
        // No spurious re-pricing warning when nothing actually changed.
        Assert.DoesNotContain("catálogo cambió", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$2.000", vm.ValidationMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// C4b at the form. LoadForEdit hydrating SelectedCustomerId was never the interesting half — the
    /// interesting half is the customer SURVIVING the save. This drives the real command (the "Usar"
    /// button's target) and then the real save path, so the assertion lands on the persisted association
    /// rather than on a property that was already correct in the form.
    /// </summary>
    [Fact]
    public async Task Confirm_InEditMode_WhenTheOperatorPicksACustomer_TheLinkSurvivesTheSave()
    {
        var product = new Product("Producto Caja", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        var stored = new Domain.Entities.Quote("COT-LINK-001", "Nombre Viejo", 30);
        stored.AddItem(new Domain.Entities.QuoteItem(product.Id, "Producto Caja", new Money(2_000m), new Quantity(1m), 0m));
        await quoteRepo.AddAsync(stored, TestContext.Current.CancellationToken);

        vm.LoadForEdit(new QuoteDetailResult(
            stored.Id, stored.QuoteNumber, null, "Nombre Viejo", null, null,
            Domain.Enums.QuoteStatus.Draft, "2026-12-31", 2_000m,
            [new QuoteDetailItemResult(product.Id, "Producto Caja", 1m, 2_000m, 0m, 2_000m)],
            null, 30));
        Assert.Null(vm.SelectedCustomerId);

        // What the operator does in the form: each search result renders a "Usar" button wired to this.
        var pickedCustomer = new CustomerSearchResult(
            Guid.NewGuid(), "Ana Gómez", "1099999999", "+57 300 111 2222", "ana@example.com", null, IsActive: true);
        vm.SelectCustomerCommand.Execute(pickedCustomer);
        Assert.Equal(pickedCustomer.Id, vm.SelectedCustomerId);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Contains("actualizada correctamente", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        // The regression: this stayed null while the message claimed success.
        Assert.Equal(pickedCustomer.Id, Assert.Single(quoteRepo.Quotes).CustomerId);
    }

    /// <summary>
    /// A quote that names no registered customer must stay that way through an edit. There is no UI to
    /// un-pick a customer once chosen — SelectedCustomerId has a private setter — so the reachable
    /// "left as walk-in" case is a walk-in quote edited with the picker alone: the association has to
    /// survive as null. The explicit clear and re-link cases are covered at the domain and use-case
    /// level, where a caller can actually express them.
    /// </summary>
    [Fact]
    public async Task Confirm_InEditMode_OnAWalkInQuote_TheQuoteStaysUnregistered()
    {
        var product = new Product("Producto Caja", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(50m), new StockQuantity(5m));
        var vm = CreateViewModel(out InMemoryQuoteRepository quoteRepo, out _, product, withUpdate: true);

        var stored = new Domain.Entities.Quote("COT-LINK-002", "Cliente Ocasional", 30);
        stored.AddItem(new Domain.Entities.QuoteItem(product.Id, "Producto Caja", new Money(2_000m), new Quantity(1m), 0m));
        await quoteRepo.AddAsync(stored, TestContext.Current.CancellationToken);

        vm.LoadForEdit(new QuoteDetailResult(
            stored.Id, stored.QuoteNumber, null, "Cliente Ocasional", null, null,
            Domain.Enums.QuoteStatus.Draft, "2026-12-31", 2_000m,
            [new QuoteDetailItemResult(product.Id, "Producto Caja", 1m, 2_000m, 0m, 2_000m)],
            null, 30));
        Assert.Null(vm.SelectedCustomerId);

        await vm.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(quoteRepo.Quotes).CustomerId);
    }

    private static void AddCartItem(QuoteViewModel vm, Guid productId, string name, decimal unitPrice)
        => vm.CartItems.Add(new QuoteCartItemViewModel(productId, name, unitPrice));

    private static QuoteViewModel CreateViewModel(out InMemoryQuoteRepository quoteRepo, out InMemoryUnitOfWork unitOfWork, Product? product = null, bool withUpdate = false)
    {
        quoteRepo = new InMemoryQuoteRepository();
        unitOfWork = new InMemoryUnitOfWork();
        var productRepo = new StaticProductRepository(product);
        var createQuote = new CreateQuoteUseCase(quoteRepo, productRepo, unitOfWork);
        var updateQuote = withUpdate
            ? new UpdateQuoteUseCase(quoteRepo, productRepo, unitOfWork, new CashierWithQuotesEditSession())
            : null;
        var sendDocument = new SendDocumentUseCase(
            new FakePdfService(),
            new FakeEmailService(),
            new FakeWhatsAppService(),
            new InMemoryDocumentSendLogRepository(),
            unitOfWork);
        return new QuoteViewModel(new EmptyProductLookupService(), createQuote, sendDocument, updateQuoteUseCase: updateQuote);
    }

    /// <summary>
    /// The edit use case now enforces Quotes.Edit itself, so the view-model tests driving the save path
    /// have to present a session that actually holds the key.
    /// </summary>
    private sealed class CashierWithQuotesEditSession : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username => "cashier";
        public IReadOnlySet<string> Permissions { get; } =
            new HashSet<string>([Domain.Authorization.PermissionKeys.Quotes.Edit], StringComparer.Ordinal);
        public bool IsAuthenticated => true;
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

        /// <summary>Adds and updates are counted apart so a test can prove which use case actually ran.</summary>
        public int AddCount { get; private set; }

        public int UpdateCount { get; private set; }

        public Task<Domain.Entities.Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var q) ? q : null);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            AddCount++;
            _store[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Domain.Entities.Quote quote, CancellationToken ct = default)
        {
            UpdateCount++;
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
