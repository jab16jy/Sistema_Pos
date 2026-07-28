using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class CancelQuoteUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IUnitOfWork _unitOfWork;

    public CancelQuoteUseCase(IQuoteRepository quotes, IUnitOfWork unitOfWork)
    {
        _quotes = quotes;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _quotes.GetByIdAsync(quoteId, ct).ConfigureAwait(true)
            ?? throw new InvalidOperationException($"Cotización con ID {quoteId} no encontrada.");

        quote.Cancel();
        await _quotes.UpdateAsync(quote, ct).ConfigureAwait(true);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(true);
    }
}
