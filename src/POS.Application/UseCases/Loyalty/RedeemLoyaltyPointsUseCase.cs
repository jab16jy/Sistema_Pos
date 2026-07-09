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

    /// <summary>
    /// Validates and computes a redemption (points + COP discount) WITHOUT persisting anything.
    /// Used to preview the discount at checkout before the sale is confirmed.
    /// </summary>
    public async Task<RedeemPointsResult> PreviewAsync(RedeemPointsRequest request, CancellationToken cancellationToken = default)
    {
        var (_, points, discount) = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        return new RedeemPointsResult(points, discount);
    }

    /// <summary>
    /// Standalone redemption (no sale context): persists the redemption in its own unit of work.
    /// Used by the loyalty module for manual redemptions.
    /// </summary>
    public async Task<RedeemPointsResult> ExecuteAsync(RedeemPointsRequest request, CancellationToken cancellationToken = default)
    {
        var (account, points, discount) = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);

        await AddRedemptionTransactionAsync(account.Id, points, discount, saleId: null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RedeemPointsResult(points, discount);
    }

    /// <summary>
    /// Records a redemption tied to a sale WITHOUT saving — the caller's unit of work must
    /// commit it atomically together with the sale, so points are only spent if the sale succeeds.
    /// </summary>
    public async Task<RedeemPointsResult> RecordForSaleAsync(RedeemPointsRequest request, Guid saleId, CancellationToken cancellationToken = default)
    {
        var (account, points, discount) = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);

        await AddRedemptionTransactionAsync(account.Id, points, discount, saleId, cancellationToken).ConfigureAwait(false);

        return new RedeemPointsResult(points, discount);
    }

    /// <summary>
    /// Validates the request against the active rule and account balance, and computes the
    /// actual (possibly capped) points and COP discount. Performs no persistence.
    /// </summary>
    private async Task<(LoyaltyAccount Account, int Points, decimal Discount)> ResolveAsync(
        RedeemPointsRequest request, CancellationToken cancellationToken)
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

        // Guard: cap the discount to the sale total to prevent negative totals.
        // When the computed discount exceeds the sale total, reduce the redeemed points
        // so the discount fits within the sale total.
        // SaleTotal == 0/null means no sale context (standalone redemption) — skip the cap.
        var actualPointsRedeemed = request.PointsToRedeem;
        var actualDiscount = rule.DiscountFor(actualPointsRedeemed).Amount;

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

        // A zero (or negative) redemption is not a valid redemption: it would produce a
        // non-negative "Redeemed" transaction (rejected by the domain) and a $0 tender.
        // Reachable when the sale total is below one point's value and the rule allows a
        // zero minimum. Fail with a clear message instead of a confusing domain error.
        if (actualPointsRedeemed <= 0)
        {
            throw new InvalidOperationException("The sale total is too low to redeem any points.");
        }

        return (account, actualPointsRedeemed, actualDiscount);
    }

    private Task AddRedemptionTransactionAsync(
        Guid accountId, int points, decimal discount, Guid? saleId, CancellationToken cancellationToken)
    {
        var transaction = new LoyaltyTransaction(
            accountId,
            LoyaltyTransactionType.Redeemed,
            -points,
            saleId,
            $"Points redeemed: {points} pts = {discount} COP discount");

        return _loyalty.AddTransactionAsync(transaction, cancellationToken);
    }
}
