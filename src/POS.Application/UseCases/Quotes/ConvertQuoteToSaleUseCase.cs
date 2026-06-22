using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class ConvertQuoteToSaleUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly CreateSaleUseCase _createSale;
    private readonly IUnitOfWork _unitOfWork;

    public ConvertQuoteToSaleUseCase(IQuoteRepository quotes, CreateSaleUseCase createSale, IUnitOfWork unitOfWork)
    {
        _quotes = quotes;
        _createSale = createSale;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Legacy overload — uses explicit payment lines from request.</summary>
    public Task<CreateSaleResult> ExecuteAsync(ConvertQuoteToSaleRequest request, CancellationToken ct = default)
        => ExecuteInternalAsync(request, settlementPlan: null, ct);

    /// <summary>Settlement-plan overload — deferred balance supported.</summary>
    public Task<CreateSaleResult> ExecuteAsync(ConvertQuoteToSaleRequest request, SaleSettlementPlan settlementPlan, CancellationToken ct = default)
        => ExecuteInternalAsync(request, settlementPlan, ct);

    private async Task<CreateSaleResult> ExecuteInternalAsync(
        ConvertQuoteToSaleRequest request,
        SaleSettlementPlan? settlementPlan,
        CancellationToken ct)
    {
        var quote = await _quotes.GetByIdAsync(request.QuoteId, ct)
            ?? throw new InvalidOperationException($"Cotización {request.QuoteId} no encontrada.");

        if (quote.ExpiresAt < DateTime.UtcNow && quote.Status != QuoteStatus.Expired)
        {
            quote.Expire();
        }

        if (quote.Status == QuoteStatus.Converted)
        {
            throw new InvalidOperationException($"La cotización '{quote.QuoteNumber}' ya fue convertida a venta.");
        }

        if (quote.Status == QuoteStatus.Expired)
        {
            throw new InvalidOperationException($"La cotización '{quote.QuoteNumber}' está vencida y no puede convertirse.");
        }

        var saleItems = quote.Items.Select(i => new CreateSaleItemRequest(
            i.ProductId,
            i.Quantity.Value,
            i.Discount)).ToList();

        CreateSaleRequest saleRequest = settlementPlan is not null
            ? new CreateSaleRequest(request.CashierUserId, quote.CustomerId, saleItems, settlementPlan)
            : new CreateSaleRequest(request.CashierUserId, quote.CustomerId, saleItems, request.Payments);

        var saleResult = await _createSale.ExecuteAsync(saleRequest, ct);

        quote.Convert(saleResult.SaleId);

        await _quotes.UpdateAsync(quote, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return saleResult;
    }
}
