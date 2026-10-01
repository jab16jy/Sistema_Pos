using POS.Application.DTOs.Quotes;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.Sales;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Quotes;

public sealed class UpdateQuoteUseCase
{
    private readonly IQuoteRepository _quotes;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentOperatorSession _session;
    private readonly ISettingsRepository? _settings;

    public UpdateQuoteUseCase(
        IQuoteRepository quotes,
        IProductRepository products,
        IUnitOfWork unitOfWork,
        ICurrentOperatorSession session,
        ISettingsRepository? settings = null)
    {
        _quotes = quotes;
        _products = products;
        _unitOfWork = unitOfWork;
        _session = session;
        _settings = settings;
    }

    public async Task<QuoteResult> ExecuteAsync(UpdateQuoteRequest request, CancellationToken ct = default)
    {
        // This use case writes UnitPrice, Quantity and Discount — the money path — so the permission
        // belongs here and not only on QuoteHistoryViewModel.EditCommand. The view-model gate is
        // correct UI behaviour (it hides an affordance the operator may not use), but an affordance is
        // not access control: any other caller reaching this constructor inherited an unauthenticated
        // money write.
        //
        // Surfaced exactly like the other money-path guards (CreateSaleUseCase,
        // ProcessReturnUseCase, AdjustStockUseCase): an UnauthorizedActionException naming the
        // permission, thrown rather than returned because QuoteResult has no error channel.
        // The view-model renders ex.Message, so the operator still sees why it was refused.
        if (PermissionAuthorization.EnsureHasPermission(
                _session, PermissionKeys.Quotes.Edit, "No tienes permiso para editar cotizaciones.") is not null)
        {
            throw new UnauthorizedActionException(PermissionKeys.Quotes.Edit);
        }

        // KNOWN GAP, deliberately not fixed here: CreateQuoteUseCase and CancelQuoteUseCase are also
        // ungated at the use-case level. That is a pre-existing app-wide pattern (see
        // GetAllUsersUseCase, GetAllRolesUseCase, which are gated — and CreateSaleUseCase, which is
        // not), not something this change introduced. Closing it app-wide is its own change; doing it
        // for quotes alone would leave the module internally inconsistent without making the system
        // any safer. Recorded rather than silently expanded into.
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