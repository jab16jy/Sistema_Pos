using POS.Application.DTOs.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Purchases;

public sealed class CreateSupplierQuotationUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IProductSupplierRepository _catalog;
    private readonly ISupplierQuotationRepository _quotations;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierQuotationUseCase(ISupplierRepository suppliers, IProductRepository products, IProductSupplierRepository catalog, ISupplierQuotationRepository quotations, IUnitOfWork unitOfWork)
        => (_suppliers, _products, _catalog, _quotations, _unitOfWork) = (suppliers, products, catalog, quotations, unitOfWork);

    public async Task<SupplierQuotationResult> ExecuteAsync(CreateSupplierQuotationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0) throw new ArgumentException("Supplier quotation must contain items.", nameof(request));
        Supplier supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier was not found.");
        if (!supplier.IsActive) throw new InvalidOperationException("Supplier is inactive.");

        var quotation = new SupplierQuotation(request.SupplierId, request.EffectiveDate, request.PaymentTerms, request.Observations);
        foreach (CreateSupplierQuotationItemRequest requestedItem in request.Items)
        {
            ProductSupplier catalog = await _catalog.GetAsync(request.SupplierId, requestedItem.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The selected product is not in the supplier catalog.");
            if (!catalog.IsActive) throw new InvalidOperationException("The selected supplier catalog product is inactive.");
            Product product = await _products.GetByIdAsync(requestedItem.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Product was not found.");
            quotation.AddItem(new SupplierQuotationItem(product.Id, product.Name, catalog.SupplierSku ?? product.SourceCode, catalog.PurchaseUnitName, catalog.UnitsPerPurchaseUnit, new Quantity(requestedItem.Quantity), new Money(catalog.PurchaseUnitCost)));
        }

        await _quotations.AddAsync(quotation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(quotation);
    }

    internal static SupplierQuotationResult ToResult(SupplierQuotation quotation) => new(
        quotation.Id, quotation.SupplierId, quotation.EffectiveDate, quotation.PaymentTerms, quotation.Observations, quotation.Status.ToString(),
        quotation.Items.Select(i => new PurchaseDraftItem(i.ProductId, i.ProductName, i.ProductReference, i.PurchaseUnitName, i.UnitsPerPurchaseUnit, i.PurchaseUnitCost.Amount, i.Quantity)).ToList());
}

public sealed class AcceptSupplierQuotationUseCase
{
    private readonly ISupplierQuotationRepository _quotations;
    private readonly IUnitOfWork _unitOfWork;
    public AcceptSupplierQuotationUseCase(ISupplierQuotationRepository quotations, IUnitOfWork unitOfWork) => (_quotations, _unitOfWork) = (quotations, unitOfWork);

    public async Task<SupplierQuotationResult> ExecuteAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        SupplierQuotation quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier quotation was not found.");
        quotation.Accept();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreateSupplierQuotationUseCase.ToResult(quotation);
    }
}

public sealed class ConvertSupplierQuotationToPurchaseDraftUseCase
{
    private readonly ISupplierQuotationRepository _quotations;
    public ConvertSupplierQuotationToPurchaseDraftUseCase(ISupplierQuotationRepository quotations) => _quotations = quotations;

    public async Task<PurchaseDraftFromQuotationResult> ExecuteAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        SupplierQuotation quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier quotation was not found.");
        if (quotation.Status != Domain.Enums.SupplierQuotationStatus.Accepted)
            throw new InvalidOperationException("Only accepted supplier quotations can preload a purchase.");

        return new PurchaseDraftFromQuotationResult(
            quotation.Id, quotation.SupplierId, quotation.EffectiveDate, quotation.PaymentTerms, quotation.Observations,
            quotation.Items.Select(i => new PurchaseDraftItem(i.ProductId, i.ProductName, i.ProductReference, i.PurchaseUnitName, i.UnitsPerPurchaseUnit, i.PurchaseUnitCost.Amount, i.Quantity)).ToList());
    }
}
