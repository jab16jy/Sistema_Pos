using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Loyalty;

/// <summary>Stages immutable loyalty corrections in the caller's unit of work.</summary>
public sealed class CompensateSaleLoyaltyUseCase(ILoyaltyRepository loyalty)
{
    public Task CompensateFullAsync(Sale sale, string reason, CancellationToken cancellationToken = default)
        => CompensateAsync(sale, null, reason, cancellationToken);

    public Task CompensatePartialAsync(Sale sale, IReadOnlyList<PartialReturnLine> returnedLines, string reason, CancellationToken cancellationToken = default)
        => CompensateAsync(sale, returnedLines, reason, cancellationToken);

    public async Task CompensateReturnAsync(
        Sale sale,
        IReadOnlyList<SaleReturn> priorReturns,
        SaleReturn currentReturn,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(priorReturns);
        ArgumentNullException.ThrowIfNull(currentReturn);

        Dictionary<Guid, int> returnedQuantities = CalculateReturnedQuantities(sale, priorReturns.Append(currentReturn));
        IReadOnlyList<LoyaltyTransaction> rows = await loyalty.GetTransactionsBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false);
        bool isFull = IsFullReturn(sale, returnedQuantities);
        decimal returnedNet = CalculateReturnedNet(sale, returnedQuantities);
        decimal originalTotal = sale.Total().Amount;
        decimal originalEarnable = sale.EffectiveEarnableTotal().Amount;
        decimal allocatedTender = originalTotal <= 0m
            ? 0m
            : Math.Min(RoundMoney(sale.LoyaltyPointsTenderTotal().Amount * returnedNet / originalTotal), sale.LoyaltyPointsTenderTotal().Amount);
        decimal returnedEarnable = Math.Max(0m, returnedNet - allocatedTender);

        foreach (LoyaltyTransaction source in rows.Where(row => row.Type is LoyaltyTransactionType.Earned or LoyaltyTransactionType.Redeemed))
        {
            int target = isFull
                ? Math.Abs(source.Points)
                : AllocatePoints(source, returnedNet, returnedEarnable, originalTotal, originalEarnable);
            int alreadyCompensated = rows
                .Where(row => row.CompensatesTransactionId == source.Id)
                .Sum(row => Math.Abs(row.Points));
            int delta = Math.Clamp(target - alreadyCompensated, 0, Math.Abs(source.Points));
            if (delta > 0)
            {
                await loyalty.AddTransactionAsync(
                    LoyaltyTransaction.CreateCompensation(source, reason, delta, currentReturn.Id), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task CompensateAsync(Sale sale, IReadOnlyList<PartialReturnLine>? returnedLines, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (returnedLines is not null)
            ValidatePartialReturnLines(sale, returnedLines);

        IReadOnlyList<LoyaltyTransaction> rows = await loyalty.GetTransactionsBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> compensatedSourceIds = rows
            .Where(row => row.CompensatesTransactionId is not null)
            .Select(row => row.CompensatesTransactionId!.Value)
            .ToHashSet();
        bool isFull = returnedLines is null || IsFullReturn(sale, returnedLines);

        decimal returnedNet = 0m;
        decimal returnedEarnable = 0m;
        decimal originalTotal = sale.Total().Amount;
        decimal originalEarnable = sale.EffectiveEarnableTotal().Amount;
        if (!isFull)
        {
            returnedNet = CalculateReturnedNet(sale, returnedLines!);
            decimal allocatedTender = originalTotal <= 0m
                ? 0m
                : RoundMoney(sale.LoyaltyPointsTenderTotal().Amount * returnedNet / originalTotal);
            allocatedTender = Math.Min(allocatedTender, sale.LoyaltyPointsTenderTotal().Amount);
            returnedEarnable = Math.Max(0m, returnedNet - allocatedTender);
        }

        foreach (LoyaltyTransaction source in rows.Where(row =>
                     row.Type is LoyaltyTransactionType.Earned or LoyaltyTransactionType.Redeemed
                     && !compensatedSourceIds.Contains(row.Id)))
        {
            int points = isFull
                ? Math.Abs(source.Points)
                : AllocatePoints(source, returnedNet, returnedEarnable, originalTotal, originalEarnable);
            if (points > 0)
            {
                await loyalty.AddTransactionAsync(LoyaltyTransaction.CreateCompensation(source, reason, points), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsFullReturn(Sale sale, IReadOnlyList<PartialReturnLine> lines)
    {
        Dictionary<Guid, int> quantities = lines.GroupBy(line => line.SaleItemId).ToDictionary(group => group.Key, group => group.Sum(line => line.QuantityReturned));
        return quantities.Count == sale.Items.Count && sale.Items.All(item => quantities.TryGetValue(item.Id, out int quantity) && quantity == (int)item.Quantity);
    }

    private static bool IsFullReturn(Sale sale, IReadOnlyDictionary<Guid, int> quantities)
        => quantities.Count == sale.Items.Count && sale.Items.All(item => quantities.TryGetValue(item.Id, out int quantity) && quantity == (int)item.Quantity);

    private static Dictionary<Guid, int> CalculateReturnedQuantities(Sale sale, IEnumerable<SaleReturn> returns)
    {
        var quantities = new Dictionary<Guid, int>();
        foreach (SaleReturn saleReturn in returns)
        {
            if (saleReturn.OriginalSaleId != sale.Id)
                throw new ArgumentException("Return does not belong to the sale.", nameof(returns));

            IEnumerable<(Guid SaleItemId, int Quantity)> items = saleReturn.Items.Count == 0
                ? sale.Items.Select(item => (item.Id, (int)item.Quantity))
                : saleReturn.Items.Select(item => (item.SaleItemId, item.QuantityReturned));
            foreach ((Guid saleItemId, int quantity) in items)
            {
                SaleItem saleItem = sale.Items.SingleOrDefault(item => item.Id == saleItemId)
                    ?? throw new ArgumentException($"Sale item '{saleItemId}' was not found in sale '{sale.Id}'.", nameof(returns));
                quantities[saleItemId] = quantities.GetValueOrDefault(saleItemId) + quantity;
                if (quantity <= 0 || quantities[saleItemId] > (int)saleItem.Quantity)
                    throw new ArgumentOutOfRangeException(nameof(returns), "Returned quantities must not exceed the original sale quantity.");
            }
        }

        return quantities;
    }

    private static void ValidatePartialReturnLines(Sale sale, IReadOnlyList<PartialReturnLine> lines)
    {
        if (lines.Count == 0)
            throw new ArgumentException("At least one return line is required for a partial return.", nameof(lines));

        Dictionary<Guid, SaleItem> saleItems = sale.Items.ToDictionary(item => item.Id);
        HashSet<Guid> returnedItemIds = [];
        foreach (PartialReturnLine line in lines)
        {
            if (!returnedItemIds.Add(line.SaleItemId))
                throw new ArgumentException($"Sale item '{line.SaleItemId}' appears more than once in the partial return.", nameof(lines));

            if (!saleItems.TryGetValue(line.SaleItemId, out SaleItem? item))
                throw new ArgumentException($"Sale item '{line.SaleItemId}' was not found in sale '{sale.Id}'.", nameof(lines));

            if (line.QuantityReturned <= 0 || line.QuantityReturned > (int)item.Quantity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lines),
                    $"Quantity returned ({line.QuantityReturned}) for item '{item.ProductName}' must be in [1, {(int)item.Quantity}].");
            }
        }
    }

    private static decimal CalculateReturnedNet(Sale sale, IReadOnlyList<PartialReturnLine> lines)
    {
        decimal returnedNet = 0m;
        foreach (PartialReturnLine line in lines)
        {
            SaleItem item = sale.Items.Single(item => item.Id == line.SaleItemId);
            decimal gross = RoundMoney(item.UnitPrice.Amount * line.QuantityReturned);
            decimal allocatedDiscount = RoundMoney(item.Discount.Amount * line.QuantityReturned / item.Quantity);
            returnedNet += RoundMoney(Math.Max(0m, gross - allocatedDiscount));
        }
        return RoundMoney(returnedNet);
    }

    private static decimal CalculateReturnedNet(Sale sale, IReadOnlyDictionary<Guid, int> quantities)
    {
        decimal returnedNet = 0m;
        foreach ((Guid saleItemId, int quantity) in quantities)
        {
            SaleItem item = sale.Items.Single(item => item.Id == saleItemId);
            decimal gross = RoundMoney(item.UnitPrice.Amount * quantity);
            decimal allocatedDiscount = RoundMoney(item.Discount.Amount * quantity / item.Quantity);
            returnedNet += RoundMoney(Math.Max(0m, gross - allocatedDiscount));
        }

        return RoundMoney(returnedNet);
    }

    private static int AllocatePoints(LoyaltyTransaction source, decimal returnedNet, decimal returnedEarnable, decimal originalTotal, decimal originalEarnable)
    {
        decimal numerator = source.Type == LoyaltyTransactionType.Earned ? returnedEarnable : returnedNet;
        decimal denominator = source.Type == LoyaltyTransactionType.Earned ? originalEarnable : originalTotal;
        return denominator <= 0m ? 0 : Math.Min(Math.Abs(source.Points), (int)Math.Floor(Math.Abs(source.Points) * numerator / denominator));
    }

    private static decimal RoundMoney(decimal amount) => new Money(amount).Amount;

    public sealed record PartialReturnLine(Guid SaleItemId, int QuantityReturned);
}
