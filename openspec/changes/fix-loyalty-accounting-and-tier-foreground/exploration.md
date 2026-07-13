# Exploration: fix-loyalty-accounting-and-tier-foreground

## Scope

Correct loyalty accounting at sale completion, cancellation, and return processing, while adding a persisted, per-tier black/white badge foreground setting. This exploration does not implement application code.

## Confirmed Current State

### Sale earning and redemption

- `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs` stages a `LoyaltyPoints` payment after redemption, but calls `AccumulateLoyaltyPointsUseCase.RecordForSaleAsync` with `sale.Total().Amount`. `Sale.Total()` sums line subtotals only and ignores payments. This violates `openspec/specs/loyalty-points/spec.md` R-LP-05 because the redemption tender is not removed from the earning base.
- `src/POS.Domain/Entities/Sale.cs` already exposes the payments and their methods. A named domain calculation for the total `PaymentMethod.LoyaltyPoints` amount is preferable to duplicating a payment filter in application use cases.
- `src/POS.Application/UseCases/Loyalty/AccumulateLoyaltyPointsUseCase.cs` correctly skips when the *base* calculation is zero, but it creates an `Earned` transaction after applying the tier multiplier without checking the resulting value. A valid `0 < multiplier < 1` can floor a positive base amount to zero, which `LoyaltyTransaction` rejects.

### Cancellation, returns, and ledger integrity

- `src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs` cancels the sale and restores inventory but creates no loyalty compensation.
- `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs` creates a single `SaleReturn` per sale (including partial returns), restores inventory, and marks the sale as `Returned`, but creates no loyalty compensation.
- `src/POS.Infrastructure/Persistence/Repositories/LoyaltyRepository.cs` calculates balance by summing every signed transaction for the account. There is no active/reversed flag or transaction-to-transaction reversal link, despite the older loyalty spec referring to active transactions.
- `LoyaltyTransactionType` only contains `Earned` and `Redeemed`; `LoyaltyTransaction` permits only a positive Earned amount and a negative Redeemed amount. The current model cannot represent a typed, auditable reversal.
- A partial return accepts caller-supplied `ReturnLineRequest.RefundAmount`. It validates returned quantity but does not validate that the refund equals the corresponding original line value. Loyalty compensation MUST NOT rely on this operator-entered amount.

### Tier editor and badges

- `LoyaltyTier` currently persists `Name`, thresholds/benefits, and hex `Color`; `LoyaltyTierResult`, create/update DTOs, `LoyaltyTierPresentation`, and `LoyaltyViewModel` carry the same fields.
- `TierPointsMultiplierText` updates the backing text even when parsing fails, but leaves `TierPointsMultiplier` at its earlier numeric value. `SaveTierAsync` validates the stale number, so invalid displayed text can be saved as the prior value.
- `CustomersView.xaml` renders the tier badge with a configurable background but hard-coded white icon and text.
- `CashierViewModel` already loads tier name and color, but `CashierView.xaml` currently renders tier information only as ordinary text; it has no tier-color badge. The new foreground should be used when this existing tier information is rendered as a badge.
- The latest tier-color migration establishes the local convention: a non-null column with a default value, a matching EF configuration, generated migration designer, and snapshot update.

## Recommended Design

### 1. Make the earning base explicit

Add a domain-level sale calculation for the summed `LoyaltyPoints` tender, then pass:

```text
effectiveEarnableTotal = sale.Total - sale.LoyaltyPointsTenderTotal
```

to `EarnPointsRequest`. It MUST be non-negative and is the sole R-LP-05 earning base. It continues to include line-level discounts, including tier discounts, because those are already included in `Sale.Total()`.

In `AccumulateLoyaltyPointsUseCase`, calculate base points, determine the pre-sale tier, apply its multiplier, and only then decide whether to create an account and transaction. If the post-multiplier value is zero, return `false` without creating either record. This preserves the existing floor semantics and satisfies the no-zero-transaction invariant.

### 2. Use an immutable compensating ledger

Do not edit or deactivate historical earn/redeem rows. Extend the loyalty ledger with explicit compensation transaction types and a nullable `CompensatesTransactionId` (the original loyalty transaction). The compensation row keeps the same `SaleId`, has a descriptive reason containing the cancellation/return context, and uses a unique database index on `CompensatesTransactionId` so an original movement cannot be compensated twice.

Recommended signed types:

| Original event | Compensation event | Signed points |
|---|---|---:|
| Earned | EarnReversed | negative original earned points (or allocated share) |
| Redeemed | RedemptionRestored | positive original redeemed points (or allocated share) |

`LoyaltyTransaction` MUST validate sign by type. `GetBalanceAsync` may continue to sum all signed rows; the compensation rows naturally restore the correct balance. `GetTotalIssuedPointsAsync` MUST be revised to count actual earning events only, rather than every positive row, so restored redemptions do not inflate the program issuance metric. Program-history presentation needs labels/colors for both new event types.

Introduce an Application-layer `CompensateSaleLoyaltyUseCase` that queries source transactions by sale, stages compensation rows, and deliberately does **not** save. `CancelSaleUseCase` and `ProcessReturnUseCase` call it before their existing single `SaveChangesAsync`, preserving atomicity with status, inventory, and `SaleReturn` changes. Add a sale-scoped read method to `ILoyaltyRepository` and its EF implementation; all test doubles implementing that interface must be updated.

### 3. Deterministic return allocation policy (recommended; approval required)

Cancellation and a full return compensate 100% of every source loyalty movement for the sale.

For the currently supported single partial-return event, calculate the returned share from immutable original `SaleItem` data and quantities, not from `RefundAmount`:

1. Calculate each returned line's original net subtotal by proportionally allocating its stored line discount to the returned quantity, using `Money`'s two-decimal `AwayFromZero` rounding.
2. Allocate the original loyalty redemption tender across the returned subtotal proportionally to the original sale subtotal, with the same monetary rounding.
3. The returned earnable value is `returnedNetSubtotal - allocatedLoyaltyTender`.
4. Reverse `FLOOR(originalEarnedPoints * returnedEarnableValue / originalEffectiveEarnableTotal)`, capped at the original earned amount. Restore redeemed points with the same deterministic proportion of the original redemption points.
5. When all sold quantities are returned, compensate the original amounts exactly, never a recomputed value using today's rule or tier.

The original transaction values, not current loyalty rules/tier definitions, are the accounting source of truth. This prevents rule changes, tier changes, and rounding drift from changing historical compensation.

The current one-return-per-sale guard makes this policy deterministic. If multiple partial returns are later allowed, the design MUST change to cumulative allocation per source transaction (`cumulative entitlement - already compensated`) so rounded portions cannot be lost or duplicated.

### 4. Negative balances after compensation (recommended; approval required)

Allow the immutable ledger to become negative after a cancellation/return reverses points the customer has already spent on a later sale. Block further redemption while the computed balance is below the requested amount; future earnings can repay the deficit. Blocking a completed cancellation/return because points were later spent would leave sales and loyalty accounting inconsistent.

### 5. Explicit black/white tier foreground

Represent the allowed foreground as a Domain enum such as `LoyaltyTierTextForeground { Black, White }`, exposed by a `TextForeground` property on `LoyaltyTier` and all tier DTOs/results. The entity validates/defaults it to `White`, preserving existing badge appearance. This is safer than an unconstrained brush string and keeps WPF types out of Domain/Application.

Persist it in `LOYALTY_TIERS` through `LoyaltyTierConfiguration` and an EF migration with a non-null `White` default; update the generated migration designer and `AppDbContextModelSnapshot`.

Map it through:

- `LoyaltyTierMapper`, `LoyaltyTierPresentation`, and the tier create/update requests.
- `LoyaltyViewModel`: selected/new-tier state, a two-option editor collection/selection, validation, save requests, and preview text foreground.
- `LoyaltyView.xaml`: black/white selector and a preview that applies both background and foreground.
- `CustomersViewModel`/`CustomersView.xaml`: foreground brush bound to the customer tier badge's icon and both text blocks.
- `CashierViewModel`/`CashierView.xaml`: foreground brush bound to a tier badge shown with the assigned customer's existing tier information. Do not apply a tier foreground to unrelated loyalty discount rows.

### 6. Reject invalid multiplier text at the UI boundary

Keep `TierPointsMultiplierText` as the source binding text, but track parse validity separately. Invalid/blank/non-positive text MUST make `SaveTierAsync` fail with a validation message and MUST NOT submit the previous numeric value. Valid comma and dot decimal formats remain supported. Domain validation remains the final invariant.

## Affected Files

### Production

- `src/POS.Domain/Entities/Sale.cs` — named loyalty-tender/earnable-total calculation.
- `src/POS.Domain/Entities/LoyaltyTransaction.cs` — compensation link and type/sign invariants.
- `src/POS.Domain/Entities/LoyaltyTier.cs` — text foreground property/default/invariant.
- `src/POS.Domain/Enums/LoyaltyTransactionType.cs` — compensation event types.
- `src/POS.Domain/Enums/LoyaltyTierTextForeground.cs` — new closed black/white vocabulary.
- `src/POS.Domain/Interfaces/ILoyaltyRepository.cs` — sale-scoped transaction lookup.
- `src/POS.Application/DTOs/Loyalty/LoyaltyDtos.cs` — tier foreground DTO mapping; any compensation result/request kept internal to Application.
- `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs` — R-LP-05 effective earning base.
- `src/POS.Application/UseCases/Loyalty/AccumulateLoyaltyPointsUseCase.cs` — skip zero after multiplier.
- `src/POS.Application/UseCases/Loyalty/CompensateSaleLoyaltyUseCase.cs` — new atomic staging use case.
- `src/POS.Application/UseCases/Loyalty/GetLoyaltyProgramOverviewUseCase.cs` and `ManageLoyaltyTiersUseCases.cs` — transaction/tier mapping updates.
- `src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs` and `ProcessReturnUseCase.cs` — call compensation in the current unit of work.
- `src/POS.Infrastructure/Persistence/Repositories/LoyaltyRepository.cs` — sale lookup, balance/metrics semantics.
- `src/POS.Infrastructure/Persistence/Configurations/LoyaltyTransactionConfiguration.cs` and `LoyaltyTierConfiguration.cs` — mapping and unique compensation index.
- `src/POS.Infrastructure/Migrations/<timestamp>_AddLoyaltyCompensationAndTierTextForeground.cs`, matching designer, and `AppDbContextModelSnapshot.cs` — schema rollout.
- `src/POS.Presentation.Shared/ViewModels/LoyaltyViewModel.cs`, `CustomersViewModel.cs`, and `CashierViewModel.cs` — foreground state/bindings and multiplier parse validity.
- `src/POS.Presentation/Views/LoyaltyView.xaml`, `CustomersView.xaml`, and `CashierView.xaml` — admin selector/preview and readable tier badges.

### Tests

- `tests/POS.Application.Tests/Loyalty/CreateSaleWithRedemptionTests.cs` — net-of-redemption earnings in the same sale save.
- `tests/POS.Application.Tests/Loyalty/AccumulateLoyaltyPointsUseCaseTests.cs` — multiplier-floor-to-zero has no account or transaction.
- `tests/POS.Application.Tests/Loyalty/LoyaltyRepositoryTests.cs` — EF balance, compensation lookup/index, and issued-points metric semantics.
- `tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs` — cancellation, full return, and partial-return compensation/atomicity.
- `tests/POS.Presentation.Shared.Tests/LoyaltyViewModelTests.cs` — invalid multiplier text cannot save; foreground create/edit/load/default behavior.
- `tests/POS.Presentation.Shared.Tests/CustomersViewModelTests.cs` and `CashierViewModelTests.cs` — foreground state is loaded and exposed for badges.
- Existing `ILoyaltyRepository` fakes in `CreateSaleUseCaseTests.cs`, the loyalty tests above, `CustomersViewModelTests.cs`, and `CashierViewModelTests.cs` — implement the new read contract.

## Test Scenarios

1. A sale of 10,000 COP with a 1,000 COP loyalty tender and a 0.001 rate earns 9 points, not 10; sale, redeem, and earn rows commit once together.
2. A tier multiplier of 0.5 applied to one base point creates neither an account nor an Earned transaction; a nonzero post-multiplier still earns its floored amount.
3. Cancelling a sale reverses every sale-linked earned row and restores every sale-linked redeemed row in the same commit; a retry/duplicate cannot create a second compensation.
4. A full return produces the same complete compensation as cancellation, with the return, inventory movement, status, and loyalty rows atomic.
5. A partial return uses returned original quantities/subtotals rather than an arbitrary `RefundAmount`; it applies the documented monetary and point allocation exactly once.
6. Compensation after later spending can produce a negative balance and subsequent redemption is rejected until the account recovers.
7. A change to rules or tiers after the original sale does not change its cancellation/return compensation.
8. Program total-issued and redemption-count metrics do not misclassify restoration/reversal rows.
9. Existing tiers migrate with white foreground; new and edited tiers persist only black or white; Clientes and Caja bind the configured foreground for every tier badge element.
10. `TierPointsMultiplierText = "abc"`, blank, or `"0"` displays a recoverable validation error and does not create/update a tier using the prior valid multiplier. Dot/comma valid formats remain saveable.

## Risks and Ambiguities Requiring Resolution

1. **Partial-return refund settlement:** restoring redeemed points while also paying the full entered cash `RefundAmount` can over-refund the customer. The recommended policy allocates the original loyalty tender to the return, restores that portion of points, and refunds only the non-loyalty portion in money. The current return request has no payment/refund-method model, so the product owner must confirm this financial behavior before specification/design.
2. **Partial-return line discount allocation:** `SaleItem.Discount` is a line-level aggregate. The recommended quantity-proportional allocation is deterministic, but policy must confirm it is acceptable for promotions that cannot be split by unit.
3. **Negative balance policy:** recommended as above to ensure completed cancellations/returns always reconcile, but it changes the customer-facing balance behavior and needs explicit approval.
4. **Historical data:** existing loyalty rows have no compensation link. The migration can add nullable columns/defaults safely, but it cannot infer whether an already-cancelled/returned historical sale should be back-compensated. Recommended scope: no retroactive ledger repair; apply compensation only to new actions after rollout and document/report pre-existing discrepancies separately.
5. **Unrelated inventory defect:** `CancelSaleUseCase` restores `item.Quantity` rather than `item.Quantity * item.UnitsPerPresentation`, unlike returns. It is outside this loyalty-focused change but should be tracked separately because cancellation of box sales leaves stock inconsistent.

## Rollback Considerations

The schema migration is additive: new foreground and compensation-link columns/defaults plus enum values. Rollback can remove the new tier foreground and compensation metadata only if no compensation rows have been written. After production compensation rows exist, rollback should be application-only (disable new actions) until an audited data migration is planned; deleting ledger evidence is not acceptable.
