# Delta for Loyalty Points Module

## MODIFIED Requirements

### Requirement: Loyalty account balance is an immutable-ledger sum

The system MUST compute a loyalty account balance as the sum of every signed loyalty transaction for that account. The system MUST NOT rewrite, deactivate, or exclude a historical transaction to correct a completed sale; it MUST record a typed compensating transaction instead.

#### Scenario: Compute a balance containing compensations

- GIVEN an account has `Earned +100`, `Redeemed -40`, `EarnReversed -100`, and `RedemptionRestored +40` transactions
- WHEN the system retrieves the account balance
- THEN the balance MUST be `0`
- AND every source and compensation transaction MUST remain present in the ledger

### Requirement: Loyalty transactions record typed, non-zero movements

The system MUST record each loyalty point movement as a non-zero signed transaction. It MUST support `Earned`, `Redeemed`, `EarnReversed`, and `RedemptionRestored` transaction types. `Earned` and `RedemptionRestored` transactions MUST have positive points; `Redeemed` and `EarnReversed` transactions MUST have negative points.

#### Scenario: Reject an invalid compensation sign

- GIVEN a request to create an `EarnReversed` transaction with `+10` points
- WHEN the loyalty transaction is validated
- THEN the system MUST reject the transaction
- AND the system MUST NOT persist a ledger row

### Requirement: Points are earned from the net eligible sale value

When a sale contains a same-sale `LoyaltyPoints` tender, the system MUST calculate its earning base as `max(0, SaleTotal - LoyaltyPointsTenderTotal)`. The system MUST use this net eligible value as the sole R-LP-05 earning base, including any line-level discounts already represented in `SaleTotal`.

#### Scenario: Earn points after a loyalty-points tender

- GIVEN a sale total of `10,000 COP`
- AND the sale contains a `1,000 COP` `LoyaltyPoints` tender
- AND the earning rate is `0.001` points per COP
- WHEN the sale is confirmed
- THEN the system MUST calculate points from `9,000 COP`
- AND the system MUST persist `9` earned points rather than `10`
- AND the sale, redemption, and earning movements MUST commit in one unit of work

### Requirement: Zero post-multiplier earnings do not create loyalty records

The system MUST calculate base points, apply the customer's pre-sale tier multiplier, and floor the result before it creates a loyalty account or an `Earned` transaction. If the floored post-multiplier result is zero, the system MUST save the sale normally and MUST create neither a loyalty account nor a zero-point transaction.

#### Scenario: A valid multiplier floors earnings to zero

- GIVEN a customer without a loyalty account
- AND a confirmed sale has a positive earning base that produces `1` base point
- AND the customer's pre-sale tier multiplier is `0.5`
- WHEN the system accumulates loyalty points
- THEN the system MUST floor the result to `0`
- AND the system MUST save the sale without failure
- AND the system MUST NOT create a loyalty account or an `Earned` transaction

### Requirement: Redemption respects a negative computed balance

The system MUST permit a balance to become negative when a valid cancellation or return compensation reverses points that the customer spent on a later sale. The system MUST reject any later redemption whose requested points exceed the computed balance, including when that balance is negative, until subsequent earnings restore sufficient balance.

#### Scenario: Block redemption after a compensating deficit

- GIVEN a valid compensation leaves an account balance of `-25` points
- WHEN an operator requests a redemption of `1` point
- THEN the system MUST reject the redemption with a recoverable insufficient-balance error
- AND the completed cancellation or return MUST remain compensated

## ADDED Requirements

### Requirement: Sale loyalty movements are compensated immutably and idempotently

The system MUST compensate each original sale-linked loyalty transaction by appending exactly one typed compensation transaction that references the original transaction through `CompensatesTransactionId`. A compensation MUST retain the source sale reference and a cancellation or return reason. The persistence model MUST enforce a unique constraint on `CompensatesTransactionId`, so an original transaction MUST NOT be compensated more than once, including after a retry or duplicate request.

#### Scenario: Compensate each original movement once

- GIVEN a completed sale has an `Earned +50` transaction and a `Redeemed -100` transaction
- WHEN the sale loyalty effects are compensated for a cancellation
- THEN the system MUST append one `EarnReversed -50` transaction linked to the earned transaction
- AND the system MUST append one `RedemptionRestored +100` transaction linked to the redeemed transaction
- AND the system MUST NOT modify either original transaction

#### Scenario: Reject duplicate source compensation

- GIVEN an original sale-linked earned transaction already has a compensation transaction
- WHEN a retry attempts to compensate that original transaction again
- THEN the system MUST create no second compensation transaction
- AND the persisted unique compensation link MUST preserve one compensation per source transaction

### Requirement: Cancellation and full return compensate exact original values atomically

The system MUST compensate 100 percent of every original sale-linked loyalty movement when a completed sale is cancelled or fully returned. It MUST use the original transaction points, rather than current loyalty rules or tier definitions. The system MUST stage the compensations in the same unit of work as the sale status, inventory restoration, and, for returns, the return record.

#### Scenario: Cancel a sale with loyalty activity

- GIVEN a completed sale has original earned and redeemed loyalty transactions
- WHEN the sale is cancelled
- THEN the system MUST append exact compensations for every original sale-linked movement
- AND the cancellation, inventory restoration, and compensations MUST commit atomically

#### Scenario: Full return rolls back with loyalty compensation

- GIVEN a completed sale has original loyalty transactions
- WHEN a full return fails before the unit of work is saved
- THEN the system MUST persist neither the return nor its loyalty compensations
- AND the original loyalty transactions MUST remain unchanged

### Requirement: Partial-return compensation uses an immutable original-sale snapshot

For the supported single partial-return event, the system MUST calculate loyalty compensation only from the original sale snapshot: original sale item quantities, unit prices, line discounts, sale total, loyalty-points tender, and original loyalty transactions. The system MUST NOT use caller-supplied `RefundAmount` as a loyalty-accounting input.

For each returned line, the system MUST derive returned net subtotal as the original returned gross value less the quantity-proportional original line discount, rounded as money to two decimals using `AwayFromZero`. It MUST allocate the original loyalty-points tender proportionally to the returned net subtotal over the original sale net total, using the same monetary rounding. It MUST derive returned earnable value as `max(0, returned net subtotal - allocated loyalty tender)`.

The system MUST reverse earned points as `FLOOR(original earned points × returned earnable value / original effective earnable total)`, capped at the original earned points. It MUST restore redeemed points using the same returned-sale proportion of the original redeemed points, capped at the original redeemed points. When all original quantities are returned, it MUST compensate the exact original transaction values. The system MUST NOT recompute compensation from current rules or current tier definitions.

#### Scenario: Ignore an arbitrary refund amount for partial-return accounting

- GIVEN a sale snapshot contains `10,000 COP` net total, a `1,000 COP` loyalty-points tender, and original loyalty movements
- AND a partial return selects original quantities with a calculated `2,000 COP` returned net subtotal
- WHEN the request supplies `RefundAmount = 9,999 COP`
- THEN the system MUST allocate loyalty compensation from the original snapshot and selected quantities
- AND the supplied `RefundAmount` MUST NOT change the compensated points

#### Scenario: Preserve original accounting after rule and tier changes

- GIVEN a completed sale has original loyalty transactions and a recorded original sale snapshot
- AND the loyalty rate or the customer's tier multiplier changes after the sale
- WHEN the sale is partially returned
- THEN the system MUST calculate compensation from the original snapshot and original transaction values
- AND the current rule and tier values MUST NOT change the result

### Requirement: Loyalty program metrics classify compensation correctly

The system MUST calculate total issued points from `Earned` transactions only. It MUST NOT count `RedemptionRestored` transactions as issued points. A redemption-count metric MUST count `Redeemed` transactions only and MUST NOT treat a `RedemptionRestored` transaction as a new redemption. The system MUST classify `Earned`, `EarnReversed`, `Redeemed`, and `RedemptionRestored` distinctly in loyalty program history and metrics.

#### Scenario: Do not inflate issued points after restored redemption

- GIVEN the ledger contains `Earned +100`, `Redeemed -40`, and `RedemptionRestored +40`
- WHEN the system calculates total issued points
- THEN the total issued points MUST be `100`
- AND the restoration MUST be presented as a redemption restoration rather than an earning

#### Scenario: Do not count a restoration as a new redemption

- GIVEN the ledger contains one `Redeemed` transaction and one `RedemptionRestored` transaction for its compensation
- WHEN the system calculates the redemption count
- THEN the metric MUST count exactly one redemption
- AND the restoration MUST be classified separately

### Requirement: Additive migration preserves historical loyalty data without repair

The system MUST introduce compensation metadata and transaction classifications through an additive migration. Existing ledger rows MUST remain immutable and eligible for balance calculation. The system MUST NOT backfill, repair, or infer compensation for historical cancelled or returned sales; compensation applies only to cancellation and return actions processed after rollout.

#### Scenario: Migrate an existing ledger without historical repair

- GIVEN an existing database contains loyalty rows for a sale returned before rollout
- WHEN the additive loyalty migration is applied
- THEN the original rows MUST remain unchanged
- AND the system MUST NOT create inferred compensation rows for that historical return
