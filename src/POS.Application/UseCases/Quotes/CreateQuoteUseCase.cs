using POS.Application.DTOs.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class CreateQuoteUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsRepository? _settings;

    public CreateQuoteUseCase(IQuoteRepository quotes, IProductRepository products, IUnitOfWork unitOfWork, ISettingsRepository? settings = null)
    {
        _quotes = quotes;
        _products = products;
        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    public async Task<QuoteResult> ExecuteAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
        decimal maximumDiscount = await CashierDiscountPolicy.GetMaximumAsync(_settings, ct).ConfigureAwait(false);
        CashierDiscountPolicy.Validate(request.Items, maximumDiscount);

        Quote quote = null!;

        // Reservation, quote staging, and save must commit atomically: the sequence UPSERT and the
        // quote insert live in the same transaction so a failed save never burns a number silently
        // beyond what the sequence row already reflects, and no two callers can observe the same number.
        await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            var quoteNumber = await _quotes.GetNextQuoteNumberAsync(transactionCt);

            quote = new Quote(
                quoteNumber,
                request.CustomerName,
                request.ValidityDays,
                request.CustomerId,
                request.Phone,
                request.Email,
                request.Observations);

            // Intentionally no ILoyaltyRepository/tier lookup here: a quote is a commercial
            // proposal whose customer tier can change before it's converted, so only the
            // cashier's manual per-line Discount is frozen in. The tier discount is resolved
            // live at checkout (see CashierViewModel._currentTierDiscountPercent) — and
            // ConvertQuoteToSaleUseCase carries this frozen Discount through without
            // re-resolving the tier either, for the same reason.
            foreach (QuoteItem item in await QuoteLineBuilder.BuildAllAsync(request.Items, _products, transactionCt).ConfigureAwait(false))
            {
                quote.AddItem(item);
            }

            await _quotes.AddAsync(quote, transactionCt);
            await _unitOfWork.SaveChangesAsync(transactionCt);
        }, ct).ConfigureAwait(false);

        return new QuoteResult(
            quote.Id,
            quote.QuoteNumber,
            quote.Total,
            quote.ExpiresAt.ToString("yyyy-MM-dd"),
            quote.Status);
    }
}
