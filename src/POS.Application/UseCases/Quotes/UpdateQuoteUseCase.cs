using POS.Application.DTOs.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class UpdateQuoteUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsRepository? _settings;

    public UpdateQuoteUseCase(IQuoteRepository quotes, IProductRepository products, IUnitOfWork unitOfWork, ISettingsRepository? settings = null)
    {
        _quotes = quotes;
        _products = products;
        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    public async Task<QuoteResult> ExecuteAsync(UpdateQuoteRequest request, CancellationToken ct = default)
    {
        var quote = await _quotes.GetByIdAsync(request.QuoteId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cotización {request.QuoteId} no encontrada.");

        // Checked before the discount policy and the product lookups so a frozen quote is rejected
        // without touching anything. The aggregate repeats the same guard on Update/ReplaceItems;
        // this copy only exists to fail earlier and cheaper.
        if (!quote.CanEdit)
        {
            throw new QuoteNotEditableException(quote.QuoteNumber, quote.Status);
        }

        // Same ceiling as creation: a quote is a proposal a client may hold on to, so an edit must
        // not become a way around the discount limit the cashier is held to at the till.
        decimal maximumDiscount = await CashierDiscountPolicy.GetMaximumAsync(_settings, ct).ConfigureAwait(false);
        CashierDiscountPolicy.Validate(request.Items, maximumDiscount);

        // Header, lines, and the orphan cleanup the repository performs on the dropped lines commit
        // together: a half-written edit would leave the persisted lines disagreeing with the total
        // the operator was shown.
        await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            IReadOnlyList<QuoteItem> items = await QuoteLineBuilder.BuildAllAsync(request.Items, _products, transactionCt).ConfigureAwait(false);

            quote.Update(request.CustomerName, request.ValidityDays, request.Phone, request.Email, request.Observations);
            quote.ReplaceItems(items);

            await _quotes.UpdateAsync(quote, transactionCt).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(transactionCt).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        // Deliberately no sale, payment or inventory call anywhere in this path (I7): the quote is
        // not a commitment, so editing it must not move stock.
        return new QuoteResult(
            quote.Id,
            quote.QuoteNumber,
            quote.Total,
            quote.ExpiresAt.ToString("yyyy-MM-dd"),
            quote.Status);
    }
}