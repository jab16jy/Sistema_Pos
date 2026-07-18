using POS.Application.DTOs.Quotes;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Quotes;

public sealed class CreateQuoteUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CreateQuoteUseCase(IQuoteRepository quotes, IProductRepository products, IUnitOfWork unitOfWork)
    {
        _quotes = quotes;
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<QuoteResult> ExecuteAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
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

            foreach (var item in request.Items)
            {
                var product = await _products.GetByIdAsync(item.ProductId, transactionCt)
                    ?? throw new InvalidOperationException($"Producto {item.ProductId} no encontrado.");

                int unitsPerPresentation = item.Presentation == Domain.Enums.ProductPresentation.Box
                    ? product.UnitsPerBox ?? throw new InvalidOperationException($"Producto '{product.Name}' no está configurado para venta por caja.")
                    : 1;

                quote.AddItem(new QuoteItem(
                    product.Id,
                    product.Name,
                    product.PriceFor(item.Presentation),
                    new Quantity(item.Quantity),
                    item.Discount,
                    item.Presentation,
                    unitsPerPresentation));
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
