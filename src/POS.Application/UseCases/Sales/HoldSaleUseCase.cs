using POS.Application.DTOs.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class HoldSaleUseCase
{
    private readonly IProductRepository _products;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository? _inventory;
    private readonly ISettingsRepository? _settings;

    public HoldSaleUseCase(
        IProductRepository products,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IInventoryRepository? inventory = null,
        ISettingsRepository? settings = null)
    {
        _products = products;
        _sales = sales;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
        _settings = settings;
    }

    public async Task<HeldSaleSummary> ExecuteAsync(
        HoldSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CashierUserId == Guid.Empty)
        {
            throw new ArgumentException("A cashier is required to hold a sale.", nameof(request));
        }

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one product before holding the sale.");
        }

        decimal maximumDiscount = await CashierDiscountPolicy
            .GetMaximumAsync(_settings, cancellationToken)
            .ConfigureAwait(false);
        CashierDiscountPolicy.Validate(request.Items, maximumDiscount);

        var sale = new Sale(request.CashierUserId, request.CustomerId);
        var productsById = new Dictionary<Guid, Product>();

        foreach (CreateSaleItemRequest item in request.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");
            productsById[product.Id] = product;

            if (item.UnitPriceOverride.HasValue || item.UnitsPerPresentationOverride.HasValue)
            {
                if (!item.UnitPriceOverride.HasValue || !item.UnitsPerPresentationOverride.HasValue)
                {
                    throw new ArgumentException("A sale line snapshot requires both price and units per presentation.", nameof(request));
                }

                sale.AddItem(product, new Quantity(item.Quantity), item.Presentation, new Money(item.Discount),
                    new Money(item.UnitPriceOverride.Value), item.UnitsPerPresentationOverride.Value);
            }
            else
            {
                sale.AddItem(product, new Quantity(item.Quantity), item.Presentation, new Money(item.Discount));
            }
        }

        sale.Hold();
        await _sales.AddAsync(sale, cancellationToken).ConfigureAwait(false);

        // Stock is reserved once, when the held aggregate is created. Resume/complete never calls AddItem.
        if (_inventory is not null)
        {
            foreach (SaleItem line in sale.Items)
            {
                if (productsById.TryGetValue(line.ProductId, out Product? product) && product.TracksInventory)
                {
                    await _inventory.AddMovementAsync(
                        new InventoryMovement(
                            line.ProductId,
                            InventoryMovementType.Exit,
                            new Quantity(line.Quantity * line.UnitsPerPresentation),
                            "Venta en espera",
                            request.CashierUserId,
                            sale.Id),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HeldSaleMapper.Map(sale);
    }
}
