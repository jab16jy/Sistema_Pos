using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Loyalty;

/// <summary>
/// Calculates and records loyalty points earned for a completed sale.
/// If no active rule exists or calculated points equal zero, no transaction is created.
/// Must be called within the same unit of work as the sale save.
/// </summary>
public sealed class AccumulateLoyaltyPointsUseCase
{
    private readonly ILoyaltyRepository _loyalty;
    private readonly IUnitOfWork _unitOfWork;

    public AccumulateLoyaltyPointsUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
    {
        _loyalty = loyalty;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(EarnPointsRequest request, CancellationToken cancellationToken = default)
    {
        if (await RecordForSaleAsync(request, cancellationToken).ConfigureAwait(false))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records earned points tied to a sale WITHOUT saving — the caller's unit of work must
    /// commit it atomically together with the sale, so points are only awarded if the sale succeeds.
    /// </summary>
    public async Task<bool> RecordForSaleAsync(EarnPointsRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _loyalty.GetActiveRuleAsync(cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            // No active rule — skip accumulation silently
            return false;
        }

        var points = rule.PointsFor(new Money(request.SaleTotal));
        if (points == 0)
        {
            // Below threshold — no transaction
            return false;
        }

        var account = await _loyalty.GetAccountByCustomerIdAsync(request.CustomerId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            account = new LoyaltyAccount(request.CustomerId);
            await _loyalty.AddAccountAsync(account, cancellationToken).ConfigureAwait(false);
        }

        var transaction = new LoyaltyTransaction(
            account.Id,
            LoyaltyTransactionType.Earned,
            points,
            request.SaleId,
            $"Points earned from sale {request.SaleId}");

        await _loyalty.AddTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
