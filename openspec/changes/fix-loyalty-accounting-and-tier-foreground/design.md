# Loyalty accounting and tier foreground design

## Decision summary

This change keeps loyalty accounting as an immutable signed ledger, corrects sale earnings to exclude the same sale's loyalty tender, and appends one auditable compensation per original sale movement. It also persists a closed black/white tier foreground and keeps malformed multiplier text from being submitted as a stale numeric value.

The change does **not** alter the refund settlement API, refund-method selection, or return UI workflow. `RefundAmount` remains stored for the existing settlement flow, but is not an input to loyalty allocation.

## Scope and invariants

| Area | Decision |
|---|---|
| Sale earnings | Earn from `max(0, sale total - LoyaltyPoints tender total)`, after existing line discounts. |
| Ledger correction | Never edit, delete, or deactivate historical rows; append typed compensation rows. |
| Cancellation and full return | Compensate 100% of every original sale-linked movement using its recorded points. |
| Partial return | Support the existing single event per sale only; allocate from immutable sale lines, quantities, tender, and source movements, never `RefundAmount`. |
| Idempotency | A compensation row links to exactly one source row; the database enforces one compensation per source. |
| Balance | May become negative after a valid compensation; existing balance checks block later redemption. |
| Tier foreground | Persist only `Black` or `White`, defaulting existing and new tiers to `White`. |
| Presentation validation | Multiplier text has an explicit valid/invalid state; invalid text cannot reuse an earlier parsed value. |

## Dependency flow

The dependency direction remains `Presentation -> Application -> Domain`; Infrastructure implements Domain contracts and depends inward. WPF brush types and EF Core types do not cross into Domain.

```text
WPF Views
  -> Presentation.Shared ViewModels / presentation records
  -> Application DTOs and use cases
  -> Domain entities, enums, value objects, repository interfaces
  <- Infrastructure EF repositories, configurations, migrations, AppDbContext
```

### Domain

- `Sale` owns named calculations for the sum of `PaymentMethod.LoyaltyPoints` tenders and the non-negative effective earnable total. Application code must not duplicate payment filtering.
- `LoyaltyTransactionType` gains `EarnReversed` and `RedemptionRestored`. Values are appended to the existing numeric enum so stored `Earned` and `Redeemed` values remain stable.
- `LoyaltyTransaction` gains nullable `CompensatesTransactionId`. A source movement has no compensation link; a compensation movement must reference its original source. The entity rejects zero points and enforces the following sign invariant:

  | Type | Required signed points |
  |---|---:|
  | `Earned` | `> 0` |
  | `RedemptionRestored` | `> 0` |
  | `Redeemed` | `< 0` |
  | `EarnReversed` | `< 0` |

  The compensation factory/constructor path also validates the compatible source pair: `Earned -> EarnReversed` and `Redeemed -> RedemptionRestored`. It preserves the source `SaleId` and records a cancellation or return reason. This makes an invalid type/sign or source/type pairing impossible before persistence.
- `LoyaltyTierTextForeground` is a Domain enum with exactly `Black` and `White`. `LoyaltyTier.TextForeground` defaults to `White`; constructors and `Update` validate the closed value alongside the existing name, benefits, multiplier, and hex background validation.
- `Money` remains the monetary rounding authority: two decimal places using `MidpointRounding.AwayFromZero`. Point calculations deliberately use `Math.Floor` and are integer-valued.
- `ILoyaltyRepository` gains a sale-scoped transaction read contract suitable for compensation staging. It returns the sale's persisted loyalty rows, including existing compensation links, without exposing EF concerns. Every in-memory test implementation must implement this contract.

### Application

`CreateSaleUseCase` completes the sale, stages redemption as today, then calls accumulation with `sale.EffectiveEarnableTotal()` rather than `sale.Total()`. It retains one final `IUnitOfWork.SaveChangesAsync` call; redemption, earning, sale, inventory, and receivable staging remain one unit of work.

`AccumulateLoyaltyPointsUseCase.RecordForSaleAsync` retains its no-save contract. Its order is:

1. Load the active rule and calculate base points from the supplied effective earnable amount.
2. Read the pre-sale balance and resolve the pre-sale tier.
3. Apply the tier multiplier and floor the result.
4. If the post-multiplier points are zero, return `false` before creating an account or transaction.
5. Otherwise create the account if needed and stage an `Earned` source transaction.

This preserves the tier timing and floor behavior while removing the zero-transaction failure.

A new Application-only `CompensateSaleLoyaltyUseCase` coordinates compensation. It receives the loaded sale, compensation context (cancellation, full return, or partial return), and reason; reads the sale-scoped ledger rows; derives compensation amounts from the immutable source data; and calls `AddTransactionAsync` only. It **must not** call `SaveChangesAsync`.

`CancelSaleUseCase` and `ProcessReturnUseCase` call this use case after validating and staging their existing sale/inventory/return changes but before their current single save. The return use case supplies the selected original quantities for partial returns and identifies a full return separately. Neither use case changes `ProcessReturnRequest`, `ReturnLineRequest`, or the refund settlement behavior.

### Infrastructure

`LoyaltyRepository` implements the sale-scoped read and continues to calculate balance by summing all signed rows. It changes metrics to filter by transaction **type**, not sign:

- total issued points: `Earned` only;
- redemption count: `Redeemed` only, in the existing date range;
- history: distinct labels and visual kinds for earned, earned reversal, redeemed, and redemption restoration.

The repository must include compensation rows in account history and balances. It must not backfill compensation for historical cancellations or returns.

EF configuration maps `CompensatesTransactionId` as an optional converted `Guid`, with an optional self-reference using restrictive delete behavior, and adds a unique index on that column. SQLite unique-index semantics allow multiple `NULL` source rows while prohibiting two non-null links to the same source. The repository pre-check makes normal retries idempotent; the unique index is the final concurrency safeguard.

`LoyaltyTierConfiguration` persists `TextForeground` as the tier enum with a non-null `White` default. The foreground remains a Domain value until Presentation maps it to a framework brush.

The generated EF migration is additive:

- add nullable `CompensatesTransactionId` to `LOYALTY_TRANSACTIONS`;
- add the optional self-reference and unique compensation-link index;
- add non-null `TextForeground` to `LOYALTY_TIERS` with a `White` default;
- update the generated designer and `AppDbContextModelSnapshot`.

No existing ledger row is rewritten, and no historical compensation is inferred. Existing tier rows receive the database default.

### Presentation

Application tier results and create/update requests carry `TextForeground`; `LoyaltyTierMapper` and `LoyaltyTierPresentation` preserve it. Presentation.Shared exposes a closed two-option selection and maps the enum to `Brushes.Black` or `Brushes.White` locally.

`LoyaltyViewModel` adds editor and preview foreground state. New-tier state initializes to `White`; loading a selected tier restores its persisted foreground; save requests include it. `LoyaltyView.xaml` presents only the two selections and previews the configured background and foreground together.

`CustomersViewModel` and `CashierViewModel` receive the foreground through their existing tier result mapping and expose a presentation brush. `CustomersView.xaml` binds that brush to every tier-badge icon and text element. Cashier applies it only if the existing tier information is rendered as a styled tier badge; normal cashier text and the unrelated loyalty-discount row keep their current styling.

## Compensation and allocation design

### Source selection and idempotency

The compensator considers only original, sale-linked `Earned` and `Redeemed` rows. It never compensates a compensation row. For each source:

1. If an existing row already has `CompensatesTransactionId == source.Id`, do not stage another row.
2. Otherwise derive the signed amount and append its matching compensation.
3. Let the unique index reject a racing second writer. A failed save rolls back the entire caller unit of work; a retry reads the persisted link and stages nothing further.

The existing `MarkCancelled` state validation and one-return-per-sale repository guard remain the normal retry boundaries. The link constraint additionally protects direct, duplicate, or concurrent paths that bypass those guards.

### Exact cancellation and full return

Cancellation and full return bypass proportional calculation. For every original source movement:

- `Earned +P` creates `EarnReversed -P`;
- `Redeemed -P` creates `RedemptionRestored +abs(P)`.

The values come from the original rows, not current rules, tiers, or redemption rates. Therefore a later rule/tier edit cannot change historical compensation.

### Single partial-return allocation

The partial-return branch uses the persisted `SaleItem` snapshot (`Quantity`, `UnitPrice`, `Discount`, and `UnitsPerPresentation`), the sale's original total and loyalty tender, and original source transactions. It intentionally ignores every `ReturnLineRequest.RefundAmount` during loyalty calculation.

For each selected returned line `i`, where `q_i` is its returned quantity and `Q_i` its original sale quantity:

```text
returnedGross_i    = Money(unitPrice_i * q_i)
allocatedDiscount_i = Money(lineDiscount_i * q_i / Q_i)
returnedNet_i      = Money(max(0, returnedGross_i - allocatedDiscount_i))
returnedNet        = Sum(returnedNet_i)

allocatedTender    = Money(originalLoyaltyTender * returnedNet / originalSaleNetTotal)
returnedEarnable   = max(0, returnedNet - allocatedTender)
originalEarnable   = max(0, originalSaleNetTotal - originalLoyaltyTender)
```

Each `Money(...)` operation rounds to two decimals away from zero. The implementation caps monetary allocations at their corresponding original amount to prevent a representation edge case from exceeding the immutable source.

For a non-full partial return:

```text
earnedReversal = min(originalEarnedPoints,
                     floor(originalEarnedPoints * returnedEarnable / originalEarnable))

redemptionRestoration = min(abs(originalRedeemedPoints),
                            floor(abs(originalRedeemedPoints) * returnedNet / originalSaleNetTotal))
```

The compensator emits a row only when the derived points are non-zero. It explicitly handles zero denominators as zero allocation; an original earned row should have a positive original earnable denominator, but the guard prevents an invalid division. If the selected quantities equal all immutable original quantities, the compensator uses the exact full-return branch instead of a recomputed ratio, guaranteeing 100% compensation despite monetary rounding.

The current one-return-per-sale guard is essential: it removes cumulative rounding allocation from this change. Supporting multiple partial return events later requires per-source cumulative entitlement (`entitlement to date - already compensated`) and is out of scope.

## Atomicity and failure behavior

All affected use cases stage aggregate mutations and new rows against the same EF `AppDbContext`, then call `IUnitOfWork.SaveChangesAsync` once:

| Flow | Staged before the one save |
|---|---|
| Sale with redemption | sale, payments, redemption row, earned row, inventory movements, optional receivable |
| Cancellation | cancelled status, stock restoration, inventory movements, compensation rows |
| Full return | returned status, `SaleReturn` and items, stock restoration, inventory movements, exact compensation rows |
| Partial return | returned status, one `SaleReturn` and selected items, selected stock restoration, inventory movements, allocated compensation rows |

EF Core persists one `SaveChangesAsync` atomically. If validation, compensation staging, a uniqueness conflict, or persistence fails, the save must commit none of the staged return/cancellation/inventory/ledger changes. The existing `AppDbContext.ExecuteInTransactionAsync` behavior remains available for callers that need an explicit multi-save transaction, but this design does not introduce a second save or transaction boundary.

## Multiplier parsing validity state

`TierPointsMultiplierText` is the displayed source of truth. Its setter normalizes only a single comma-or-dot decimal separator, validates a complete positive decimal token, and records both a parsed pending value and `IsTierPointsMultiplierTextValid`. Blank input, zero, negative values, extra separators, or other characters set validity to false and clear the pending parsed value.

`SaveTierAsync` requires this validity flag in addition to the existing tier validation. It constructs the request from the pending value derived from the current text, never from the prior `TierPointsMultiplier` backing value. A validation message is recoverable; no create/update use case is invoked. Domain validation stays as the final protection for non-UI callers.

## Test strategy

Tests follow the configured xUnit v3 suite and keep behavior at the owning layer.

| Layer | Coverage |
|---|---|
| Domain | Sale loyalty-tender/effective-total calculations; all four transaction sign invariants; valid compensation source pairs; tier foreground default and rejection of unsupported enum values. |
| Application: sale | Same-sale redemption earns from the net total; a multiplier that floors positive base points to zero creates neither account nor row; nonzero floored earnings still stage once. |
| Application: compensation | Cancellation and full return exactly compensate earn/redeem sources; rule/tier changes do not change results; partial return line-discount/tender/point math uses snapshots; arbitrary `RefundAmount` changes nothing; all-quantity partial selection reaches exact values; zero/cap boundaries create no zero rows. |
| Application: idempotency and failure | Existing link produces no second compensation; a duplicate return remains rejected by the one-return guard; negative post-compensation balance blocks later redemption; failed persistence leaves status, inventory, return rows, and ledger unchanged. |
| Infrastructure | SQLite migration defaults existing tiers to `White`; nullable compensation links preserve historical rows; unique non-null link rejects a duplicate; signed balance includes compensations; issued-points and redemption-count filters exclude restoration/reversal types. |
| Presentation.Shared | Foreground loads, new/edit/save requests, preview mapping, customer/cashier badge state, and invalid/blank/zero/malformed multiplier text cannot submit a stale number; `1.5` and `1,5` remain valid. |
| WPF smoke | On Windows, composition and bindings for the tier selector/preview and badge foregrounds do not break startup. |

Before implementation is accepted, run the configured checks where the platform permits them:

```text
dotnet format POS.sln --verify-no-changes
dotnet build POS.sln --no-restore
dotnet test POS.sln --no-restore
```

The WPF smoke project runs on Windows; Linux verification covers Domain, Application, Infrastructure-compatible, and Presentation.Shared projects as allowed by the configured constraints.

## Rollout and rollback

Deploy the additive migration before application binaries that write foregrounds or compensation links. The migration is safe for existing data because the new link is nullable and the foreground has a non-null `White` default. No historical cancellation/return is repaired automatically.

Before any compensation row is written, rollback may revert the application and, subject to deployment policy, the additive migration. After compensation rows exist, rollback must be application-only (for example, stop offering the new behavior) or use a separately audited data migration. It must not delete compensation rows or rewrite historical ledger evidence. The unrelated cancellation box-inventory defect remains explicitly out of scope.
