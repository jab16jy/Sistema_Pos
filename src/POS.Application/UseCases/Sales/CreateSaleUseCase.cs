using POS.Application.Common;
using POS.Application.DTOs.Loyalty;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class CreateSaleUseCase
{
    private readonly IProductRepository _products;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReceivableRepository? _receivables;
    private readonly AccumulateLoyaltyPointsUseCase? _accumulatePoints;
    private readonly RedeemLoyaltyPointsUseCase? _redeemPoints;
    private readonly IInventoryRepository? _inventory;
    private readonly ISettingsRepository? _settings;

    /// <summary>Legacy constructor — no receivable support. Used by tests that don't need deferred balance.</summary>
    public CreateSaleUseCase(IProductRepository products, ISaleRepository sales, IUnitOfWork unitOfWork)
        : this(products, sales, unitOfWork, null, null, null, null)
    {
    }

    public CreateSaleUseCase(
        IProductRepository products,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IReceivableRepository? receivables)
        : this(products, sales, unitOfWork, receivables, null, null, null)
    {
    }

    public CreateSaleUseCase(
        IProductRepository products,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IReceivableRepository? receivables,
        AccumulateLoyaltyPointsUseCase? accumulatePoints)
        : this(products, sales, unitOfWork, receivables, accumulatePoints, null, null)
    {
    }

    public CreateSaleUseCase(
        IProductRepository products,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IReceivableRepository? receivables,
        AccumulateLoyaltyPointsUseCase? accumulatePoints,
        RedeemLoyaltyPointsUseCase? redeemPoints,
        IInventoryRepository? inventory = null,
        ISettingsRepository? settings = null)
    {
        _products = products;
        _sales = sales;
        _unitOfWork = unitOfWork;
        _receivables = receivables;
        _accumulatePoints = accumulatePoints;
        _redeemPoints = redeemPoints;
        _inventory = inventory;
        _settings = settings;
    }

    public async Task<CreateSaleResult> ExecuteAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one product before completing the sale.");
        }

        if (!request.IsQuoteConversion)
        {
            decimal maximumDiscount = await CashierDiscountPolicy
                .GetMaximumAsync(_settings, cancellationToken)
                .ConfigureAwait(false);
            CashierDiscountPolicy.Validate(request.Items, maximumDiscount);
        }

        // Validate payment platforms before building the sale
        if (request.Payments is not null)
        {
            foreach (var payment in request.Payments)
            {
                PaymentPlatforms.Validate(payment.Method, payment.BankOrPlatform);
            }
        }

        var sale = new Sale(request.CashierUserId, request.CustomerId);
        var productsById = new Dictionary<Guid, Product>();

        foreach (var item in request.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");
            productsById[product.Id] = product;

            if (item.UnitPriceOverride.HasValue || item.UnitsPerPresentationOverride.HasValue)
            {
                if (!item.UnitPriceOverride.HasValue || !item.UnitsPerPresentationOverride.HasValue)
                {
                    throw new ArgumentException("A sale line snapshot requires both price and units per presentation.", nameof(request));
                }

                sale.AddItem(
                    product,
                    new Quantity(item.Quantity),
                    item.Presentation,
                    new Money(item.Discount),
                    new Money(item.UnitPriceOverride.Value),
                    item.UnitsPerPresentationOverride.Value);
            }
            else
            {
                sale.AddItem(product, new Quantity(item.Quantity), item.Presentation, new Money(item.Discount));
            }
        }

        // Loyalty redemption (tender): validate and cap the redemption, add it as a
        // non-money LoyaltyPoints payment covering part of the total, and stage the point
        // deduction (with this SaleId) so it commits atomically with the sale below.
        // If the customer cannot actually redeem, this throws and the whole sale is aborted
        // before anything is persisted — points are never spent without a completed sale.
        if (request.RedeemLoyaltyPoints > 0 && request.CustomerId.HasValue && _redeemPoints is not null)
        {
            // Redeeming points against a deferred (credit) sale is not supported: the tender
            // and the settlement plan's collected/deferred split would double-count coverage.
            // Fail loud here so a non-UI caller can't reach the illegal combination silently.
            if (request.SettlementPlan is not null)
            {
                throw new InvalidOperationException(
                    "Loyalty point redemption cannot be combined with a deferred settlement plan.");
            }

            RedeemPointsResult redemption = await _redeemPoints.RecordForSaleAsync(
                new RedeemPointsRequest(request.CustomerId.Value, request.RedeemLoyaltyPoints, sale.Total().Amount),
                sale.Id,
                cancellationToken).ConfigureAwait(false);

            if (redemption.DiscountAmount > 0m)
            {
                sale.AddPayment(new Payment(Domain.Enums.PaymentMethod.LoyaltyPoints, new Money(redemption.DiscountAmount)));
            }
        }

        if (request.SettlementPlan is not null)
        {
            var plan = request.SettlementPlan;

            if (plan.DeferredBalanceAmount > 0m && request.CustomerId is null)
            {
                throw new InvalidOperationException(
                    "A customer must be identified to create a deferred balance.");
            }

            if (plan.CollectedMethod is { } collectedMethod)
            {
                PaymentPlatforms.Validate(collectedMethod, plan.CollectedBankOrPlatform);
            }
            else if (!string.IsNullOrWhiteSpace(plan.CollectedBankOrPlatform))
            {
                throw new InvalidOperationException("A payment platform requires a collected payment method.");
            }

            sale.SetSettlementPlan(plan);

            if (plan.CollectedAmount > 0m && plan.CollectedMethod is not null)
            {
                sale.AddPayment(new Payment(
                    plan.CollectedMethod.Value,
                    new Money(plan.CollectedAmount),
                    plan.CollectedMethod == Domain.Enums.PaymentMethod.Cash ? new Money(plan.CollectedAmount) : null,
                    plan.CollectedBankOrPlatform));
            }
        }
        else
        {
            foreach (var payment in request.Payments!)
            {
                sale.AddPayment(new Payment(
                    payment.Method,
                    new Money(payment.Amount),
                    payment.ReceivedAmount is null ? null : new Money(payment.ReceivedAmount.Value),
                    payment.BankOrPlatform));
            }
        }

        sale.Complete();

        await _sales.AddAsync(sale, cancellationToken);

        // The sale aggregate already decremented stock as each line was added; here we record the
        // matching Exit movement per line so every stock change stays traceable (saldo ⇄ historial).
        // ponytail: null ledger only in unit fixtures; production DI injects it and always audits sales.
        if (_inventory is not null)
        {
            foreach (SaleItem line in sale.Items)
            {
                if (productsById.TryGetValue(line.ProductId, out Product? product) && product.TracksInventory)
                {
                    var baseUnits = new Quantity(line.Quantity * line.UnitsPerPresentation);
                    await _inventory.AddMovementAsync(
                        new InventoryMovement(line.ProductId, InventoryMovementType.Exit, baseUnits, "Venta", request.CashierUserId, sale.Id),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        Receivable? receivable = null;
        if (sale.DeferredBalance().Amount > 0m && request.CustomerId.HasValue)
        {
            receivable = new Receivable(request.CustomerId.Value, sale.Id, sale.DeferredBalance());
            if (_receivables is not null)
            {
                await _receivables.AddAsync(receivable, cancellationToken);
            }
        }

        if (_accumulatePoints is not null && request.CustomerId.HasValue)
        {
            await _accumulatePoints.RecordForSaleAsync(
                new EarnPointsRequest(request.CustomerId.Value, sale.Id, sale.EffectiveEarnableTotal().Amount),
                cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSaleResult(
            sale.Id,
            sale.InvoiceNumber,
            sale.Total().Amount,
            sale.CashInDrawer().Amount,
            sale.DeferredBalance().Amount);
    }
}
