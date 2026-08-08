using POS.Application.DTOs.Customers;
using POS.Application.Common;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Presentation.Shared.Cashier;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CashierViewModelTests
{
    [Fact]
    public void TransferPayment_ExposesCanonicalPlatformAndClearsItForOtherMethods()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService());

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        Assert.True(viewModel.IsTransferPayment);
        Assert.Equal(PaymentPlatforms.All, viewModel.PaymentPlatformOptions);

        viewModel.BankOrPlatform = "Nequi";
        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);

        Assert.False(viewModel.IsTransferPayment);
        Assert.Equal(string.Empty, viewModel.BankOrPlatform);
    }

    [Fact]
    public void ReceivedAmount_UsesWholeCopAndIgnoresDecimalKey()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService());

        viewModel.ReceivedAmount = 12_345.67m;
        viewModel.AppendReceivedAmountCommand.Execute(".");

        Assert.Equal(12_345m, viewModel.ReceivedAmount);
    }

    [Fact]
    public async Task ApplyRedemptionAsync_WithEmptyCart_BlocksRedemptionInsteadOfPreviewingPhantomDiscount()
    {
        // Regression: canjear puntos con el carrito vacío mostraba un descuento (p.ej. 180 pts = 1800)
        // que nunca se aplica (NetPayable neteaba a 0) y que un cambio posterior del carrito borraba.
        var redeem = new RedeemLoyaltyPointsUseCase(new UnusedLoyaltyRepository(), new InMemoryUnitOfWork());
        var viewModel = new CashierViewModel(
            new MultiProductLookupService(),
            new PreviewPaymentUseCase(),
            null,
            redeem)
        {
            SelectedCustomer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true),
            LoyaltyPointsToRedeem = 180
        };

        await viewModel.ApplyRedemptionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0m, viewModel.LoyaltyDiscount);
        Assert.Equal(180, viewModel.LoyaltyPointsToRedeem);
        Assert.Contains("Agrega productos", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectCustomerAsync_WithCurrentTier_AppliesAutomaticTierDiscountToNetPayable()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var product = new ProductLookupResult(Guid.NewGuid(), "Serum", "SER-1", 2_000m, 10m);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 10m);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(product),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            null,
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty))
        {
            SearchText = "serum",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal(200m, viewModel.TierDiscount); // 10% of 2_000
        Assert.Equal(1_800m, viewModel.NetPayable);
        Assert.True(viewModel.HasTierDiscount);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithTieredCustomer_PersistsSaleWithTierDiscountAppliedAsMoney()
    {
        // Regression: CashierCartItemViewModel.Discount is a percentage, but CreateSaleItemRequest.Discount
        // (and SaleItem.Discount) is absolute money — the tier discount must be converted, not passed through
        // raw, or the persisted Sale.Total() silently diverges from what the cashier is shown (NetPayable).
        var product = new Product("Serum", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(10m), new StockQuantity(2m));
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 10m);
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            null,
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty))
        {
            SearchText = "serum",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        viewModel.ReceivedAmount = viewModel.NetPayable;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal(200m, line.Discount.Amount); // 10% of 2_000 gross, as money — not "10" raw
        Assert.Equal(1_800m, sale.Total().Amount); // matches viewModel.NetPayable exactly
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithTierAndManualDiscount_CompoundsBothIndependently()
    {
        // Tier (10%) and the cashier's manual ApplyCartDiscountCommand (20%) are independent and
        // never additive: 100_000 -> 90_000 (tier) -> 72_000 (manual on top), not 100_000 * 0.7.
        var product = new Product("Crema", Guid.NewGuid(), new Money(50_000m), new Money(100_000m), new StockQuantity(10m), new StockQuantity(2m));
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 10m);
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            null,
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty))
        {
            SearchText = "crema",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        viewModel.CartDiscountPercentInput = 20m;
        viewModel.ApplyCartDiscountCommand.Execute(null);

        Assert.Equal(72_000m, viewModel.NetPayable);
        Assert.True(viewModel.HasManualDiscount);

        viewModel.ReceivedAmount = viewModel.NetPayable;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal(28_000m, line.Discount.Amount); // 100_000 - 72_000, persisted as one compounded money discount
        Assert.Equal(72_000m, sale.Total().Amount);
    }

    private sealed class TieredLoyaltyRepository : ILoyaltyRepository
    {
        private readonly Guid _customerId;
        private readonly decimal _discountPercent;
        private readonly LoyaltyAccount _account;
        private readonly LoyaltyRule? _rule;
        private readonly int _balance;

        public TieredLoyaltyRepository(Guid customerId, decimal discountPercent, LoyaltyRule? rule = null, int balance = 500)
        {
            _customerId = customerId;
            _discountPercent = discountPercent;
            _account = new LoyaltyAccount(customerId);
            _rule = rule;
            _balance = balance;
        }

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(customerId == _customerId ? _account : null);

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default) => Task.FromResult(_rule);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default) => Task.FromResult(_balance);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
            // allowsRedemption: the checkout redemption panel only renders for tiers that grant it.
            => Task.FromResult<IReadOnlyList<LoyaltyTier>>(
                [new LoyaltyTier("Oro", 100, _discountPercent, 1m, allowsRedemption: true)]);
    }

    [Theory]
    [InlineData(150, false)]
    [InlineData(200, true)]
    [InlineData(250, true)]
    public async Task SelectCustomerAsync_HidesLoyaltyInfo_UntilBalanceReachesMinimumRedeemPoints(int balance, bool expectedVisible)
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var rule = new LoyaltyRule(pointsPerCurrencyUnit: 1m, redemptionValuePerPoint: 10m, minimumRedeemPoints: 200);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 0m, rule, balance);
        var viewModel = new CashierViewModel(
            new PrototypeProductLookupService(),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            null,
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty));

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal(expectedVisible, viewModel.HasCustomerLoyaltyInfo);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenCartAndPaymentAreValid_PersistsSaleAndResetsCartAfterSuccess()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, authorizer: CreateAuthorizer()))
        {
            SearchText = "rice",
            Quantity = 2m,
            CashierUserId = Guid.NewGuid()
        };
        int completedSales = 0;
        viewModel.SaleCompleted += () => completedSales++;

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 6_000m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.CartItems);
        Assert.Equal(0m, viewModel.TotalAmount);
        Assert.Equal(0m, viewModel.ReceivedAmount);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(5_000m, sale.Total().Amount);
        Assert.Equal(5_000m, sale.CashInDrawer().Amount);
        Assert.Equal(8m, product.CurrentStock);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, completedSales);
    }

    [Theory]
    [InlineData(PaymentMethod.DebitCard, null)]
    [InlineData(PaymentMethod.CreditCard, null)]
    [InlineData(PaymentMethod.Transfer, "Bancolombia")]
    [InlineData(PaymentMethod.Transfer, "Nequi")]
    [InlineData(PaymentMethod.Transfer, "Daviplata")]
    public async Task ConfirmSaleAsync_NonCashMethod_PaysFullTotalWithoutTypingReceivedAmount(
        PaymentMethod method,
        string? platform)
    {
        // Regression: a digital payment has no cash to count, so the cashier never types an
        // amount. ReceivedAmount stayed 0 and the whole total was read as a deferred balance,
        // blocking the sale ("Se requiere identificar al cliente...") on every non-cash method.
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()))
        {
            SearchText = "rice",
            Quantity = 2m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == method);
        if (platform is not null)
        {
            viewModel.BankOrPlatform = platform;
        }

        Assert.Equal(0m, viewModel.ReceivedAmount);
        Assert.Equal(0m, viewModel.DeferredAmount);
        Assert.True(viewModel.CanConfirm, viewModel.Message);

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(5_000m, sale.TotalPaid().Amount);
        Assert.Equal(0m, sale.CashInDrawer().Amount);
        Assert.Equal(method, Assert.Single(sale.Payments).Method);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithSplitPayment_PersistsOnePaymentPerTender()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(5_000m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, authorizer: CreateAuthorizer()))
        {
            SearchText = "rice",
            Quantity = 2m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Total is 10,000 — pay 4,000 cash and 6,000 by transfer.
        viewModel.ReceivedAmount = 4_000m;
        viewModel.AddTenderCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Nequi";
        viewModel.ReceivedAmount = 6_000m;
        viewModel.AddTenderCommand.Execute(null);

        Assert.True(viewModel.CanConfirm);
        Assert.Equal(0m, viewModel.RemainingToPay);

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(10_000m, sale.Total().Amount);
        Assert.Equal(10_000m, sale.TotalPaid().Amount);
        Assert.Equal(4_000m, sale.CashInDrawer().Amount);
        Assert.Equal(2, sale.Payments.Count);
        Assert.Empty(viewModel.Tenders);
    }

    [Fact]
    public async Task ConfirmSaleAsync_AsksForConfirmationAndPersistsNothingUntilTheCashierAgrees()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 2_500m;

        // The customer question is answered first — the confirmation is the last gate before persisting.
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsAwaitingSaleConfirmation);
        Assert.Empty(sales.Sales);
        Assert.Contains("confirmar esta compra", viewModel.Message, StringComparison.OrdinalIgnoreCase);

        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsAwaitingSaleConfirmation);
        Assert.Single(sales.Sales);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenDeclined_KeepsTheCartAndAsksAgainOnTheNextAttempt()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 2_500m;

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        viewModel.ConfirmSaleNoCommand.Execute(null);

        Assert.False(viewModel.IsAwaitingSaleConfirmation);
        Assert.Empty(sales.Sales);
        Assert.Single(viewModel.CartItems);

        // Declining must not leave a spent acknowledgement behind that skips the next prompt.
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsAwaitingSaleConfirmation);
        Assert.Empty(sales.Sales);
    }

    [Fact]
    public async Task SetExactAmountCommand_WithTenderAlreadyAdded_FillsOnlyWhatIsStillOwed()
    {
        // 400.000 sale settled as 300.000 by transfer plus cash: "OK" on the cash leg must offer
        // the 100.000 still owed. Filling the whole total made the second leg over-cover the sale.
        CashierViewModel viewModel = await CreateSplitPaymentViewModelAsync();

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);
        viewModel.SetExactAmountCommand.Execute(null);

        Assert.Equal(100_000m, viewModel.ReceivedAmount);
    }

    [Fact]
    public async Task SetExactAmountCommand_WithoutTenders_StillFillsTheWholeTotal()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(Guid.NewGuid(), "Combo", "7701", 400_000m, 10m)))
        {
            SearchText = "combo",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.SetExactAmountCommand.Execute(null);

        Assert.Equal(400_000m, viewModel.ReceivedAmount);
    }

    [Fact]
    public async Task PaymentBalance_DuringSplitPayment_ReportsWhatIsStillOwed()
    {
        CashierViewModel viewModel = await CreateSplitPaymentViewModelAsync();

        Assert.True(viewModel.IsPaymentShortfall);
        Assert.Equal("Falta", viewModel.PaymentBalanceLabel);
        Assert.Equal(100_000m, viewModel.RemainingToPay);
        Assert.Equal(viewModel.RemainingToPayText, viewModel.PaymentBalanceText);

        // Covering the rest flips the same box back to reporting change.
        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);
        viewModel.SetExactAmountCommand.Execute(null);
        viewModel.AddTenderCommand.Execute(null);

        Assert.False(viewModel.IsPaymentShortfall);
        Assert.Equal("Cambio", viewModel.PaymentBalanceLabel);
        Assert.Equal(0m, viewModel.RemainingToPay);
    }

    /// <summary>400.000 in the cart with a 300.000 Bancolombia transfer already tendered.</summary>
    private static async Task<CashierViewModel> CreateSplitPaymentViewModelAsync()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(Guid.NewGuid(), "Combo", "7701", 400_000m, 10m)))
        {
            SearchText = "combo",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Bancolombia";
        viewModel.ReceivedAmount = 300_000m;
        viewModel.AddTenderCommand.Execute(null);

        return viewModel;
    }

    [Fact]
    public async Task AddTenderCommand_NonCashAmountAboveRemaining_IsDisabled()
    {
        // Regression: a digital tender larger than the remaining balance over-covers the total.
        // Non-cash legs persist at their full amount, so the sale would never complete
        // (Sale.Complete() requires TotalPaid == Total exactly) — the UI must not offer it.
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Equal(2_500m, viewModel.NetPayable);
        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Nequi";
        viewModel.ReceivedAmount = 5_000m;

        Assert.False(viewModel.AddTenderCommand.CanExecute(null));

        // A tender within the remaining balance is still accepted.
        viewModel.ReceivedAmount = 2_500m;
        Assert.True(viewModel.AddTenderCommand.CanExecute(null));
    }

    [Fact]
    public async Task ResumeHeldSaleAsync_ClearsStaleTendersAndDisablesSplitPayment()
    {
        // Regression: tenders added before resuming a held sale used to survive into the resumed
        // sale, where completion ignores them (single-payment path) and can crash with
        // "Cash received amount cannot be lower than payment amount." Split payments apply only
        // to new full sales.
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        Guid cashierId = Guid.NewGuid();
        var sale = new Sale(cashierId);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.Hold();
        sales.Sales.Add(sale);

        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            holdSaleUseCase: null,
            getHeldSalesUseCase: new GetHeldSalesUseCase(sales),
            resumeHeldSaleUseCase: new ResumeHeldSaleUseCase(sales, new InMemoryUnitOfWork()),
            completeHeldSaleUseCase: null,
            cancelHeldSaleUseCase: null)
        {
            CashierUserId = cashierId
        };

        // Stale tender from a previous new sale — must not survive into the resumed sale.
        viewModel.Tenders.Add(new PaymentTenderViewModel(PaymentMethod.Transfer, "Transferencia", 2_500m, "Nequi"));

        var held = new HeldSaleSummary(
            sale.Id, cashierId, null, sale.InvoiceNumber, sale.CreatedAt, 2_500m,
            [new HeldSaleItem(product.Id, product.Name, 1m, 2_500m, 0m, null, 1)]);

        await viewModel.ResumeHeldSaleAsync(held, TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsResumedSale);
        Assert.Empty(viewModel.Tenders);
        Assert.False(viewModel.AddTenderCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddingAsBox_ChargesBoxPrice_AndDecrementsStockInBaseUnits()
    {
        var product = new Product(
            "Ampolla 350",
            Guid.NewGuid(),
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(24m),
            new StockQuantity(0m),
            presentations: [("Caja", 12, new Money(110_000m))]);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(
                product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock, 0m,
                [new ProductPresentationDto("Caja", 12, 110_000m)])),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, authorizer: CreateAuthorizer()))
        {
            SearchText = "ampolla",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedPresentationOption = viewModel.AvailablePresentations.First(p => p.Name == "Caja");
        viewModel.AddSelectedProductCommand.Execute(null);

        CashierCartItemViewModel line = Assert.Single(viewModel.CartItems);
        Assert.Equal(110_000m, line.UnitPrice);
        Assert.Equal("Caja", line.PresentationName);
        Assert.Equal(12m, line.InventoryUnits);

        viewModel.ReceivedAmount = 110_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(110_000m, sale.Total().Amount);
        Assert.Equal(12m, product.CurrentStock);
    }

    [Fact]
    public async Task AddingAHalfBox_ChargesTheHalfBoxPrice_AndDecrementsItsConfiguredUnits()
    {
        var product = new Product(
            "Ampolla 350",
            Guid.NewGuid(),
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(24m),
            new StockQuantity(0m),
            presentations: [("Caja", 12, new Money(110_000m)), ("Media caja", 6, new Money(57_000m))]);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(
                product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock, 0m,
                [new ProductPresentationDto("Caja", 12, 110_000m), new ProductPresentationDto("Media caja", 6, 57_000m)])),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, authorizer: CreateAuthorizer()))
        {
            SearchText = "ampolla",
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedPresentationOption = viewModel.AvailablePresentations.First(p => p.Name == "Media caja");
        viewModel.Quantity = 2m;
        viewModel.AddSelectedProductCommand.Execute(null);

        CashierCartItemViewModel line = Assert.Single(viewModel.CartItems);
        Assert.Equal("Media caja", line.PresentationName);
        // Two half boxes cost 2 * 57.000 - not one box (110.000), and not 12 * 10.000.
        Assert.Equal(114_000m, line.Subtotal);
        Assert.Equal(12m, line.InventoryUnits);

        viewModel.ReceivedAmount = 114_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(114_000m, sale.Total().Amount);
        Assert.Equal(12m, product.CurrentStock);
    }

    [Fact]
    public async Task Quantity_IsAlwaysWhole_EvenForAPackagedPresentation()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(
                Guid.NewGuid(), "Ampolla 350", "7701", 10_000m, 24m, 0m,
                [new ProductPresentationDto("Caja", 12, 110_000m)])))
        {
            SearchText = "ampolla"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedPresentationOption = viewModel.AvailablePresentations.First(p => p.Name == "Caja");

        viewModel.Quantity = 0.5m;
        Assert.Equal(1m, viewModel.Quantity);

        viewModel.Quantity = 2.75m;
        Assert.Equal(2m, viewModel.Quantity);
    }

    [Fact]
    public async Task SelectedPresentation_WhenTheProductDoesNotOfferIt_FallsBackToUnit()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(
                Guid.NewGuid(), "Ampolla 350", "7701", 10_000m, 24m, 0m,
                [new ProductPresentationDto("Caja", 12, 110_000m)])))
        {
            SearchText = "ampolla"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        // Asking for a non-existent presentation falls back to Unidad option (null Name)
        Assert.Null(viewModel.SelectedProduct!.PackageFor("Cuarto de caja"));
        Assert.True(viewModel.SelectedPresentationOption!.IsUnit);

        viewModel.AddSelectedProductCommand.Execute(null);
        CashierCartItemViewModel line = Assert.Single(viewModel.CartItems);
        Assert.Null(line.PresentationName);
        Assert.Equal(10_000m, line.UnitPrice);
    }


    [Fact]
    public async Task ConfirmSaleAsync_WhenCartIsEmpty_DoesNotPersistAndShowsRecoverableMessage()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()))
        {
            CashierUserId = Guid.NewGuid(),
            ReceivedAmount = 10_000m
        };

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sales.Sales);
        Assert.Contains("El carrito está vacío", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_FindsProductsByNameAndSelectsFirstResult()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(viewModel.SearchResults);
        Assert.Equal("Arroz 500g", viewModel.SelectedProduct?.Name);
        Assert.Contains("Selecciona", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchText_WhenTyped_AutocompletesProductsWithoutPressingSearch()
    {
        var lookup = new StaticProductLookupService(new ProductLookupResult(
            Guid.NewGuid(),
            "Exfoliante facial",
            "EXF-001",
            25_000m,
            8m));
        var viewModel = new CashierViewModel(lookup)
        {
            SearchText = "exfoliante"
        };

        await Task.Delay(350, TestContext.Current.CancellationToken);

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Equal("Exfoliante facial", result.Name);
        Assert.Equal(result, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task ClearSearchCommand_EmptiesTheSearchBoxAndItsResults()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(viewModel.SearchResults);
        Assert.True(viewModel.ClearSearchCommand.CanExecute(null));

        viewModel.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Empty(viewModel.SearchResults);
        Assert.Null(viewModel.SelectedProduct);
        // Nothing left to clear, so the button stops inviting a click.
        Assert.False(viewModel.ClearSearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddSelectedProduct_AddsItemAndCalculatesTotal()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "aceite",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Single(viewModel.CartItems);
        Assert.Equal(19600m, viewModel.TotalAmount);
        Assert.Equal("$19.600", viewModel.TotalText);
        Assert.Contains("agregado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_ExposesPresentationStockStatusForDropdown()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(Guid.NewGuid(), "Low stock item", "LOW-1", 1_500m, 5m, 5m)))
        {
            SearchText = "low"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Equal("Stock bajo", result.StockStatusLabel);
        Assert.Equal("warning", result.StockStatusKind);
        Assert.Equal("$1.500", result.UnitPriceText);
    }

    [Fact]
    public async Task SearchAsync_ExposesOutOfStockStatusWhenAvailableStockIsZero()
    {
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(Guid.NewGuid(), "Empty item", "OUT-1", 1_500m, 0m, 5m)))
        {
            SearchText = "empty"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Equal("Sin stock", result.StockStatusLabel);
        Assert.Equal("error", result.StockStatusKind);
    }

    [Fact]
    public async Task CartItemQuantityChange_RecomputesSubtotalSummaryAndPaymentBalance()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 10_000m;
        CashierCartItemViewModel item = Assert.Single(viewModel.CartItems);

        item.Quantity = 3m;

        Assert.Equal(7_500m, item.Subtotal);
        Assert.Equal(7_500m, viewModel.TotalAmount);
        Assert.Equal("1 productos · 3 unidades", viewModel.CartSummaryText);
        Assert.Equal("$2.500", viewModel.PaymentBalanceText);
    }

    [Fact]
    public async Task SecondaryActions_ArePresentationOnlyAndExposeRecoveryMessages()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.True(viewModel.AssociateCustomerCommand.CanExecute(null));
        Assert.True(viewModel.HoldSaleCommand.CanExecute(null));

        viewModel.AssociateCustomerCommand.Execute(null);
        Assert.Contains("cliente", viewModel.Message, StringComparison.OrdinalIgnoreCase);

        viewModel.HoldSaleCommand.Execute(null);
        Assert.Contains("Requiere un caso de uso", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithoutCustomer_OpensAssignmentBeforePersistingAndCanContinueWithoutCustomer()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([])))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sales.Sales);
        Assert.True(viewModel.IsCustomerAssignmentOpen);
        Assert.Contains("Antes de facturar", viewModel.Message, StringComparison.OrdinalIgnoreCase);

        // The customer question comes first; only then is the sale offered for confirmation.
        await viewModel.ContinueWithoutCustomerAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Null(sale.CustomerId);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithSelectedCustomer_PersistsCustomerIdForHistoryAuditAndPoints()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(1_000m), new Money(2_500m), new StockQuantity(10m), new StockQuantity(2m));
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var sales = new InMemorySaleRepository();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), authorizer: CreateAuthorizer()),
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])))
        {
            SearchText = "rice",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        viewModel.ReceivedAmount = 3_000m;
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        viewModel.CustomerSearchText = "Ana";
        await viewModel.SearchCustomersAsync(TestContext.Current.CancellationToken);
        CustomerSearchResult selected = Assert.Single(viewModel.CustomerSearchResults);

        await viewModel.SelectCustomerAsync(selected, TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(customer.Id, sale.CustomerId);
        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceivedCash_WhenEnough_CalculatesChangeAndAllowsPrototypeConfirmation()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 10000m;

        Assert.True(viewModel.CanConfirm);
        Assert.Equal(5000m, viewModel.ChangeAmount);
        Assert.Equal("$5.000", viewModel.ChangeText);
    }

    [Fact]
    public async Task ReceivedCash_WhenLowerThanTotal_DisablesConfirmationUntilCustomerIsIdentified()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 1000m;
        viewModel.CommitReceivedAmount(); // simulates pressing Enter / leaving the field

        // Underpaying now auto-derives a deferred balance, which requires an identified customer.
        // It also exceeds the default 50% deferred cap (10.200 of 11.200), so the panel stays
        // hidden on both counts.
        Assert.False(viewModel.CanConfirm);
        Assert.Equal(0m, viewModel.ChangeAmount);
        Assert.False(viewModel.IsDeferredAmountVisible);
        Assert.Equal(10_200m, viewModel.DeferredAmount);
        Assert.Contains("cliente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.IsPaymentShortfall);
        Assert.Equal("Falta", viewModel.PaymentBalanceLabel);
        Assert.Equal("$10.200", viewModel.PaymentBalanceText);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenCartIsEmpty_ShowsWireframeEmptyCartMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            ReceivedAmount = 10_000m
        };

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.Contains("El carrito está vacío", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSelectedProduct_WhenQuantityExceedsStock_ShowsRecoveryMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 99m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Empty(viewModel.CartItems);
        Assert.Contains("Solo hay", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddProductAndResetSearch_AddsToCartAndClearsSearch()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        CashierProductSearchResultViewModel product = viewModel.SearchResults[0];

        bool added = viewModel.AddProductAndResetSearch(product);

        Assert.True(added);
        Assert.Single(viewModel.CartItems);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Empty(viewModel.SearchResults);
        Assert.Null(viewModel.SelectedProduct);
        Assert.Contains("agregado", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddProductAndResetSearch_WhenStockInsufficient_KeepsSearchState()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "café",
            Quantity = 99m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        CashierProductSearchResultViewModel product = viewModel.SearchResults[0];

        bool added = viewModel.AddProductAndResetSearch(product);

        Assert.False(added);
        Assert.Empty(viewModel.CartItems);
        Assert.Equal("café", viewModel.SearchText);
        Assert.NotEmpty(viewModel.SearchResults);
        Assert.Contains("Solo hay", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveSelection_NavigatesAndClampsWithinResults()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "a"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.SearchResults.Count >= 2);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);

        viewModel.MoveSelection(1);
        Assert.Equal(viewModel.SearchResults[1], viewModel.SelectedProduct);

        viewModel.MoveSelection(-1);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);

        viewModel.MoveSelection(-1);
        Assert.Equal(viewModel.SearchResults[0], viewModel.SelectedProduct);
    }

    [Fact]
    public async Task SearchAsync_WhenNoResults_SetsHasNoMatches()
    {
        var viewModel = new CashierViewModel(new EmptyProductLookupService())
        {
            SearchText = "zzz"
        };

        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasNoMatches);

        viewModel.SearchText = string.Empty;
        Assert.False(viewModel.HasNoMatches);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WithMissingCode_ShowsMessageAndLeavesCartUnchanged()
    {
        var existing = new ProductLookupResult(Guid.NewGuid(), "Already in cart", "KNOWN-001", 5_000m, 10m);
        var viewModel = new CashierViewModel(new StaticProductLookupService(existing));
        viewModel.AddProductAndResetSearch(new CashierProductSearchResultViewModel(existing));
        decimal totalBeforeScan = viewModel.TotalAmount;
        viewModel.SearchText = "MISSING-001";

        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.CartItems);
        Assert.Equal(totalBeforeScan, viewModel.TotalAmount);
        Assert.Equal("MISSING-001", viewModel.SearchText);
        Assert.Contains("No encontramos un producto activo", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WhenReentered_AddsTheProductOnlyOnce()
    {
        var product = new ProductLookupResult(Guid.NewGuid(), "Scanner product", "RACE-001", 5_000m, 10m);
        var lookup = new BlockingProductLookupService(product);
        var viewModel = new CashierViewModel(lookup)
        {
            SearchText = "RACE-001"
        };

        Task firstScan = viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);
        await lookup.LookupStarted.Task;
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);
        lookup.CompleteLookup();
        await firstScan;

        Assert.Equal(1, lookup.BarcodeLookupCount);
        Assert.Single(viewModel.CartItems);
        Assert.Equal(1m, viewModel.CartItems[0].Quantity);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WhenAutocompleteHoldsSemaphore_WaitsAndAddsExactlyOneItem()
    {
        var scannerProduct = new ProductLookupResult(Guid.NewGuid(), "Scanner Product", "SCAN-CODE", 5_000m, 10m);
        var searchBlocker = new TaskCompletionSource<IReadOnlyList<ProductLookupResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var searchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookup = new BlockingSearchProductLookupService(scannerProduct, searchBlocker, searchStarted);
        var viewModel = new CashierViewModel(lookup)
        {
            SearchText = "anything" // triggers autocomplete → SearchAsync blocks holding semaphore
        };

        // Wait until autocomplete's SearchAsync has started (and acquired the semaphore)
        await searchStarted.Task;

        // Scanner tries to acquire the semaphore — blocked
        viewModel.SearchText = "SCAN-CODE";
        Task scanTask = viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        // Give the scanner a moment to reach the semaphore wait
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Empty(viewModel.CartItems); // scanner hasn't added yet

        // Release autocomplete, allowing scanner to proceed
        searchBlocker.TrySetResult([]);
        await scanTask;

        Assert.Single(viewModel.CartItems);
        Assert.Equal("Scanner Product", viewModel.CartItems[0].ProductName);
    }

    [Fact]
    public async Task HandleScannerInputAsync_WithExactBarcode_UsesFreshLookupAndAddsOnlyTheExactProduct()
    {
        var first = new ProductLookupResult(Guid.NewGuid(), "Arroz 500g", "ARR-1", 5_000m, 10m);
        var second = new ProductLookupResult(Guid.NewGuid(), "Azúcar 500g", "AZU-1", 4_000m, 10m);
        var viewModel = new CashierViewModel(new MultiProductLookupService(first, second))
        {
            SearchText = "AZU-1"
        };
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Azúcar 500g", Assert.Single(viewModel.CartItems).ProductName);
        Assert.Equal(string.Empty, viewModel.SearchText);
    }

    [Fact]
    public async Task HandleSearchEnterAsync_WithAlphanumericBarcodeAndSelectedSuggestion_AddsExactBarcodeProduct()
    {
        var suggestedProduct = new ProductLookupResult(Guid.NewGuid(), "Producto sugerido", "ABC-100", 5_000m, 10m);
        var barcodeProduct = new ProductLookupResult(Guid.NewGuid(), "Producto del código", "ABC-123", 4_000m, 10m);
        var viewModel = new CashierViewModel(new MultiProductLookupService(suggestedProduct, barcodeProduct))
        {
            SearchText = "ABC-123"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(suggestedProduct.ProductId, viewModel.SelectedProduct?.ProductId);

        await viewModel.HandleSearchEnterAsync(
            isSuggestionNavigated: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(barcodeProduct.ProductId, Assert.Single(viewModel.CartItems).ProductId);
    }

    [Fact]
    public async Task HandleSearchEnterAsync_WithTypedTextAndSelectedSuggestion_AddsSuggestionWhenNoExactBarcodeMatches()
    {
        var suggestedProduct = new ProductLookupResult(Guid.NewGuid(), "Producto sugerido", "ABC-100", 5_000m, 10m);
        var viewModel = new CashierViewModel(new StaticProductLookupService(suggestedProduct))
        {
            SearchText = "Producto sugerido"
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);

        await viewModel.HandleSearchEnterAsync(
            isSuggestionNavigated: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(suggestedProduct.ProductId, Assert.Single(viewModel.CartItems).ProductId);
    }

    [Fact]
    public async Task ConfirmCustomerSelectionAsync_AssignsKeyboardSelectedCustomer()
    {
        var first = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var second = new CustomerSearchResult(Guid.NewGuid(), "Beatriz Cliente", "1002", "301", null, null, true);
        var viewModel = new CashierViewModel(
            new PrototypeProductLookupService(),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([first, second])))
        {
            CustomerSearchText = "cliente"
        };

        await viewModel.SearchCustomersAsync(TestContext.Current.CancellationToken);
        viewModel.MoveCustomerSelection(1);
        await viewModel.ConfirmCustomerSelectionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(second.Id, viewModel.SelectedCustomer?.Id);
    }

    [Fact]
    public async Task SelectCustomerAsync_WhenCustomerHasOpenQuote_ShowsPendingQuotesForCashier()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-001", customer.Id, productId, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        CustomerPayableQuote pending = Assert.Single(viewModel.CustomerQuotes);
        Assert.Equal("COT-CAJA-001", pending.QuoteNumber);
        Assert.True(viewModel.HasCustomerQuotes);
        Assert.Contains("cotización", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCustomerQuoteAsync_LoadsQuoteItemsIntoCashierCartWithDiscountedTotal()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-002", customer.Id, productId, discount: 10m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);

        CashierCartItemViewModel item = Assert.Single(viewModel.CartItems);
        Assert.Equal("Serum", item.ProductName);
        Assert.Equal(10m, item.Discount);
        Assert.Equal(1_800m, viewModel.TotalAmount);
        Assert.True(viewModel.IsQuoteLoaded);
        Assert.Contains("COT-CAJA-002", viewModel.LoadedQuoteBanner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCustomerQuoteAsync_WithTieredCustomer_SuppressesTierDiscount()
    {
        // Regression: a quote's price is already fixed when it's created, and confirming it
        // (ConvertQuoteToSaleUseCase) charges TotalAmount, never NetPayable — but TierDiscount was an
        // unconditional getter, so the UI showed an extra tier discount on top of the quote's own
        // discount that was never actually charged. Loading a quote must suppress it.
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-TIER", customer.Id, productId, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 10m);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty));

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);

        Assert.Equal(2_000m, viewModel.TotalAmount); // sanity: tier % would have applied to this
        Assert.Equal(0m, viewModel.TierDiscount);
        Assert.False(viewModel.HasTierDiscount);
        Assert.Equal(viewModel.TotalAmount, viewModel.NetPayable);
    }

    [Fact]
    public async Task ApplyRedemptionAsync_WithQuoteLoaded_BlocksRedemption()
    {
        // Same root cause as the tier-discount suppression above: ConvertQuoteToSaleUseCase charges
        // TotalAmount, so a loyalty redemption applied on top of a loaded quote would show a discount
        // on screen that never gets charged.
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-PTS", customer.Id, productId, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var redeem = new RedeemLoyaltyPointsUseCase(new UnusedLoyaltyRepository(), new InMemoryUnitOfWork());
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            redeem,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo));

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);
        viewModel.LoyaltyPointsToRedeem = 50;

        await viewModel.ApplyRedemptionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0m, viewModel.LoyaltyDiscount);
        Assert.Contains("cotización", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCustomerQuoteAsync_ClearsStaleTendersAndQuoteIsConfirmable()
    {
        // Regression: tenders added before loading a customer quote used to survive into the
        // quote. Completion ignores Tenders (single-payment path), so a stale non-cash tender
        // either gated confirmation on a split the operator can never edit (CanConfirm=false
        // dead-end on a partially covering tender) or silently recorded the sale as Cash while
        // the tender panel showed a digital tender.
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var productId = Guid.NewGuid();
        var quote = CreateCustomerQuote("COT-CAJA-CLEAR", customer.Id, productId, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(productId, "Serum", "SER-1", 2_000m, 10m)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            null);

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        // Stale tender from a previous new sale — must not survive into the loaded quote.
        viewModel.Tenders.Add(new PaymentTenderViewModel(PaymentMethod.Transfer, "Transferencia", 1_000m, "Nequi"));

        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsQuoteLoaded);
        Assert.Empty(viewModel.Tenders);
        Assert.False(viewModel.HasTenders);
        // The quote covers its own total with received cash — no split tenders required.
        Assert.Equal(2_000m, viewModel.TotalAmount);
        Assert.True(viewModel.CanConfirm);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WhenQuoteLoaded_ConvertsOriginalQuoteAndCreatesInvoice()
    {
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var product = new Product("Serum", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(10m), new StockQuantity(2m));
        var quote = CreateCustomerQuote("COT-CAJA-003", customer.Id, product.Id, discount: 0m);
        var quoteRepo = new InMemoryQuoteRepository(quote);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var createSaleUseCase = new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, authorizer: CreateAuthorizer());
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "SER-1", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            new GetCustomerPayableQuotesUseCase(quoteRepo),
            new ConvertQuoteToSaleUseCase(quoteRepo, createSaleUseCase, unitOfWork))
        {
            CashierUserId = Guid.NewGuid()
        };

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        await viewModel.LoadCustomerQuoteAsync(viewModel.CustomerQuotes.Single(), TestContext.Current.CancellationToken);
        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(sale.Id, quote.ConvertedSaleId);
        Assert.Empty(viewModel.CartItems);
        Assert.Contains("facturada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TaxTotal_WithMixedRateCart_ExtractsVatPerProductRateWithoutChangingTheTotal()
    {
        // The shop reported that changing a product's IVA "does nothing". Prices are VAT-inclusive,
        // so the total is right to stay put — but the breakdown must move, and must move per product.
        var taxed = new ProductLookupResult(Guid.NewGuid(), "Gravado 19", "IVA-19", 11_900m, 10m, TaxRate: 19m);
        var exempt = new ProductLookupResult(Guid.NewGuid(), "Exento", "IVA-00", 5_000m, 10m, TaxRate: 0m);
        var viewModel = new CashierViewModel(new MultiProductLookupService(taxed, exempt))
        {
            SearchText = "IVA-19"
        };
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);
        viewModel.SearchText = "IVA-00";
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.CartItems.Count);
        Assert.Equal(16_900m, viewModel.TotalAmount);
        Assert.Equal(1_900m, viewModel.TaxTotal);          // 11_900 * 19 / 119, exempt line adds nothing
        Assert.Equal(15_000m, viewModel.TaxableBaseTotal);
        Assert.Equal(viewModel.TotalAmount - viewModel.TierDiscount, viewModel.TaxableBaseTotal + viewModel.TaxTotal);
    }

    [Fact]
    public async Task TaxableBaseTotal_WithActiveTierDiscount_MatchesTheDomainsPerLineRoundedBase()
    {
        // Regression: TaxableBaseTotal must not be TotalAmount - TierDiscount(unrounded) - TaxTotal —
        // that reuses the aggregate tier percentage, while the domain (and BuildLineRequests) rounds
        // the discount independently per line. The two disagree by a cent even on a single line, and
        // an invariant assertion built from TaxableBaseTotal's own formula can't catch that drift —
        // this asserts against numbers worked out independently of the production formula.
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var product = new ProductLookupResult(Guid.NewGuid(), "Gravado", "GRA-1", 33.34m, 10m, TaxRate: 19m);
        var loyalty = new TieredLoyaltyRepository(customer.Id, discountPercent: 10m);
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(product),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            null,
            null,
            null,
            new GetLoyaltyBalanceUseCase(loyalty))
        {
            SearchText = "GRA-1",
            Quantity = 1m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);
        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        // gross 33.34, 10% tier discount rounded per line to 3.33 (not the unrounded 3.334) -> subtotal 30.01
        Assert.Equal(30.01m, viewModel.TotalAmount - 3.33m);
        Assert.Equal(4.79m, viewModel.TaxTotal);            // round(30.01 * 19 / 119, 2)
        Assert.Equal(25.22m, viewModel.TaxableBaseTotal);   // 30.01 - 4.79, matching Sale.TaxableBaseTotal()
    }

    [Fact]
    public async Task TaxTotal_WhenProductRateIsZeroed_DropsToZeroWhileTheTotalStaysTheSame()
    {
        var exemptNow = new ProductLookupResult(Guid.NewGuid(), "Gravado 19", "IVA-19", 11_900m, 10m, TaxRate: 0m);
        var viewModel = new CashierViewModel(new MultiProductLookupService(exemptNow))
        {
            SearchText = "IVA-19"
        };
        await viewModel.HandleScannerInputAsync(TestContext.Current.CancellationToken);

        Assert.Equal(11_900m, viewModel.TotalAmount);
        Assert.Equal(0m, viewModel.TaxTotal);
        Assert.Equal(11_900m, viewModel.TaxableBaseTotal);
    }

    private sealed class EmptyProductLookupService : POS.Application.Interfaces.IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([]);
    }

    private sealed class SettingsRepositoryFake(decimal maximumDeferredBalancePercent) : ISettingsRepository
    {
        public decimal MaximumDeferredBalancePercent { get; set; } = maximumDeferredBalancePercent;

        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessSettings?>(new BusinessSettings(
                "Test Business",
                null, null, null, null, null, null, null, false, null, null, null, null, null,
                "COP",
                maximumDeferredBalancePercent: MaximumDeferredBalancePercent));

        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StaticProductLookupService : POS.Application.Interfaces.IProductLookupService
    {
        private readonly ProductLookupResult _product;

        public StaticProductLookupService(ProductLookupResult product) => _product = product;

        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([_product]);

        public Task<ProductLookupResult?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLookupResult?>(string.Equals(_product.Barcode, barcode.Trim(), StringComparison.Ordinal) ? _product : null);
    }

    private sealed class MultiProductLookupService(params ProductLookupResult[] products) : POS.Application.Interfaces.IProductLookupService
    {
        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>(products);

        public Task<ProductLookupResult?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(products.SingleOrDefault(product => string.Equals(product.Barcode, barcode.Trim(), StringComparison.Ordinal)));
    }

    private sealed class BlockingProductLookupService(ProductLookupResult product) : IProductLookupService
    {
        private readonly TaskCompletionSource<ProductLookupResult?> _lookupCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> LookupStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int BarcodeLookupCount { get; private set; }

        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductLookupResult>>([]);

        public Task<ProductLookupResult?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            BarcodeLookupCount++;
            LookupStarted.TrySetResult(true);
            return _lookupCompletion.Task;
        }

        public void CompleteLookup() => _lookupCompletion.TrySetResult(product);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product _product;

        public InMemoryProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_product.Barcode == barcode ? _product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Sales.FirstOrDefault(sale => sale.Id == id));

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class StaticCustomerLookupQueryService : ICustomerLookupQueryService
    {
        private readonly IReadOnlyList<CustomerSearchResult> _customers;

        public StaticCustomerLookupQueryService(IReadOnlyList<CustomerSearchResult> customers) => _customers = customers;

        public Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_customers);
    }

    private static Quote CreateCustomerQuote(string quoteNumber, Guid customerId, Guid productId, decimal discount)
    {
        var quote = new Quote(
            quoteNumber,
            "Ana Cliente",
            30,
            customerId,
            customerPhone: "+57 300 123 4567",
            customerEmail: null);
        quote.AddItem(new QuoteItem(productId, "Serum", new Money(2_000m), new Quantity(1m), discount));
        return quote;
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        private readonly Dictionary<Guid, Quote> _quotes = new();

        public InMemoryQuoteRepository(params Quote[] quotes)
        {
            foreach (Quote quote in quotes)
            {
                _quotes[quote.Id] = quote;
            }
        }

        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_quotes.TryGetValue(id, out Quote? quote) ? quote : null);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("COT-AUTO");

        public Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
        {
            _quotes[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default)
        {
            _quotes[quote.Id] = quote;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from, DateOnly? to, string? customerName, QuoteStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Quote>, int)>(([.. _quotes.Values], _quotes.Count));

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Quote>>(_quotes.Values
                .Where(q => q.CustomerId == customerId && q.Status is QuoteStatus.Draft or QuoteStatus.Active && q.ExpiresAt >= DateTime.UtcNow)
                .ToArray());
    }

    // --- RED: settlement plan / deferred balance in CashierViewModel ---

    [Fact]
    public async Task DeferredAmount_WhenReceivedAmountIsBelowTotal_AutoCompletesToShortfallAndShowsCreditMessage()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m // default is 0 (opt-in) — this test isn't about the cap.
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Cashier enters less than the total — DeferredAmount auto-derives the shortfall.
        decimal total = viewModel.TotalAmount;
        viewModel.ReceivedAmount = total - 1m;
        viewModel.CommitReceivedAmount(); // simulates pressing Enter / leaving the field

        Assert.True(viewModel.CanConfirm);
        Assert.True(viewModel.IsDeferredAmountVisible);
        Assert.Equal(1m, viewModel.DeferredAmount);
        Assert.Contains("crédito", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSelectedProduct_WithDefaultReceivedAmount_DoesNotRevealDeferredPanel()
    {
        // Regression: adding a product recomputes totals (NetPayable > 0) while ReceivedAmount is
        // still its default 0, so DeferredAmount is already > 0 — but nothing has been committed
        // yet, so the panel must stay hidden.
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid()
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.True(viewModel.DeferredAmount > 0m);
        Assert.False(viewModel.IsDeferredAmountVisible);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithDeferredBalance_WhenNoCustomerSet_CannotConfirm()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m
            // CustomerId not set
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Underpaying auto-derives a deferred balance without a customer assigned.
        viewModel.ReceivedAmount = 0m;

        // Without a customer, deferred should block confirmation
        Assert.False(viewModel.CanConfirm);
    }

    [Fact]
    public async Task SettlementPlan_WhenCollectedAmountPlusDeferredEqualsTotal_CanConfirm()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 2m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m // default is 0 (opt-in) — this test isn't about the cap.
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        decimal total = viewModel.TotalAmount;
        // Pay half in cash; the other half is auto-derived as the deferred balance.
        viewModel.ReceivedAmount = total / 2m;

        Assert.True(viewModel.CanConfirm);
        Assert.Equal(total / 2m, viewModel.DeferredAmount);
        Assert.Contains("crédito", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]    // exact match
    [InlineData(1000)] // overpayment
    public async Task DeferredAmount_WhenReceivedAmountCoversTotal_StaysHiddenAndZero(int extraReceived)
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid()
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = viewModel.TotalAmount + extraReceived;

        Assert.Equal(0m, viewModel.DeferredAmount);
        Assert.False(viewModel.IsDeferredAmountVisible);
    }

    [Fact]
    public async Task DeferredAmount_WhenUnderpaid_StaysHiddenUntilCommitted()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m // default is 0 (opt-in) — this test isn't about the cap.
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        decimal total = viewModel.TotalAmount;
        viewModel.ReceivedAmount = total - 1m;

        // Not real time: the panel stays hidden while typing, even though the underlying
        // amount is already short of the total.
        Assert.False(viewModel.IsDeferredAmountVisible);
        Assert.Equal(1m, viewModel.DeferredAmount);

        // Simulates the cashier pressing Enter or leaving the field.
        viewModel.CommitReceivedAmount();
        Assert.True(viewModel.IsDeferredAmountVisible);
    }

    [Fact]
    public async Task DeferredAmount_WhenReceivedAmountChangesAfterCommit_HidesPanelAgain()
    {
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m // default is 0 (opt-in) — this test isn't about the cap.
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        decimal total = viewModel.TotalAmount;
        viewModel.ReceivedAmount = total - 1m;
        viewModel.CommitReceivedAmount();
        Assert.True(viewModel.IsDeferredAmountVisible);

        // Editing the amount again invalidates the stale commit until it's committed again.
        viewModel.ReceivedAmount = total - 2m;
        Assert.False(viewModel.IsDeferredAmountVisible);
    }

    // --- Multi-tender + deferred balance unification ---

    [Fact]
    public async Task AddTender_TwoTendersLeavingRemainderWithCustomer_ShowsDeferredPanelWithoutCommit()
    {
        var product = new Product("Combo", Guid.NewGuid(), new Money(200_000m), new Money(400_000m), new StockQuantity(10m), new StockQuantity(1m));
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)))
        {
            SearchText = "combo",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m // default is 0 (opt-in) — this test isn't about the cap.
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        // Total 400.000: transfer 200.000 + cash 100.000 leaves 100.000 unassigned — within the
        // configured 50% maximum deferred (200.000).
        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Bancolombia";
        viewModel.ReceivedAmount = 200_000m;
        viewModel.AddTenderCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);
        viewModel.ReceivedAmount = 100_000m;
        viewModel.AddTenderCommand.Execute(null);

        // No CommitReceivedAmount() call — the tenders themselves drive the panel.
        Assert.Equal(100_000m, viewModel.RemainingToPay);
        Assert.Equal(100_000m, viewModel.DeferredAmount);
        Assert.True(viewModel.IsDeferredAmountVisible);
        Assert.True(viewModel.CanConfirm);
    }

    [Fact]
    public async Task AddTender_TwoTendersLeavingRemainderWithoutCustomer_CannotConfirm()
    {
        var product = new Product("Combo", Guid.NewGuid(), new Money(200_000m), new Money(400_000m), new StockQuantity(10m), new StockQuantity(1m));
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)))
        {
            SearchText = "combo",
            Quantity = 1m
            // CustomerId not set
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Bancolombia";
        viewModel.ReceivedAmount = 200_000m;
        viewModel.AddTenderCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);
        viewModel.ReceivedAmount = 100_000m;
        viewModel.AddTenderCommand.Execute(null);

        Assert.False(viewModel.CanConfirm);
        Assert.Contains("cliente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeferredAmount_WhenExceedsMaximumDeferredPercent_StaysHiddenAndBlocksConfirm()
    {
        // Total 40.000, cap set to 50% → max deferred is 20.000. Paying 19.000 would leave
        // 21.000 deferred, over the cap, so the panel must not appear yet.
        var product = new Product("Producto", Guid.NewGuid(), new Money(20_000m), new Money(40_000m), new StockQuantity(10m), new StockQuantity(1m));
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "9001", product.SalePrice.Amount, product.CurrentStock)))
        {
            SearchText = "producto",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 19_000m;
        viewModel.CommitReceivedAmount();

        Assert.Equal(21_000m, viewModel.DeferredAmount);
        Assert.False(viewModel.IsDeferredAmountVisible);
        Assert.False(viewModel.CanConfirm);
        Assert.Contains("50%", viewModel.Message);
    }

    [Fact]
    public async Task DeferredAmount_AtMaximumDeferredPercent_BecomesVisibleAndAllowsConfirm()
    {
        // Same sale, but paying exactly 20.000 (the 50% cap) leaves 20.000 deferred — allowed.
        var product = new Product("Producto", Guid.NewGuid(), new Money(20_000m), new Money(40_000m), new StockQuantity(10m), new StockQuantity(1m));
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "9001", product.SalePrice.Amount, product.CurrentStock)))
        {
            SearchText = "producto",
            Quantity = 1m,
            CustomerId = Guid.NewGuid(),
            MaximumDeferredBalancePercent = 50m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ReceivedAmount = 20_000m;
        viewModel.CommitReceivedAmount();

        Assert.Equal(20_000m, viewModel.DeferredAmount);
        Assert.True(viewModel.IsDeferredAmountVisible);
        Assert.True(viewModel.CanConfirm);
    }

    [Fact]
    public async Task DeferredAmount_WithDefaultCap_IsAlwaysBlockedUntilBusinessOptsIn()
    {
        // The deferred balance cap now defaults to 0% — no credit is accepted anywhere until the
        // business explicitly raises MaximumDeferredBalancePercent from Ajustes.
        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SearchText = "arroz",
            Quantity = 1m,
            CustomerId = Guid.NewGuid()
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Equal(0m, viewModel.MaximumDeferredBalancePercent);

        viewModel.ReceivedAmount = viewModel.TotalAmount - 1m;
        viewModel.CommitReceivedAmount();

        Assert.False(viewModel.IsDeferredAmountVisible);
        Assert.False(viewModel.CanConfirm);
        Assert.Contains("0%", viewModel.Message);
    }

    [Fact]
    public async Task SelectCustomerAsync_RefreshesDeferredBalanceCapWithoutRequiringRestart()
    {
        // Regression: MaximumDeferredBalancePercent was only fetched once, from CashierView's
        // Loaded event — a cap change from Ajustes kept being ignored for the rest of the app
        // session (read by users as "solo funciona si reinicias la app"). Customer selection is a
        // cheap, natural point to re-read it, mirroring the existing loyalty-balance refresh.
        var settings = new SettingsRepositoryFake(10m);
        var customer = new CustomerSearchResult(Guid.NewGuid(), "Ana Cliente", "1001", "300", null, null, true);
        var viewModel = new CashierViewModel(
            new PrototypeProductLookupService(),
            new PreviewPaymentUseCase(),
            null,
            null,
            null,
            new SearchCustomersUseCase(new StaticCustomerLookupQueryService([customer])),
            getDeferredBalanceLimitUseCase: new GetDeferredBalanceLimitUseCase(settings));

        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);
        Assert.Equal(10m, viewModel.MaximumDeferredBalancePercent);

        settings.MaximumDeferredBalancePercent = 40m; // admin raises the cap in Ajustes, no restart
        await viewModel.SelectCustomerAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal(40m, viewModel.MaximumDeferredBalancePercent);
    }

    [Fact]
    public async Task ConfirmSaleAsync_WithTendersLeavingRemainder_PersistsOnePaymentPerLegAndDefersTheRest()
    {
        var product = new Product("Combo", Guid.NewGuid(), new Money(200_000m), new Money(400_000m), new StockQuantity(10m), new StockQuantity(1m));
        var sales = new InMemorySaleRepository();
        var receivables = new InMemoryReceivableRepository();
        var customerId = Guid.NewGuid();
        var viewModel = new CashierViewModel(
            new StaticProductLookupService(new ProductLookupResult(product.Id, product.Name, "7701", product.SalePrice.Amount, product.CurrentStock)),
            new PreviewPaymentUseCase(),
            new CreateSaleUseCase(
                new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork(), receivables,
                accumulatePoints: null, redeemPoints: null, inventory: null,
                settings: new SettingsRepositoryFake(50m), authorizer: CreateAuthorizer()))
        {
            SearchText = "combo",
            Quantity = 1m,
            CashierUserId = Guid.NewGuid(),
            CustomerId = customerId,
            MaximumDeferredBalancePercent = 50m
        };
        await viewModel.SearchAsync(TestContext.Current.CancellationToken);
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Transfer);
        viewModel.BankOrPlatform = "Bancolombia";
        viewModel.ReceivedAmount = 200_000m;
        viewModel.AddTenderCommand.Execute(null);

        viewModel.SelectedPaymentMethod = viewModel.PaymentMethods.Single(option => option.Method == PaymentMethod.Cash);
        viewModel.ReceivedAmount = 100_000m;
        viewModel.AddTenderCommand.Execute(null);

        Assert.True(viewModel.CanConfirm);

        await viewModel.ConfirmSaleAsync(TestContext.Current.CancellationToken);
        await viewModel.AcknowledgeSaleConfirmationAsync(TestContext.Current.CancellationToken);

        Assert.Contains("guardada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Sale sale = Assert.Single(sales.Sales);
        Assert.Equal(400_000m, sale.Total().Amount);
        Assert.Equal(2, sale.Payments.Count);
        Assert.Contains(sale.Payments, p => p.Method == PaymentMethod.Transfer && p.Amount.Amount == 200_000m && p.BankOrPlatform == "Bancolombia");
        Assert.Contains(sale.Payments, p => p.Method == PaymentMethod.Cash && p.Amount.Amount == 100_000m);
        Assert.Equal(100_000m, sale.DeferredBalance().Amount);
        Receivable receivable = Assert.Single(receivables.Stored);
        Assert.Equal(customerId, receivable.CustomerId);
        Assert.Equal(100_000m, receivable.OutstandingBalance.Amount);
    }

    // Loyalty backing store for a redemption use case that the empty-cart guard never reaches.
    private sealed class UnusedLoyaltyRepository : ILoyaltyRepository
    {
        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyAccount?>(null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyRule?>(null);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
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

    private sealed class InMemoryReceivableRepository : IReceivableRepository
    {
        public List<Receivable> Stored { get; } = [];

        public Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default)
        {
            Stored.Add(receivable);
            return Task.CompletedTask;
        }

        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored.FirstOrDefault(r => r.Id == id));

        public Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BlockingSearchProductLookupService : IProductLookupService
    {
        private readonly ProductLookupResult _product;
        private readonly TaskCompletionSource<IReadOnlyList<ProductLookupResult>> _searchBlocker;
        private readonly TaskCompletionSource<bool> _searchStarted;

        public BlockingSearchProductLookupService(
            ProductLookupResult product,
            TaskCompletionSource<IReadOnlyList<ProductLookupResult>> searchBlocker,
            TaskCompletionSource<bool> searchStarted)
        {
            _product = product;
            _searchBlocker = searchBlocker;
            _searchStarted = searchStarted;
        }

        public Task<IReadOnlyList<ProductLookupResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            _searchStarted.TrySetResult(true);
            return _searchBlocker.Task;
        }

        public Task<ProductLookupResult?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLookupResult?>(string.Equals(_product.Barcode, barcode.Trim(), StringComparison.Ordinal) ? _product : null);
    }

    private static CriticalCommandAuthorizer CreateAuthorizer()
    {
        var user = new User("test-cashier", "hash", RoleConstants.AdministratorRoleId);
        return new CriticalCommandAuthorizer(new TestSession(user.Id), new TestUsers(user), new TestRoles());
    }

    [Fact]
    public async Task LoadCustomerQuoteAsync_WhenQuoteAlreadyLoaded_BlocksSecondQuote()
    {
        var customerId = Guid.NewGuid();
        var item1 = new CustomerPayableQuoteItem(Guid.NewGuid(), "Prod A", 1m, 100m, 0m, 100m);
        var item2 = new CustomerPayableQuoteItem(Guid.NewGuid(), "Prod B", 1m, 200m, 0m, 200m);
        var quote1 = new CustomerPayableQuote(Guid.NewGuid(), "COT-001", "Cliente Test", 100m, DateTime.UtcNow.AddDays(1), [item1]);
        var quote2 = new CustomerPayableQuote(Guid.NewGuid(), "COT-002", "Cliente Test", 200m, DateTime.UtcNow.AddDays(1), [item2]);

        var viewModel = new CashierViewModel(new PrototypeProductLookupService())
        {
            SelectedCustomer = new CustomerSearchResult(customerId, "Cliente Test", "123", "300", null, null, true)
        };

        await viewModel.LoadCustomerQuoteAsync(quote1, TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsQuoteLoaded);
        Assert.Equal("COT-001", viewModel.LoadedQuoteNumber);

        await viewModel.LoadCustomerQuoteAsync(quote2, TestContext.Current.CancellationToken);
        Assert.Equal("COT-001", viewModel.LoadedQuoteNumber);
        Assert.Contains("Solo se permite cargar una cotización a la vez", viewModel.Message);
    }

    private sealed class TestSession(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "test-cashier";
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
        private static readonly Role Administrator = Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName);
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
}
