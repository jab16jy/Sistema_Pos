# Design: Audit and Financial Integrity Hardening

## Decision summary

This change makes every new high-risk mutation attributable to one active session actor, every new return financially reproducible from completed-sale snapshots, and every new operational reference durable at the database boundary. It keeps the existing Clean Architecture direction: Presentation adapts contracts; Application authorizes and coordinates; Domain owns monetary and ledger invariants; Infrastructure implements EF Core/SQLite persistence, reporting queries, preflights, and migrations.

The design is deliberately narrow. It does not introduce a general ledger, an authorization framework replacement, automatic command retries, or a shared document-sequence service.

## Invariants and boundaries

| Area | Decision |
|---|---|
| Return amount | Derive it from persisted completed `SaleItem` snapshots and all prior return quantities; never accept money from the request. |
| Return financial effect | An accepted return has exactly one classified resolution: cash/digital settlement **or** receivable reconciliation. |
| Reporting date | Billings stay on `Sale.CreatedAt`; refund outflows use the persisted settlement timestamp. |
| Loyalty | Append signed, per-return compensation entries; never edit source or historical ledger rows. |
| Actor | `ICurrentOperatorSession` identifies the candidate actor; Application revalidates that user and permission before mutation. |
| Shift | The session actor, not a request ID or administrator role, must own the open shift being closed. |
| Stock | EF optimistic concurrency detects stale product writes; callers receive a retryable result and no command is replayed. |
| History | Legacy return, audit, loyalty, identity, and financial rows remain readable and are never inferred, renamed, or silently repaired. |

## Architecture

```text
WPF / Avalonia / Presentation.Shared
  -> actor-free command DTOs + return operation key + tender selection
  -> Application use cases
       -> CriticalCommandAuthorizer (session, active user, permission)
       -> Domain aggregates/calculators (return, loyalty, shift, quote)
       -> repository ports + IUnitOfWork
  <- Infrastructure (EF repositories, AppDbContext, SQLite queries/migrations)
```

`ICurrentOperatorSession` remains an Application port. A new Application service, `ICriticalCommandAuthorizer`, is the one canonical entry point for the eight targeted commands. `AuthorizeAsync(permission)` MUST: require an authenticated non-empty session; load the session user through `IUserRepository`; reject a missing or inactive user; obtain the current permission set through `IRoleRepository`; and return a canonical actor value containing that persisted user ID and username. It MUST reject before any entity is staged.

The following use cases consume the returned actor and remove caller-controlled actor fields: sale confirmation, `ProcessReturnUseCase`, receivable payment, `AdjustStockUseCase`, `RegisterPurchaseUseCase`, start shift, close shift, and cash closing. Their audit writes use the same session-backed actor already supplied to `AppDbContext`; no fallback to a request actor is retained. Presentation only removes/updates parameters and enables controls; it does not make authorization decisions.

`CloseShiftUseCase` loads the requested shift only after authorization and rejects unless `shift.CashierUserId == actor.UserId`. `StartShiftUseCase` creates its shift with that actor. There is intentionally no administrator bypass.

## Return event and financial-resolution model

`SaleReturn` remains the business reversal event. New returns add a nullable persisted `OperationId` correlation key; all new commands MUST supply one stable key when retrying an uncertain submission. A filtered unique index on non-null `OperationId` makes a retry return the already accepted event instead of creating a second return. Legacy rows keep `NULL` and remain unchanged.

`SaleReturnItem.RefundAmount` remains the persisted field to avoid a destructive column rename, but becomes a **derived refund snapshot**, not request input. `ReturnLineRequest` contains only `SaleItemId` and `QuantityReturned`. `ProcessReturnRequest` contains `SaleId`, `Reason`, selected lines, an operation ID, and a resolution choice; it contains neither an actor ID nor a refund amount.

Two append-only records express the mutually exclusive financial outcome:

| Record | Required fields | Constraints and meaning |
|---|---|---|
| `ReturnSettlement` | `SaleReturnId`, derived `Amount`, `SettledAt`, `PaymentMethod` | Unique `SaleReturnId`; FK to the return; method is Cash or a supported digital method only. It is the physical/digital customer outflow. |
| `ReturnReceivableReconciliation` | `SaleReturnId`, `ReceivableId`, derived `Amount`, `ReconciledAt` | Unique `SaleReturnId`; FKs to return and receivable. It reduces only the originating sale's valid open receivable and produces no cash/digital outflow. |

The return coordinator enforces exactly one resolution before the single save: settlement for cash/digital, reconciliation for receivable credit. The two tables deliberately remain separate because their accounting meaning and reporting treatment differ. Cross-table exclusivity is an Application invariant inside the transaction; unique return indexes make either individual record idempotent without introducing SQLite triggers.

### Safe return derivation

Within one `IUnitOfWork.ExecuteInTransactionAsync` scope, `ProcessReturnUseCase` performs the following ordered flow:

1. Authorize `PermissionKeys.Invoices.Return` and load the sale with immutable items/payments.
2. Require `SaleStatus.Completed`; load all return events and group their items by `SaleItemId`.
3. Reject duplicate requested lines, non-positive quantities, unknown lines, and quantities above `saleItem.Quantity - priorReturnedQuantity`.
4. For each accepted line calculate `derivedLineAmount = Money(unitPrice * quantity - lineDiscount * quantity / originalQuantity)`. Calculate in decimal, construct `Money` at each persisted line boundary, and reject an invalid negative result. Sum derived line snapshots with `Money` for the event amount.
5. Reuse the return identified by `OperationId` if it is already fully persisted; otherwise stage the return, return items, stock restoration/movements, exactly one resolution, and loyalty compensation.
6. Mark the sale returned only when the cumulative quantities of every original line equal the original quantities. Reconcile a receivable only after confirming its customer and sale match the return source and its balance covers the derived amount.
7. Save once. A validation, concurrency, uniqueness, or persistence failure commits none of the return, stock, settlement/reconciliation, or loyalty changes.

The completed `SaleItem` price, discount, quantity, presentation factor, and prior `SaleReturnItem` quantities are the only monetary/quantity source. Current catalog prices, a request amount, and historical returns without settlement evidence are never used to infer a disbursement.

## Loyalty compensation ledger

The existing `LoyaltyTransaction` is the immutable signed ledger. Extend compensation rows with nullable `SaleReturnId` in addition to their existing source transaction link. Replace the current unique source-link index with a unique composite index on `(CompensatesTransactionId, SaleReturnId)` for non-null return IDs. This permits multiple valid partial returns to compensate one original earning/redemption source while making a given return/source allocation idempotent. Legacy compensation rows retain their links and are not backfilled.

`CompensateSaleLoyaltyUseCase` becomes a no-save coordinator called by the return transaction. It reads original sale-linked `Earned` and `Redeemed` rows plus existing compensation rows, then for each source calculates:

```text
targetCumulative = allocation(sourcePoints, all prior returned quantities + current quantities)
alreadyCompensated = sum(existing compensation magnitudes for that source)
delta = clamp(targetCumulative - alreadyCompensated, 0, source magnitude)
```

Allocation uses the original sale's line price/discount snapshots, original loyalty tender, and the same rounding/floor rules recorded by the source behavior. A full cumulative return uses the exact source magnitude, not a ratio. `Earned` produces negative `EarnReversed`; `Redeemed` produces positive `RedemptionRestored`. A zero delta stages no row. The current return's composite unique key is the final duplicate-write guard; a racing conflict rolls back the whole return and a later retry recomputes from persisted evidence.

## Reporting and closing snapshots

Extend the operational report result, query contract, and `ShiftClosingSummary` with `CashReturnOutflows` and `DigitalReturnOutflows`. Preserve existing gross billed, cash-payment, digital-payment, and receivable-settlement fields.

Infrastructure reporting queries MUST include both completed and returned sales when summing gross billing by `Sale.CreatedAt`. They MUST join `ReturnSettlement` by `SettledAt` when summing return outflows, classify only Cash as drawer cash, and classify the existing supported non-cash methods as digital. `ReturnReceivableReconciliation` is excluded from outflows. A legacy return with no settlement contributes to neither outflow field.

Cash closing computes expected drawer cash from physical cash inflows less `CashReturnOutflows`; digital sales, digital receivable settlements, and digital refunds never affect drawer cash. The closing snapshot persists the two new outflow fields with zero defaults so previous closing rows remain readable.

## SQLite/EF Core persistence decisions

### Product concurrency

Add an integer `Product.Version` with database default `0` and configure it as an EF concurrency token. Centralize advancement in `AppDbContext` immediately before save: each modified `Product` gets `Version = originalVersion + 1`; additions retain `0`. This avoids scattered manual increments while ensuring EF includes the original version in updates.

Translate `DbUpdateConcurrencyException` to the existing recoverable application conflict result/exception, clear tracked changes, and preserve the original exception as the cause. A product update that affects stock (sale, return, purchase, or adjustment) and its inventory/financial records are saved once in the same EF transaction, so a stale product update rolls back the whole command. SQLite busy/locked errors continue through the existing transaction-conflict translation. Neither conflict type triggers an automatic retry.

### Referential integrity

After preflight, add database foreign keys with restrictive delete behavior for these non-polymorphic references:

| Dependent | Principal |
|---|---|
| `Sales.CashierUserId`, `SaleReturns.ProcessedByUserId`, `CashierShifts.CashierUserId` | `Users` |
| `Receivables.CustomerId`, `Receivables.SaleId` | `Customers`, `Sales` |
| `InventoryMovements.ProductId` | `Products` |
| `SaleReturnItems.SaleReturnId`, `.SaleItemId`, `.ProductId` | `SaleReturns`, `SaleItems`, `Products` |
| `Quotes.CustomerId`, `Quotes.ConvertedSaleId` when present | `Customers`, `Sales` |
| New settlement/reconciliation records | their return and receivable principals |

`InventoryMovement.ReferenceId` remains unconstrained because it is intentionally polymorphic. Existing cascade ownership relationships (for example, a return's items) remain only where the parent is not audit history; new audit-critical links are restrictive.

The username index uses the existing case-sensitive SQLite/BINARY semantics explicitly. A preflight finds exact duplicate usernames before the unique index migration; it never merges or changes case.

### Quote number allocation

Add `QuoteSequence(Year primary key, LastIssued)` and a repository operation that reserves a number only inside the same transaction that stages the quote. The SQLite repository uses one atomic UPSERT/`RETURNING` statement: on the first row for a year, initialize from the maximum valid historical number for that year; on conflict, increment `LastIssued`; return the issued value. `CreateQuoteUseCase` surrounds reservation, quote staging, and one save with `ExecuteInTransactionAsync`. The unique `QuoteNumber` index remains the final guard. Deletion never decrements the counter, and this mechanism is not generalized to invoices.

### User unit of work

`UserRepository.AddAsync` and `UpdateAsync` only attach/stage entities; they never call `SaveChangesAsync` or seed roles. Startup schema/role seeding stays in `DatabaseSchemaRollout`. Create, update, deactivate, and role-bootstrap paths own one Application-level `IUnitOfWork.SaveChangesAsync` after all related changes stage successfully. This removes the current repository-level partial commit boundary.

## Migration sequence and rollback

1. Create and restore-verify a database backup. Run the read-only integrity preflight before `Database.MigrateAsync`; diagnostics list table, record ID, relationship, and missing principal (or both user IDs and username for duplicates). Any finding aborts with no schema mutation.
2. Apply the username unique-index migration only after its preflight passes.
3. Apply additive return-resolution tables, nullable return operation/correlation data, reporting/closing default columns, loyalty return link/index replacement, product version, and quote sequence table. New tables receive their FKs when created.
4. Apply legacy operational FK migrations after all model columns exist. SQLite table rebuild migrations are split by table family (identity/receivables, then inventory/returns/quotes), copy rows unchanged, recreate indexes, and are reviewed independently below 400 changed lines.
5. Run migration smoke tests on a copied valid legacy database and re-run preflight/read-only integrity checks. Start the binary only after the complete migration set succeeds.

`DatabaseSchemaRollout.EnsureLatestSchemaAsync` must call the preflight gate before `MigrateAsync`, not after it. It MUST never invoke existing historical loyalty backfill for this feature's returns; that behavior would fabricate compensation or settlements.

Before new audit evidence is written, a failed rollout restores the verified backup. After any settlement, reconciliation, compensation, operation key, or issued quote number exists, rollback is application-only or a separately audited forward data migration. `Down` migrations MUST NOT delete or rewrite that evidence.

## Delivery units, dependencies, and conflicts

Each unit includes its focused tests and migration/preflight code and targets **at most 380 changed lines**. Split again before review if generated migration code exceeds that budget.

| Order | Unit | Depends on | Deliberate conflict / merge rule |
|---:|---|---|---|
| 1 | Return DTO derivation and completed-sale eligibility | — | Owns `ProcessReturnUseCase`, invoice DTOs, and return tests; merge first. |
| 2 | Return resolutions, receivable reconciliation, reporting, closing fields | 1 | Also edits `ProcessReturnUseCase`; rebase on 1, then own reporting SQL and closing mappings. |
| 3 | Cumulative loyalty compensation | 1 | Reads the derived return items; do not concurrently edit loyalty transaction indexes with Unit 2 migrations. |
| 4 | Canonical authorizer, actor-free contracts, owner-only shifts | 1, 2 | Edits return/purchase/stock/shift use cases and composition root; rebase after 2. |
| 5 | Product version and conflict translation | 2, 4 | Edits stock writers also touched by 2/4; merge after them and do not add retries. |
| 6a | Preflight plus username, actor, and receivable FKs | 2, 4 | Owns rollout gate and identity/receivable rebuild migrations. |
| 6b | Inventory, return, and quote FK preflight/migrations | 1, 2, 5, 6a | Owns remaining table rebuilds; no parallel generated migration edits. |
| 7 | User repository staging and Application UoW | 4 | Shares auth tests/DI; land after canonical actor wiring. |
| 8 | Transactional quote sequence | 6b | Owns `QuoteRepository`, quote mapping, and quote migration; no shared sequence abstraction. |

## Verification plan

| Layer | Focused evidence |
|---|---|
| Domain/Application | Derived partial/full amounts, prior-return limits, non-completed rejection, stable operation retry, one resolution, non-negative receivable, and rollback-on-failure. |
| Loyalty | Two partial returns equal full-return correction; source bounds/rounding; repeated return causes no second delta; legacy ledger remains unchanged. |
| Authorization | Unauthenticated, inactive, and unauthorized rejection before staging; audit actor equals session actor; shift owner succeeds and another cashier/admin fails. |
| SQLite integration | Settlement/reconciliation/report period separation; cash drawer excludes digital; stale two-context product write has no movement/outcome; every new FK rejects an orphan; preflight aborts before mutation. |
| Migration | Exact username duplicates abort; valid case-distinct names survive; copied valid legacy database migrates unchanged; quote concurrent allocation is distinct and monotonically non-reusable. |
| Presentation.Shared/WPF smoke | Both presentation consumers compile against actor-free contracts; Windows smoke verifies composition/bindings where available. |

Run focused tests per delivery unit, then:

```text
dotnet test POS.sln --no-restore
dotnet build POS.sln --no-restore
dotnet format POS.sln --verify-no-changes
```

The WPF smoke suite runs on Windows; non-Windows verification follows `openspec/config.yaml` constraints.

## Next phase

The `sdd-tasks` agent should turn the eight ordered units (with 6a/6b as separate review slices) into dependency-aware tasks, preserve the listed conflict ownership, and keep every task plus focused tests and generated migration below 400 changed lines.
