# Proposal: Correct Loyalty Accounting and Tier Foreground

## Motivation

Loyalty accounting currently over-awards points when a customer redeems points in the same sale, can attempt to persist invalid zero-point earnings after tier multiplier flooring, and does not compensate loyalty movements when a sale is cancelled or returned. These gaps make the ledger disagree with the commercial outcome and leave no auditable record of corrections.

Tier badges also allow an administrator to choose a background color without choosing a readable foreground, and invalid multiplier text can submit a previously valid numeric value that no longer matches the editor input.

## Scope

### In Scope

- Calculate sale earnings from the sale total after subtracting the same sale's `LoyaltyPoints` tender. Existing line-level discounts, including tier discounts, remain part of the sale total.
- Preserve existing multiplier flooring semantics, but create neither a loyalty account nor an `Earned` transaction when the post-multiplier result is zero.
- Add immutable, explicit compensating loyalty transactions for cancellation, full returns, and the existing API-level partial-return flow.
  - Cancellation and full return MUST reverse or restore the exact original points from every sale-linked loyalty transaction.
  - Partial returns MUST determine compensation from immutable original sale-line quantities, original line values, and original loyalty transactions.
  - Partial returns MUST NOT use caller-supplied `RefundAmount` as an accounting input.
  - Compensation MUST be staged in the existing unit of work so loyalty movements commit atomically with the sale status, inventory, and return changes.
  - A source loyalty transaction MUST NOT be compensated more than once.
- Permit a loyalty balance to become negative after valid compensation. Existing balance-based redemption checks will then block future redemption until later earnings recover the balance.
- Add a persisted per-tier foreground enum with exactly `Black` and `White` values, defaulting to `White`.
  - Expose and edit the setting in the Loyalty tier editor, including a foreground-aware preview.
  - Apply it to every tier-badge foreground element in Customers.
  - Apply it in Cashier only when its existing tier information is rendered as a styled tier badge; do not apply it to unrelated loyalty text.
- Reject blank, invalid, or non-positive tier multiplier input at the presentation boundary so a save cannot submit a stale previously parsed numeric value. Valid dot and comma decimal formats remain supported.
- Add additive persistence mappings and migrations for the compensation metadata and tier foreground. Existing tiers receive the `White` default.
- Update loyalty program metrics and history presentation so compensating movements are classified correctly and redemption restorations do not inflate issued-points totals.
- Add automated coverage for earning, compensation, negative-balance redemption blocking, tier foreground persistence/bindings, and multiplier validation.

### Non-Goals

- Changing the existing refund settlement API, refund method selection, or return UI workflow.
- Repairing historical cancelled or returned sales, or backfilling compensation rows for past transactions.
- Recalculating historical compensation using current loyalty rules or current tier definitions.
- Supporting multiple partial-return events for the same sale; the current one-return-per-sale behavior remains the supported boundary.
- Altering unrelated cancellation inventory behavior.
- Introducing arbitrary foreground colors, WPF brush values, or UI framework types into Domain.

## Capabilities

### Modified Capabilities

- `loyalty-points`: Earn points from the net eligible sale value and preserve an immutable ledger through explicit compensation movements.
- `sales-cancellation-and-returns`: Compensate loyalty effects atomically when a sale is cancelled or returned.
- `loyalty-tier-administration`: Persist and edit a constrained tier badge foreground while preventing stale multiplier saves.
- `customer-tier-badges`: Render tier badges with the configured readable foreground.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| Domain | Modified | Model loyalty tender totals, compensation transaction semantics, and the closed tier foreground vocabulary. |
| Application | Modified/New | Use the correct earning base, skip zero post-multiplier earnings, and stage sale-loyalty compensation within the caller's unit of work. |
| Infrastructure | Modified | Persist compensation links/types and tier foreground; query sale-linked movements; keep balance and program metrics correct. |
| Presentation.Shared | Modified | Carry foreground state and block saves when multiplier text is invalid. |
| WPF | Modified | Provide the tier foreground editor/preview and bind readable badge foregrounds. |
| Tests | Modified/New | Cover accounting allocation, idempotency, atomicity, migration defaults, UI state, and validation. |

## Risks and Mitigations

- **Partial-return rounding and allocation:** Quantity-proportional monetary allocation and point allocation can create rounding boundaries. The implementation will use the immutable original sale data and original transaction amounts, apply one documented deterministic policy, and cap a compensation at its source amount.
- **Duplicate compensation:** Retried cancellation or return handling could otherwise write multiple corrections. A transaction-to-source compensation link and unique persistence constraint will make duplicate compensation impossible.
- **Negative balances:** A customer may see a deficit after returning a purchase whose points were already spent. This is intentional: preventing the completed commercial reversal would leave the ledger incorrect, while existing balance checks prevent further redemption.
- **Schema rollback after use:** Compensation records are accounting evidence. Once written, they must not be deleted merely to revert application behavior.
- **Refund settlement ambiguity:** This change deliberately does not alter the caller-facing refund settlement contract. Loyalty compensation will remain independent of `RefundAmount`; settlement-policy changes require a separate approved change.

## Rollback

The migration is additive: it adds tier foreground data, compensation metadata, and new transaction classifications without rewriting historical ledger rows. Before any compensating transaction is recorded, rollback may consist of reverting the application change and its additive migration according to deployment policy. After compensations exist, rollback MUST be application-only or use a separately audited data migration; ledger evidence MUST NOT be deleted. Existing historical data is not repaired or mutated by this change.

## Success Criteria

- A sale that redeems loyalty points earns only from its total less that same sale's loyalty tender, and both redemption and earning remain in the single sale commit.
- A positive base amount that floors to zero after a valid tier multiplier creates no loyalty account and no zero-point earning row.
- Cancelling a sale or fully returning it creates exactly one immutable compensating movement per original sale-linked loyalty movement, reversing or restoring the exact original points in the current unit of work.
- An API-level partial return computes its loyalty compensation from immutable original quantities, line values, and source transactions; changing `RefundAmount` does not change the compensation result.
- Duplicate handling cannot compensate an original loyalty transaction twice.
- Valid compensation can leave an account negative, and a later redemption is rejected by the existing balance validation until the balance recovers.
- Existing tiers load with `White`; new and edited tiers persist only `Black` or `White`; Customers tier badges use the configured foreground for their icon and text; Cashier uses it only for a styled tier badge if one is rendered.
- Invalid, blank, or non-positive multiplier text produces a recoverable validation error and cannot save a prior numeric multiplier; valid comma and dot decimal formats can save.
- Loyalty issued-points totals and history distinguish earnings, reversals, redemptions, and restorations correctly.
- Relevant Domain, Application, Infrastructure, Presentation.Shared, and Windows presentation smoke coverage pass under the configured test and build commands.
