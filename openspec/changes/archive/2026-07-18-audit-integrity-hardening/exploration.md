# Exploration: Audit Integrity Hardening

## Outcome

Harden the smallest set of financial, identity, and persistence boundaries that can currently create records which are internally inconsistent, unauditable, or unsafe under concurrent use. The change is intentionally limited to confirmed findings; it does not redesign the POS, its UI, or the whole authorization model.

## Confirmed Findings

| Area | Confirmed behavior | Integrity impact | Minimal correction |
|---|---|---|---|
| Returns, refunds, cash, and reports | `ProcessReturnUseCase` restores stock, optionally reduces a receivable, compensates loyalty, and persists a `SaleReturn`, but it does not persist an actual refund disbursement. The selected refund type is not part of the Application request. `OperationalReportQueryService` aggregates only completed sales and receivable settlements. | A cash or digital refund is absent from the drawer calculation and report. A fully returned sale is also excluded from the gross-sales query after its status changes. | Persist one derived return settlement per return and report it as a dated cash/digital outflow. Include completed and returned sales in historical gross billing while excluding drafts and cancellations. |
| Manipulable refund amounts | `ReturnLineRequest` carries `RefundAmount`; `ProcessReturnUseCase` validates quantity but uses the supplied amount to create `SaleReturnItem` and reduce a receivable. | A caller can overstate or understate the monetary refund for an otherwise valid quantity. | Remove monetary amounts from the input contract and derive each amount from the immutable sale-line price, discount, quantity, and prior returned quantity. Accept returns only for completed sales. |
| Repeated partial-return loyalty compensation | `CompensateSaleLoyaltyUseCase` treats any compensation linked to a source transaction as fully compensated. A first valid partial return therefore prevents compensation for later valid partial returns. | Customer points can remain overstated after several partial returns. | Calculate the cumulative entitlement for all historical and current returned quantities, then append only the delta from compensations already recorded. |
| Application authorization and audit actor | Action-specific permission keys and `PermissionAuthorization` exist, but high-risk mutations do not consistently use them. Several mutation requests carry an actor ID while `AppDbContext` audit entries use the singleton current session actor. The return path separately checks only an administrator role and does not require an active user. | A caller-supplied ID can differ from the audited actor, and permissions are UI-dependent rather than an Application trust boundary. | Make the authenticated current session the canonical actor for targeted financial and inventory mutations; check the applicable permission in Application and reject inactive or unauthenticated actors. |
| Shift ownership | Starting a shift accepts a requested cashier identity; closing a shift accepts only a shift ID. The shift repository can retrieve any shift by ID, and no ownership check occurs before close. | One operator can create or close another operator's shift through the Application contract. | Derive the opening actor from the session and require the session operator to own a shift before closing it. Keep an explicit administrator override out of scope until a separate permission is defined. |
| EF Core stock concurrency | `ProductConfiguration` has no concurrency token. Sale and return paths read, mutate, and save products; `AppDbContext` converts concurrency exceptions, but none can be raised for a product update without a token. | Concurrent stock operations can silently overwrite each other. | Add a product version concurrency token, increment it centrally for modified products, and surface a retryable conflict without automatically replaying a financial operation. |
| Operational foreign keys | Aggregate child relationships are mapped, but operational references such as return-to-sale, return-item-to-sale-item/product, inventory-movement-to-product, sales/shift actor references, and quote customer/conversion references are scalar IDs without database foreign keys. | SQLite can retain orphaned financial or inventory records, weakening audit traceability and migration safety. | Add only non-polymorphic, audit-critical foreign keys after a preflight detects existing orphan rows. Keep generic `InventoryMovement.ReferenceId` without a foreign key because it intentionally references multiple aggregate types. |
| Repository-owned commits | `UserRepository.AddAsync` and `UpdateAsync` call `SaveChangesAsync` internally. | A repository can commit a partial unit of work and bypass the Application transaction boundary. | Remove repository commits and make the calling user use cases commit through `IUnitOfWork`, including their role-bootstrap work. |
| Username uniqueness migration | `AddUniqueIndexToUsername` creates a unique SQLite index without detecting historical duplicates. | Upgrading a database containing duplicate usernames fails during migration with no controlled remediation path. | Add a preflight that fails before schema mutation with an actionable duplicate list; do not silently rename login identities. Preserve the current case-sensitive equality semantics in this change. |
| Concurrent quote numbering | `QuoteRepository.GetNextQuoteNumberAsync` returns `Count + 1`, while `QuoteNumber` has a unique index. | Parallel quote creation can allocate the same number; one request fails, and deletion can also make the count unsuitable as a sequence. | Allocate quote numbers from a transactional, year-scoped database counter seeded from existing quote numbers. |

## Evidence Path

1. A return loads prior returns, validates quantities, mutates stock, optionally reduces receivables, invokes loyalty compensation, then saves once (`ProcessReturnUseCase`). Its request carries `RefundAmount`, but no refund method or disbursement.
2. Operational reporting reads only `SaleStatus.Completed` sales and receivable settlements; expected drawer cash is sale cash plus settlement cash (`OperationalReportQueryService`).
3. Loyalty compensation records source transaction IDs that already have any compensation, then skips all such sources on later calls (`CompensateSaleLoyaltyUseCase`).
4. Session-based permission helpers exist (`PermissionAuthorization`), while audit entries resolve the session actor (`AppDbContext`); shift start/close contracts rely on request IDs instead (`StartShiftUseCase`, `CloseShiftUseCase`, and `ShiftDtos`).
5. Product mapping has no concurrency property, while `AppDbContext` already maps `DbUpdateConcurrencyException` to a recoverable domain conflict (`ProductConfiguration`, `AppDbContext`).
6. Return, inventory movement, sale, quote, and shift mappings show the missing operational relationships; the current username migration only creates the index; `UserRepository` saves internally; and quote allocation is `CountAsync + 1` (`*Configuration`, migration, `UserRepository`, `QuoteRepository`, and `CreateQuoteUseCase`).

## Scope Boundary

### In scope

- Return settlement persistence, deterministic refund derivation, receivable reduction reconciliation, and cash/report treatment.
- Cumulative, idempotent loyalty compensation for multiple partial returns.
- Canonical session actor, permission enforcement, and owner-only shift lifecycle for the named high-risk Application mutations: sales, returns, receivable payments, stock adjustments, purchases, shifts, and cash closing.
- Product optimistic concurrency, critical operational foreign keys with migration preflight, Application-owned unit-of-work commits, safe username-index rollout, and quote-number allocation.

### Out of scope

- A general ledger, payment-gateway integration, automatic retry of financial commands, or a historical reporting rewrite beyond return effects.
- New administrator override permissions, a global RBAC redesign, or UI redesigns other than adapting callers to changed Application contracts.
- Foreign keys for polymorphic references, automatic repair or renaming of duplicate usernames, and case-insensitive username semantics.
- A shared sequence framework for invoices or other documents; this change hardens quote numbering only.

## Implementation Groups

Each group is independently reviewable and should remain below the 400 changed-line review target. Groups 1 and 2 share return-domain concepts but can be implemented in either order once their final contracts are agreed.

| Group | Deliverable | Focused regression tests |
|---|---|---|
| 1. Return financial integrity | Derived return-line amounts; persisted settlement method/amount; completed-sale-only eligibility; report cash/digital refund outflows and returned-sale gross treatment. | Overstated input amount cannot alter a refund; partial and full return settlement totals; receivable return creates no cash outflow; cash/digital refunds affect only their report column and expected drawer cash. |
| 2. Loyalty cumulative compensation | Delta-based compensation against all prior and current returned quantities. | Two partial returns produce the same total correction as one full return; a repeated command is idempotent; mixed earned/redeemed points and rounding retain the source bounds. |
| 3. Canonical actor and shift ownership | Session-derived actor and permission checks for targeted mutations; owner-only start/close shift rules. | Unauthenticated, inactive, and unauthorized session cases fail; request actor cannot diverge from audit actor; one cashier cannot close another cashier's shift; an owner can close their own open shift. |
| 4. Product concurrency | Product version token and consistent conflict translation. | Two SQLite contexts update the same tracked product; the second save raises the recoverable conflict and does not lose the first stock change. |
| 5. Referential and migration safety | Critical non-polymorphic foreign keys and preflight validation; duplicate-username migration preflight. | Valid legacy data migrates; an orphan reference aborts before schema changes; duplicate usernames abort with actionable diagnostics; foreign-key enforcement rejects newly orphaned operational records. |
| 6. Unit-of-work ownership | `UserRepository` stages changes only; user use cases commit atomically through `IUnitOfWork`. | A failing use case leaves neither user nor bootstrap role partially committed; a successful create/update commits exactly once. |
| 7. Quote number allocation | Year-scoped, transactional quote counter seeded safely from existing data. | Parallel SQLite contexts obtain distinct quote numbers; existing highest number seeds the next value; quote deletion does not reuse a number; rollover uses the next year's counter. |

## Rollout and Risk Controls

| Risk | Control | Rollback |
|---|---|---|
| Existing data blocks new constraints | Run read-only duplicate/orphan preflights before applying a migration. Abort rather than mutate identities or delete records. | Do not apply the migration until data is repaired; restore the database backup if a migration was applied. |
| Return reporting changes historical totals | Preserve gross billed sales separately from dated refund outflows and verify ranges spanning the original sale and return dates. | Revert the Application/report change and restore the prior database backup if financial reconciliation reveals a defect. |
| Concurrency conflict reaches users | Do not replay sales, returns, or payments automatically; return a retryable conflict and reload state. | Revert the token migration and Application handling only after confirming no concurrent writes depend on it. |
| Contract changes break presentation callers | Update both WPF and Avalonia/shared consumers in the same group and retain focused Application contract tests. | Revert the consumer and Application contract together; do not leave a caller-supplied actor fallback. |

## Verification Baseline

- The audit baseline passed `dotnet test POS.sln --no-restore` with 665 passing tests.
- Each implementation group must add its focused unit or SQLite integration tests before the full verification command: `dotnet test POS.sln --no-restore`.
- Final verification must also run `dotnet build POS.sln --no-restore` and `dotnet format POS.sln --verify-no-changes` as configured in `openspec/config.yaml`.

## Next Agent

`sdd-propose` should convert this exploration into a proposal that keeps the seven groups as separate reviewable work units, states the migration preflight policy explicitly, and names the exact capabilities to modify.
