using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class GetLoyaltyTiersUseCase(ILoyaltyRepository loyalty)
{
    public async Task<IReadOnlyList<LoyaltyTierResult>> ExecuteAsync(CancellationToken cancellationToken = default)
        => (await loyalty.GetTiersAsync(cancellationToken).ConfigureAwait(false))
            .Select(LoyaltyTierMapper.ToResult)
            .ToList();
}

public sealed class CreateLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyTierResult> ExecuteAsync(CreateLoyaltyTierRequest request, CancellationToken cancellationToken = default)
    {
        var tier = new LoyaltyTier(request.Name, request.MinPoints, request.DiscountPercent, request.PointsMultiplier, request.Color);
        await loyalty.AddTierAsync(tier, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyTierMapper.ToResult(tier);
    }
}

public sealed class UpdateLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyTierResult> ExecuteAsync(UpdateLoyaltyTierRequest request, CancellationToken cancellationToken = default)
    {
        LoyaltyTier tier = await loyalty.GetTierByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El tier seleccionado ya no existe.");
        tier.Update(request.Name, request.MinPoints, request.DiscountPercent, request.PointsMultiplier, request.Color);
        await loyalty.UpdateTierAsync(tier, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyTierMapper.ToResult(tier);
    }
}

public sealed class DeleteLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        if (tierId == Guid.Empty)
        {
            throw new ArgumentException("A tier must be selected.", nameof(tierId));
        }

        await loyalty.DeleteTierAsync(tierId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class LoyaltyTierMapper
{
    public static LoyaltyTierResult ToResult(LoyaltyTier tier)
        => new(tier.Id, tier.Name, tier.MinPoints, tier.DiscountPercent, tier.PointsMultiplier, tier.Color);
}
