using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class GetCustomerBenefitClaimsUseCase(ILoyaltyRepository loyalty)
{
    public async Task<IReadOnlyList<LoyaltyBenefitClaimResult>> ExecuteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        LoyaltyAccount? account = await loyalty.GetAccountByCustomerIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            // No account means no points were ever earned, so there is nothing to have claimed.
            return [];
        }

        return (await loyalty.GetClaimsByAccountAsync(account.Id, cancellationToken).ConfigureAwait(false))
            .Select(LoyaltyBenefitMapper.ToResult)
            .ToList();
    }
}

public sealed class DeliverLoyaltyBenefitClaimUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyBenefitClaimResult> ExecuteAsync(Guid claimId, Guid? deliveredByUserId = null, CancellationToken cancellationToken = default)
    {
        LoyaltyBenefitClaim claim = await loyalty.GetClaimByIdAsync(claimId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El canje seleccionado ya no existe.");

        claim.MarkDelivered(deliveredByUserId);
        await loyalty.UpdateClaimAsync(claim, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyBenefitMapper.ToResult(claim);
    }
}

/// <summary>
/// Cancels a claim and gives the points back through a compensation movement, so the original spend
/// stays in the ledger as evidence instead of being edited away.
/// </summary>
public sealed class CancelLoyaltyBenefitClaimUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyBenefitClaimResult> ExecuteAsync(
        Guid claimId,
        string? reason = null,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default)
    {
        LoyaltyBenefitClaim claim = await loyalty.GetClaimByIdAsync(claimId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El canje seleccionado ya no existe.");

        LoyaltyTransaction spend = await loyalty.GetTransactionByIdAsync(claim.RedeemTransactionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No se encontró el movimiento de puntos del canje; no se puede devolver el saldo.");

        // Cancel() runs first because it rejects an already-cancelled claim. That check is what stops
        // the same points from being restored twice — the ledger's unique compensation index only
        // covers sale returns, which carry a SaleReturnId this path never has.
        claim.Cancel(reason, cancelledByUserId);

        string trimmedReason = reason?.Trim() ?? string.Empty;
        LoyaltyTransaction restoration = LoyaltyTransaction.CreateCompensation(
            spend,
            trimmedReason.Length == 0
                ? $"Canje anulado: {claim.BenefitName}"
                : $"Canje anulado: {claim.BenefitName} — {trimmedReason}");

        await loyalty.AddTransactionAsync(restoration, cancellationToken).ConfigureAwait(false);
        await loyalty.UpdateClaimAsync(claim, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyBenefitMapper.ToResult(claim);
    }
}
