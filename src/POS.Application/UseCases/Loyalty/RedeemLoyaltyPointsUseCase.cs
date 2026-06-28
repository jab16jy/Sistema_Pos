using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

/// <summary>
/// Redeems loyalty points for a customer and returns the resulting COP discount amount.
/// Validates that the customer has sufficient balance, meets the minimum redemption threshold,
/// and caps the discount so it never exceeds the sale total (prevents negative totals).
/// </summary>
public sealed class RedeemLoyaltyPointsUseCase
{
    private readonly ILoyaltyRepository _loyalty;
    private readonly IUnitOfWork _unitOfWork;

    public RedeemLoyaltyPointsUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
    {
        _loyalty = loyalty;
        _unitOfWork = unitOfWork;
    }

    public async Task<RedeemPointsResult> ExecuteAsync(RedeemPointsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PointsToRedeem <= 0)
        {
            throw new ArgumentException("Points to redeem must be greater than zero.", nameof(request));
        }

        if (request.SaleTotal < 0m)
        {
            throw new ArgumentException("Sale total cannot be negative.", nameof(request));
        }

        var rule = await _loyalty.GetActiveRuleAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("No active loyalty rule found. Redemption is unavailable.");

        if (request.PointsToRedeem < rule.MinimumRedeemPoints)
        {
            throw new InvalidOperationException(
                $"Minimum redemption is {rule.MinimumRedeemPoints} points. Requested: {request.PointsToRedeem}.");
        }

        var account = await _loyalty.GetAccountByCustomerIdAsync(request.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Customer does not have a loyalty account.");

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Customer loyalty account is inactive.");
        }

        var balance = await _loyalty.GetBalanceAsync(account.Id, cancellationToken).ConfigureAwait(false);
        if (balance < request.PointsToRedeem)
        {
            throw new InvalidOperationException(
                $"Insufficient loyalty balance. Available: {balance}, requested: {request.PointsToRedeem}.");
        }

        var fullDiscount = rule.DiscountFor(request.PointsToRedeem);

        // Guard: cap the discount to the sale total to prevent negative totals.
        // When the computed discount exceeds the sale total, reduce the redeemed points
        // so the discount fits within the sale total.
        // SaleTotal == 0 means no sale context (standalone redemption) — skip the cap.
        var actualPointsRedeemed = request.PointsToRedeem;
        var actualDiscount = fullDiscount.Amount;

        if (request.SaleTotal > 0m && actualDiscount > request.SaleTotal)
        {
            actualPointsRedeemed = (int)Math.Floor(request.SaleTotal.Value / rule.RedemptionValuePerPoint);

            if (actualPointsRedeemed < rule.MinimumRedeemPoints)
            {
                throw new InvalidOperationException(
                    $"Redemption discount ({actualDiscount}) exceeds sale total ({request.SaleTotal}), " +
                    $"and the maximum redeemable points ({actualPointsRedeemed}) is below the minimum ({rule.MinimumRedeemPoints}).");
            }

            actualDiscount = rule.DiscountFor(actualPointsRedeemed).Amount;
        }

        var transaction = new LoyaltyTransaction(
            account.Id,
            LoyaltyTransactionType.Redeemed,
            -actualPointsRedeemed,
            null,
            $"Points redeemed: {actualPointsRedeemed} pts = {actualDiscount} COP discount");

        await _loyalty.AddTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RedeemPointsResult(actualPointsRedeemed, actualDiscount);
    }
}
