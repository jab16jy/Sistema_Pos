using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

/// <summary>
/// Spends a customer's points on a catalogue benefit and opens a pending claim for it.
/// </summary>
/// <remarks>
/// Deliberately independent of <see cref="RedeemLoyaltyPointsUseCase"/>: the benefit's own point
/// cost is the rule, so the program's MinimumRedeemPoints, RedemptionValuePerPoint and the
/// per-tier AllowsRedemption gate do not apply here. A benefit is bought with points, not converted
/// into a discount.
/// </remarks>
public sealed class ClaimLoyaltyBenefitUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyBenefitClaimResult> ExecuteAsync(ClaimLoyaltyBenefitRequest request, CancellationToken cancellationToken = default)
    {
        LoyaltyBenefit benefit = await loyalty.GetBenefitByIdAsync(request.BenefitId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El beneficio seleccionado ya no existe.");

        if (!benefit.IsActive)
        {
            throw new InvalidOperationException($"El beneficio \"{benefit.Name}\" ya no está disponible.");
        }

        LoyaltyAccount account = await loyalty.GetAccountByCustomerIdAsync(request.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El cliente no tiene una cuenta de puntos.");

        if (!account.IsActive)
        {
            throw new InvalidOperationException("La cuenta de puntos del cliente está inactiva.");
        }

        int balance = await loyalty.GetBalanceAsync(account.Id, cancellationToken).ConfigureAwait(false);
        if (balance < benefit.PointsCost)
        {
            throw new InvalidOperationException(
                $"Puntos insuficientes. Disponibles: {balance}, requeridos: {benefit.PointsCost}.");
        }

        // The spend and the claim commit in one unit of work: points must never leave the account
        // without a claim to show for them, and a claim must never exist without its spend.
        var spend = new LoyaltyTransaction(
            account.Id,
            LoyaltyTransactionType.BenefitRedeemed,
            -benefit.PointsCost,
            saleId: null,
            $"Beneficio canjeado: {benefit.Name} ({benefit.PointsCost} pts)");
        await loyalty.AddTransactionAsync(spend, cancellationToken).ConfigureAwait(false);

        var claim = new LoyaltyBenefitClaim(
            benefit.Id,
            account.Id,
            benefit.Name,
            benefit.PointsCost,
            spend.Id,
            request.ClaimedByUserId);
        await loyalty.AddClaimAsync(claim, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyBenefitMapper.ToResult(claim);
    }
}
