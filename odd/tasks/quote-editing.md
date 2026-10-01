# ODD: Editable Quotes Before Conversion

## Problem

A quote that has not been converted, cancelled, or expired can currently only be
cancelled or converted. There is no way to correct a typo in the customer name, drop a
line the client no longer wants, or change the validity window. The operator's only
remedy is to abandon the quote and create a second one, which burns another
`COT-YYYY-NNNN` number and leaves two proposals in the history for the same client.

**Required behavior:** while a quote is still an initial proposal — status `Draft` or
`Active` — the owner (administrator) or the cashier may edit it as many times as needed.
Once it reaches `Expired`, `Converted`, or `Cancelled` it is frozen.

In this system "validated or paid" is the `Converted` state: that is the point where
`ConvertQuoteToSaleUseCase` creates the `Sale` and the quote stops being a proposal.

## Invariants

| # | Invariant | Enforced in |
|---|---|---|
| I1 | A quote is editable only while `Status is Draft or Active` | `Quote` aggregate |
| I2 | `QuoteNumber` and `CreatedAt` are immutable across any number of edits | `Quote` aggregate |
| I3 | `ExpiresAt` is always `CreatedAt + ValidityDays`, recomputed on edit | `Quote` aggregate |
| I4 | Removing a line from a quote deletes its `QUOTE_ITEMS` row; no orphan survives | `QuoteRepository` |
| I5 | Editing reuses the same discount-ceiling validation as creation | `UpdateQuoteUseCase` |
| I6 | Editing requires `Quotes.Edit`, granted to the cashier role by default | `PermissionKeys` |
| I7 | Editing never touches inventory | no sale/inventory code path is reached |

I4 is the one that will bite silently. `QuoteRepository.UpdateAsync` calls
`_dbContext.Quotes.Update(quote)`, which marks every *reachable* entity as `Modified`.
A line dropped from the collection is no longer reachable, so it stays `Unchanged` and
its row survives. `Quote.Total` sums in-memory items, so the UI total and the persisted
`QUOTE_ITEMS` sum would disagree forever. A negative-control test must delete a line
and then re-read the quote from a fresh context.

## Current State (verified against `main`)

| Piece | Status |
|---|---|
| `PermissionKeys.Quotes` | has `View`, `Create`, `Convert` — **no `Edit`** |
| `PermissionKeys.CashierDefaults` | includes the three existing quote keys — **no `Edit`** |
| `Quote` aggregate | constructor, `AddItem`, `Convert`, `Expire`, `Cancel` — **no update path** |
| `QuoteItem` | immutable, `private set` on every property |
| `QuoteRepository.UpdateAsync` | marks the whole graph `Modified`; no orphan cleanup |
| `UpdateQuoteUseCase` | **does not exist** |
| `QuoteHistoryViewModel` | `Convert`, `Cancel`, `ExportPdf`, `Send*`, `ViewDetail` — **no edit** |
| `QuoteDetailResult` | carries lines but **omits `Observations` and `ValidityDays`**, so the form cannot prefill |
| Schema | additive-only work: **no migration required** |
| Audit trail | automatic — `AppDbContext.AddAuditEntries` logs every `Modified` entity with `QuoteNumber` |

## Design

### Reuse over new surface

The edit form is the existing "Nueva cotización" form, not a second one. `QuoteView.xaml.cs`
already bridges the two sub-view-models via the `HistoryViewModel` attached property and
event subscriptions, so the edit action reuses that same bridge: raise
`QuoteEditRequested` from the history view-model, and have the view hydrate
`QuoteViewModel` and switch the sub-tab.

Line resolution is extracted from `CreateQuoteUseCase` into one shared internal builder.
Both use cases then resolve price, presentation, and units through the same code, which
matters because this is a money path — a drifted duplicate would produce a quote total
that disagrees with the invoice it later becomes.

### Domain shape

```
Quote.Update(customerName, validityDays, phone, email, observations)  // I1, I3
Quote.ReplaceItems(items)                                             // I1
Quote.CanEdit => Status is Draft or Active
```

`ReplaceItems` clears and re-adds rather than mutating `QuoteItem` in place, keeping
`QuoteItem` immutable and the discount range check in one place.

### Permission backfill

`DatabaseSchemaRollout.SeedDefaultRolesAndAdminAsync` grants cashier permissions only
when the role has *no* rows at all. Any install that already has a cashier role would
silently miss `Quotes.Edit` forever. The fix is an idempotent additive insert of the
missing `CashierDefaults` keys — that repairs the root cause for every future key too,
not just this one.

## Work Units

Each closes as one Conventional Commit on the feature branch, with its tests in the same
commit. Budget is 400 authored lines per slice; the forecast below is ~535, so this ships
as a two-PR chain.

### WU1 — domain rule, permission, and update use case

- `Quote.Update` / `Quote.ReplaceItems` / `Quote.CanEdit` with domain tests
- `PermissionKeys.Quotes.Edit` + `CashierDefaults` + idempotent rollout backfill
- shared quote-line builder extracted from `CreateQuoteUseCase` (no behavior change)
- `UpdateQuoteRequest` DTO and `UpdateQuoteUseCase` (reuses `CashierDiscountPolicy` → I5)
- `QuoteRepository` orphan-item deletion (I4)
- add `Observations` and `ValidityDays` to `QuoteDetailResult`

Rollback: revert the commit. No schema, no data migration, no persisted data is rewritten.

### WU2 — edit action in the quotes UI

- `QuoteSummaryRow.CanEdit` (pure status rule) and permission gating in the view-model
- `QuoteHistoryViewModel.EditCommand` + `QuoteEditRequested` event
- `QuoteViewModel` edit mode: hydrate form, swap confirm for update, restore create after
- `QuoteView.xaml` edit affordance + `QuoteView.xaml.cs` bridge wiring
- view-model tests for the enable/disable matrix and the hydration round-trip

Rollback: revert the commit. The application keeps working without the edit entry point.

## Verification

| Check | Command |
|---|---|
| Focused domain | `dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore` |
| Focused application | `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore` |
| Focused presentation | `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore` |
| Full | `dotnet test POS.sln --no-restore` |
| Build | `dotnet build POS.sln --no-restore` |
| Manual | `run-pos-app` skill — create a quote, edit it twice, confirm the number is unchanged; convert it, confirm the edit affordance is gone |

## Negative Controls

1. Edit a `Draft` quote, drop a line, re-read from a fresh `AppDbContext` → the
   `QUOTE_ITEMS` row is gone and the total matches the sum of the remaining lines.
2. Attempt to edit a `Converted`, `Cancelled`, and `Expired` quote → each is rejected
   before any write.
3. Attempt to edit with the cashier role missing `Quotes.Edit` → rejected, and the edit
   affordance is hidden.
4. Edit three times in a row → one quote, one `QuoteNumber`, one `CreatedAt`.

## Evidence

| Work unit | Commit | Authored lines | Focused test result | Runtime result |
|---|---|---|---|---|
| WU1 | `d0f7780` | 1036 (+1011/−25) | Domain 169, Application 496 — 0 failed | N/A — persistence proven by EF+SQLite round-trip in a fresh context |
| WU2 | `2fc8ab7` | 610 (+603/−7) | Presentation.Shared 345 — 0 failed | **pending** — manual `run-pos-app` check not yet run |
| Docs | `5515fdf` | 151 | — | — |

Full suite after WU2: `dotnet test POS.sln --no-restore` → **1028 passed, 0 failed**
(Domain 169, Presentation.Shared 345, SmokeTests 18, Application 496).
Build: 0 errors, 0 warnings.

**Budget overrun: both work units exceed the 400-line review budget.** 1616 authored
lines against a 800 forecast. See "Delivery" below.

## Findings That Changed The Design

Recorded because each one contradicts what this document originally assumed.

1. **`DbSet.Update` was wrong in three ways, not one.** The documented I4 orphan was
   only the first. Adding a line gave it a fresh `Id`, so `Modified` was sent as an
   `UPDATE` against a row that did not exist — a `ConcurrencyConflictException` on every
   added line. Worse, EF's relationship fixup drops a not-yet-fixed-up line from the
   `Items` navigation, and that navigation's backing field is the same `_items` list
   `Quote.Total` sums: the edit committed successfully and reported `Total == 0`. The
   final implementation reconciles lines by Id against an `AsNoTracking` query with
   change detection suppressed. The persistence negative control was genuinely RED
   three times before this was right.

2. **`QuoteItem`'s immutability is now load-bearing.** The reconcile infers "a surviving
   line needs no write" from the absence of mutators. Adding one later would silently
   stop that change persisting. There is a comment at that exact line; it is a real
   constraint, not a style note.

3. **Editing could not re-link a registered customer.** `UpdateQuoteRequest` carries no
   `CustomerId` and `Quote.Update` does not touch the association, so an edit keeps the
   original customer forever. The form still displays the link, which makes it look
   editable. This is a gap against "editable as many times as needed" and is the one
   item in this document still genuinely unimplemented.

4. **`QuoteStatus.Active` is unreachable.** No domain method sets it; quotes are created
   `Draft` and only ever become `Converted`, `Expired`, or `Cancelled`. The `Active`
   branch of `CanEdit` is therefore currently dead, exercised in tests only through a
   private-setter reflection escape hatch. Either the state is vestigial or a transition
   is missing.

5. **Smoke tests could not catch a broken Quotes module.** `TryCreateOptionalModule`
   downgrades a construction failure to a warning string, and the bootstrap smoke test
   never asserted `shell.Quote != null`. Past green runs of that project proved less than
   they appeared to. Now asserted.

## Delivery

Both work units are over the 400-line review budget. One honest slicing pass was done:

| Candidate slice | Authored lines | Fits? |
|---|---|---|
| Domain rule + tests | 234 | yes |
| Permission + rollout backfill + tests | 109 | yes |
| Application use case + builder + tests | 407 | no |
| Persistence reconcile + tests | 284 | yes |
| UI (WU2 as committed) | 610 | no |

Rebasing into that chain still leaves two slices over budget, and it would separate each
test from the behavior it proves.

**Decision — maintainer-approved `size:exception` on both work units, delivered as two
PRs.** Rationale: 1016 of WU1's 1036 authored lines are tests; production behavior in WU1
is ~330 lines and WU2 ~290. The overrun is the EF persistence reconcile, which was
genuinely red three times before it was correct. Slicing further would buy a number at the
cost of separating each test from the rule it proves, which this repository forbids.

| PR | Content | Commit | Authored lines |
|---|---|---|---|
| PR 1 | WU1 — domain rule, permission, application, persistence | `d0f7780` | 1036 |
| PR 2 | WU2 — edit action in the UI | `2fc8ab7` | 610 |

Both PRs declare `size:exception`. Review fan-out (`review-risk`, `review-resilience`,
`review-readability`, `review-reliability`) runs before publication: RDD is active and
both candidates exceed 400 changed lines.

## Outstanding Before Archive

1. **Editing cannot re-link a registered customer.** `UpdateQuoteRequest` carries no
   `CustomerId` and `Quote.Update` does not touch the association, so an edit keeps the
   original customer forever while the form displays the link as if it were editable.
   This is the one item of the original requirement left genuinely unimplemented.
   Maintainer decision: document as a limitation, or extend WU1.
2. **`QuoteStatus.Active` has no transition.** Either the state is vestigial and the
   `CanEdit` branch for it should go, or a transition is missing from the aggregate.
3. **Manual runtime check not yet run.** Create a quote, edit it twice, confirm the
   `QuoteNumber` is unchanged, then convert it and confirm the edit affordance is gone.
   No headless test can prove the WPF interaction.

## Out of Scope

- Editing after conversion — the adjustment belongs on the generated `Sale`.
- Editing `Expired` quotes. The documented remediation is "renew or create another
  quote"; allowing an edit would silently re-price a lapsed offer. Revisit only if the
  business asks for renewal-by-edit.
- Quote version history or an undo stack. The audit log already records each edit.
- Re-quoting a line from a changed catalogue price beyond the shared builder's normal
  price resolution, which is the same resolution creation already uses.
