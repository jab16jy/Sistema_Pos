# ODD: Editable Quotes Before Conversion

## Problem

A quote that has not been converted, cancelled, or expired can currently only be
cancelled or converted. There is no way to correct a typo in the customer name, drop a
line the client no longer wants, or change the validity window. The operator's only
remedy is to abandon the quote and create a second one, which burns another
`COT-YYYY-NNNN` number and leaves two proposals in the history for the same client.

**Required behavior:** while a quote is still an initial proposal — status `Draft` or
`Active`, and its validity window has not lapsed — the owner (administrator) or the cashier
may edit it as many times as needed. Once it reaches `Expired`, `Converted`, or `Cancelled`,
or once its deadline passes, it is frozen. A quote that has lapsed is renewed by creating a
new one, never by editing the old one into a longer offer.

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
| I6 | Editing requires `Quotes.Edit`, granted to the cashier role by default, enforced **inside the use case** | `PermissionKeys`, `UpdateQuoteUseCase` |
| I7 | Editing never touches inventory | no sale/inventory code path is reached |
| I8 | A quote lapsed by calendar is **not** editable, whatever its `Status` says | `Quote.CanEditAt` |
| I9 | The total the operator approves equals the total that is persisted | `UpdateQuoteAsync` (see C2) |

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
Quote.CanEditAt(utcNow) => Status is Draft or Active
                             && ColombiaExpirationDate >= colombian(utcNow)   // I1, I8
Quote.EnsureEditableAt(utcNow)                                        // the one guard
```

`ReplaceItems` clears and re-adds rather than mutating `QuoteItem` in place, keeping
`QuoteItem` immutable and the discount range check in one place.

`CanEditAt` takes the clock so the rule is deterministic and testable; `CanEdit` is a
wall-clock convenience over it. `EnsureEditableAt` is the single rejection point, used by
`UpdateQuoteUseCase` before it does any work — there is exactly one copy of this rule, because
the duplicated copy that existed drifted (see C3).

### Permission delivery

**Original decision, superseded — see C1a.** The design proposed an idempotent additive
insert of missing `CashierDefaults` keys on every launch, to "repair the root cause for every
future key". That was wrong: `RolePermission` cannot tell "never granted" from "revoked by an
administrator", so a converging insert silently restores any cashier key an administrator
removed, on every launch, forever. It also wrote to the live database outside
`SnapshotBeforePendingMigrationsAsync`.

The seed now grants `CashierDefaults` only when the role has zero rows (a fresh install), and
new keys reach existing installs through a **data migration**, which is one-time, snapshotted
and reversible.

## Work Units

Each closes as one Conventional Commit on the feature branch, with its tests in the same
commit. Budget is 400 authored lines per slice; the forecast below is ~535, so this ships
as a two-PR chain.

### WU1 — domain rule, permission, and update use case

- `Quote.Update` / `Quote.ReplaceItems` / `Quote.CanEdit` with domain tests
- `PermissionKeys.Quotes.Edit` + `CashierDefaults`; delivery to existing roles is a **data
  migration**, not a rollout backfill (corrected in C1a — the original "idempotent additive
  insert" plan was a privilege-escalation loop)
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
5. **C1a** remove `Invoices.Return` from the cashier role as an administrator, then launch
   three times → the key is still gone.
6. **C2** raise a product's price after a quote is created, then edit the quote → the form
   total, the success message and the persisted `Quote.Total` all state the new amount, and
   the message says the catalogue price moved.
7. **C3** let a `Draft` quote's `ExpiresAt` pass → the edit affordance is gone, the row reads
   "Vencida" with the renewal guidance, and saving is rejected as expired. Same for a quote
   that was never emailed.

## Evidence

| Work unit | Commit | Authored lines | Focused test result | Runtime result |
|---|---|---|---|---|
| WU1 | `d0f7780` | 1036 (+1011/−25) | Domain 169, Application 496 — 0 failed | N/A — persistence proven by EF+SQLite round-trip in a fresh context |
| WU2 | `2fc8ab7` | 610 (+603/−7) | Presentation.Shared 345 — 0 failed | **pending** — manual `run-pos-app` check not yet run |
| Docs | `5515fdf` | 151 | — | — |

Full suite after WU2: `dotnet test POS.sln --no-restore` → **1028 passed, 0 failed**
(Domain 169, Presentation.Shared 345, SmokeTests 18, Application 496).
Build: 0 errors, 0 warnings.

## 4R Review Outcome — Correction Pass (2026-10-01)

The 4-lens adversarial review blocked the change on four severe findings. All four are
fixed on this branch. Batch 2 owns UI command wiring and test hygiene; this pass stayed
out of both.

| # | Finding | Severity | Commit | Status |
|---|---|---|---|---|
| C1a | Runtime permission backfill was a silent privilege-escalation loop | severe | `149fc0d` | fixed |
| C1b | `Quotes.Edit` unenforced at the trust boundary on a money write | severe | `c90b987` | fixed |
| C2 | Approved total was not the persisted total; every edit silently re-priced | severe | `e100a83` | fixed |
| C3 | Calendar-expired quotes stayed editable; expiry hidden behind send history | severe | `4edbf67` | fixed |

Final suite after the correction pass: `dotnet test POS.sln --no-restore` → **1043 passed,
0 failed** (Domain 174, Presentation.Shared 349, SmokeTests 18, Application 502).
Build: 0 errors, 132 warnings, all pre-existing `xUnit1051` in migration/persistence test
files this change does not touch.

### C1a — permission delivered by data migration, not a per-launch backfill

`DatabaseSchemaRollout.SeedDefaultRolesAndAdminAsync` was re-inserting every
`PermissionKeys.CashierDefaults` key missing from the cashier role on every launch. Its
comment claimed the opposite of what the code did, and the code was the problem:
`RolePermission` has no tombstone distinguishing "never granted" from "revoked by an
administrator", and `UpdateRoleUseCase` shields only the Administrator role, so cashier
permissions are administrator-editable through `ReplacePermissionsAsync`. An administrator
who removed `Invoices.Return` got it silently restored on the next launch, forever.

The backfill also mutated the live database outside `SnapshotBeforePendingMigrationsAsync` —
the exact envelope whose absence turns a transient "database is locked" into a boot loop.

The key is now delivered by `20261001183418_GrantQuotesEditToCashierRole`, following the
precedent in `20260713000530_ReplaceUserRoleEnumWithRbac`: one-time, snapshotted, reversible.

**Ordering trap found while building it.** The insert cannot key off "the cashier role
exists". On a brand-new database the role row *does* exist by the time this migration runs —
`ReplaceUserRoleEnumWithRbac` seeded it — but it holds zero permissions, and
`SeedDefaultRolesAndAdminAsync` runs *after* `MigrateAsync`. Inserting unconditionally left
the role with exactly one row, which made the seed's "zero rows" condition false, and a new
shop would have started with a single permission. The insert is therefore conditional on the
role already holding a grant set, which is precisely what separates an install that needs
repair from a fresh one. The fresh-install test is what caught this.

### C1b — `Quotes.Edit` enforced inside the use case

`UpdateQuoteUseCase` writes `UnitPrice`, `Quantity` and `Discount` and never consulted the
operator session; the only gate was `QuoteHistoryViewModel`. There is no live UI bypass
today, but a hidden affordance is not access control — any future caller reaching that
constructor inherited an unauthenticated money write.

`ICurrentOperatorSession` is now injected and `PermissionAuthorization.EnsureHasPermission`
enforces the key, surfacing as `UnauthorizedActionException` exactly like the other
money-path guards (`CreateSaleUseCase`, `ProcessReturnUseCase`, `AdjustStockUseCase`). The
view-model gate stays: it is correct UI behaviour, not the boundary.

**Not fixed here, recorded instead:** `CreateQuoteUseCase` and `CancelQuoteUseCase` are also
ungated at the use-case level. That is a pre-existing app-wide pattern — `CreateSaleUseCase`
is ungated the same way — not something this change introduced. Gating quotes alone would
leave the module internally inconsistent without making the system any safer. It needs its
own change, and a comment at the gate says so.

### C2 — the approved total now equals the persisted total

`QuoteViewModel.LoadForEdit` hydrated the cart with the quote's **frozen** snapshot price
and `RecalculateTotal()` summed that, but on save `QuoteLineBuilder` re-resolved
`product.PriceFor(presentationName)` — **today's** catalogue price — and the authoritative
`result.Total` was discarded. So the operator approved a number that was not the number
stored, the success message carried no amount at all, and `ConvertQuoteToSaleUseCase` carried
the new, higher price onto the invoice. Every edit silently re-priced the whole quote.

**Approach taken: make the divergence visible (the sanctioned alternative), not make the form
total live.** `IProductLookupService` was checked first, as the finding directed. It exposes
only `SearchAsync` (by query string) and `FindByBarcodeAsync` — **no by-id lookup** — and
`LoadForEdit` is synchronous. Making the form total live would therefore mean a product read
per line plus a second copy of presentation price resolution inside the ViewModel, which is
precisely the drift the shared `QuoteLineBuilder` exists to prevent. Not worth it for a
cosmetic improvement to when the number is shown.

`UpdateQuoteAsync` now captures the form total before saving, writes the persisted total into
the form *after* `ClearForm` (same reason `ClearForm` already spares `LastQuoteNumber`: an
emptied cart totals zero, and zero is not what the quote is worth), and states the amount in
the message. When the amount changed, the message says the catalogue price moved since the
quote was opened and shows what the form had been showing. Creation and the send path get the
same treatment.

Two tests: one with the catalogue price moved (form total, message and persisted `Quote.Total`
all agree at 4.000), one with it stable (message still carries the total, no false
re-pricing warning). The first was verified **RED** against the pre-fix code.

The better long-term fix is a by-id seam on `IProductLookupService` plus an async
`LoadForEdit`. Noted in the code; not done here.

### C3 — lapsed quotes are not editable; expiry no longer depends on send history

`Expire()` has exactly one caller in `src/` — the conversion path — so a quote whose
`ExpiresAt` passed kept `Status = Draft` forever. A status-only rule left a lapsed proposal
editable, and saving it with a larger `ValidityDays` resurrected it as a live, convertible
offer, re-pricing something the client had already let lapse.

**Maintainer decision, applied:** a quote expired *by calendar* is treated exactly like
`QuoteStatus.Expired` — not editable. Renewal means creating a new quote, which is what
`QuoteSummaryRow.ExpirationGuidance` already said.

The aggregate gained `ColombiaExpirationDate`, `CanEditAt(utcNow)` and
`IsLapsedByCalendar(utcNow)`, using the same UTC-5 comparison `ConvertQuoteToSaleUseCase`
already applied — one rule, not a second subtly different one. Colombia has no daylight
saving, so UTC-5 is fixed; that is documented at the constant. `CanEdit` remains as the
wall-clock convenience over `CanEditAt`, so the rule stays testable rather than hiding a
clock read inside a property.

`ConvertQuoteToSaleUseCase` now goes through the aggregate's accessor.
`GetQuoteHistoryUseCase` uses `ColombiaExpirationDate` rather than the raw UTC date, which
disagreed with the other two sites during the evening UTC-5 hours.

**Second-order bug, also fixed.** `GetQuoteHistoryUseCase` only computed `ReminderLabel` when
`HasSuccessfulEmail` was true, and `QuoteSummaryRow.IsExpiredByCalendar` was inferred by
string-comparing that label to `"Vencida"`. A quote that was never emailed reported no expiry
at all, however long lapsed — it kept its edit affordance and showed no renewal guidance.
Expiry is now derived from the quote itself and travels as a real `IsExpiredByCalendar` field
on `QuoteSummary` and `QuoteSummaryRow`. `HasReminder` no longer consults send history, and
the labels are emitted only for open proposals, so a converted or cancelled quote does not
grow a "Vencida" chip — which also keeps `CanConvertToSale` honest.

**A duplicated guard was deleted, not patched.** `UpdateQuoteUseCase` had its own early-out
copy of the editability check that threw with the raw `Status`. That copy had already
drifted: it reported a lapsed quote as "estado Draft" instead of *vencida*. It now delegates
to `EnsureEditableAt(utcNow)` on the aggregate. Its one test had pinned the *copy's*
exception type, so `ExecuteAsync_WhenQuoteIsConverted_RejectsBeforeAnyWrite` now expects
`QuoteAlreadyConvertedException` — which is what `QuoteTests` already specified for
`Converted`. Both paths agreeing is the point; the assertion had been protecting the
duplicate.

Tests: a Draft with a past `ExpiresAt` is rejected at both the aggregate and the use case;
a Draft expiring *today* is accepted at both, in Colombian calendar days; the UTC-5 boundary
is pinned directly; a lapsed row loses its edit affordance and gains the renewal guidance;
a never-emailed lapsed quote is correctly reported as expired; a terminal quote is not.

`GetQuoteHistory_LabelsSuccessfulEmailedQuotesByCalendarExpiry` was rewritten. Its
`Assert.Null` on the never-emailed quote's reminder **mandated the defect** — it asserted that
a lapsed, never-sent quote reports no expiry. It is now
`GetQuoteHistory_LabelsEveryQuoteByCalendarExpiryRegardlessOfWhetherItWasEmailed`, which
asserts the correct behaviour and adds the never-emailed-lapsed and terminal-status cases.

### Test assertions changed, and why

| Assertion | Was | Now | Why |
|---|---|---|---|
| `CashierPermissionBackfillTests` | `Assert.True(CashierDefaults.SetEquals(granted))` after a "rewind" | three tests: migration grants the key, fresh install seeds the set, revoked key stays revoked | `SetEquals` mandated the escalation loop — it required the role to converge on the full default set on every launch, which is precisely the behaviour that un-does an administrator's revocation |
| `ExecuteAsync_WhenQuoteIsConverted_RejectsBeforeAnyWrite` | `QuoteNotEditableException` | `QuoteAlreadyConvertedException` | the test pinned the use case's duplicated guard rather than the domain contract; `QuoteTests` already specified `QuoteAlreadyConvertedException` for `Converted` |
| `GetQuoteHistory_LabelsSuccessfulEmailedQuotesByCalendarExpiry` | `Assert.Null` on the never-emailed quote's label | `Assert.Equal("Vencida")` + `IsExpiredByCalendar` true | asserted that a lapsed never-sent quote reports no expiry — the second-order defect |

No other assertion was weakened or removed.

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
   **Fixed in batch 2 (C4b) — see the batch 2 section below.**

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
   **Closed in batch 2 (C4b, commit `3df8bac`): the association is now assigned by
   `Quote.Update`, so it can be re-linked, unlinked, or preserved.**
2. **`QuoteStatus.Active` has no transition.** Either the state is vestigial and the
   `CanEdit` branch for it should go, or a transition is missing from the aggregate.
3. **Manual runtime check not yet run.** Create a quote, edit it twice, confirm the
   `QuoteNumber` is unchanged, then convert it and confirm the edit affordance is gone.
   No headless test can prove the WPF interaction. The correction pass added behaviour the
   operator will see, so this check now also owes: confirm the success message carries the
   total, and that a lapsed quote's row shows "Vencida" with the renewal guidance.
4. **`CreateQuoteUseCase` and `CancelQuoteUseCase` are ungated at the use-case level.** Same
   pre-existing app-wide pattern as the ungated `CreateSaleUseCase`; recorded in C1b and
   marked in the code. Needs its own change, not a quotes-only patch.
5. **The edit form's total is frozen, not live** (C2). Making it live needs a by-id lookup on
   `IProductLookupService` and an async `LoadForEdit`. The divergence is surfaced rather than
   hidden, which closes the money hole but leaves the nicer form outstanding.

## Out of Scope

- Editing after conversion — the adjustment belongs on the generated `Sale`.
- Editing `Expired` quotes, whether the expiry came from `Expire()` or from the calendar
  passing. The documented remediation is "renew or create another quote"; allowing an edit
  would silently re-price a lapsed offer. Maintainer-confirmed in the correction pass:
  renewal means creating a new quote. Revisit only if the business asks for renewal-by-edit.
- Quote version history or an undo stack. The audit log already records each edit.
- Re-quoting a line from a changed catalogue price beyond the shared builder's normal
  price resolution, which is the same resolution creation already uses.

## 4R Review Outcome - Correction Pass, Batch 2 (2026-10-01)

Batch 2 owns the remaining seven findings from the second review: UI command wiring,
the silently dropped customer, persistence-contract honesty, and test hygiene. Batch 1
(C1a, C1b, C2, C3) was not revisited except for one assertion, reviewed independently
below.

| # | Finding | Severity | Commit | Status |
|---|---|---|---|---|
| C4a | "Descartar" could never enable; sibling commands unaudited | severe | `b448552` | fixed |
| C4b | Customer picked during an edit was silently discarded | severe | `3df8bac` | fixed |
| C5a | False `ReplaceItems` contract, dangling `(I4)`/`(I7)`, duplicated message | moderate | `e486489` | fixed |
| C5b | `ConvertQuoteToSaleUseCase` had no real-repository coverage | severe | `2727bad` | fixed |
| C5c | `Task.Delay(50)` races; non-hermetic raw-table count | moderate | `320a650`, `50f0565` | fixed |
| C6a | Create and update restated the same guards and projection | moderate | `4ab58cc` | fixed |
| C6b | `validityDays` had no upper bound | moderate | `11af062` | fixed |
| C6c | Four hand-written spellings of one status set | minor | `cecc4c2` | fixed |

Two further defects were found **inside batch 2's own work** while verifying it, and are
fixed here: the tree did not compile (`CS0103`), and `QuoteSummaryRow` shipped a
duplicated `<summary>` tag. Both are recorded below rather than quietly corrected.

### Two defects found in this batch's own partial pass

The branch already carried batch 2 commits when this pass began. Verifying them found
that the work was **incomplete, not wrong**:

1. **The tree did not build.** `QuoteValidityDaysOutOfRangeException` referenced `Quote`
   from `POS.Domain.Exceptions` with no `using POS.Domain.Entities`, so `Quote.cs` and
   `QuoteViewModel.cs` failed with `CS0103` (2 errors) and every project downstream of
   `POS.Domain` failed to compile. C6b was mid-flight.
2. **`QuoteSummaryRow.CanConvertToSale` had two `<summary>` tags** (commit `cecc4c2`), the
   outer one never closed, so the block was malformed XML. Harmless to the compiler,
   useless to anything that reads the doc comment.

Fixed in `1adb6e3` (the doc tag) and completed in `11af062` (the bound plus its tests).

### C4a - the "Descartar" button was a one-way door

`RelayCommand.CanExecuteChanged` is a bare event with no `CommandManager` behind it
(verified: no `CommandManager` anywhere in `src/`), so WPF evaluates `CanExecute` once at
bind time. "Descartar" was bound while `IsEditing` was still `false` and never re-queried:
entering edit mode had exactly one exit, saving.

`_cancelEditCommand.RaiseCanExecuteChanged()` now fires from the `IsEditing` setter. Two
tests, because one of them is not enough: `CanExecute` re-reads its predicate on every
call, so a test that only calls `CanExecute` would pass with the raise missing - it pins
the value, not the notification. WPF only re-reads it if `CanExecuteChanged` fires.

**Audit of every other command in `QuoteViewModel`.** Three of the thirteen commands
close a `canExecute` over mutable state; the other ten have no predicate and are always
enabled, so they cannot go stale.

| Command | Predicate | Raises where | Verdict |
|---|---|---|---|
| `ApplyCartDiscountCommand` | `CartItems.Count > 0` | `RecalculateTotal` | correct |
| `ClearCartDiscountCommand` | `HasManualDiscount` | `RecalculateTotal` | correct |
| `CancelEditCommand` | `IsEditing` | `IsEditing` setter | **was broken, now fixed** |

The first two are sound because every path that mutates `CartItems` or a line's
`Discount` ends in `RecalculateTotal` - `AddToCart`, `RemoveFromCart`, `ApplyCartDiscount`,
`ClearCartDiscount`, `LoadForEdit`, `ClearForm` - and a line's own `PropertyChanged`
handler calls it too. No other instance of this class of bug.

### C4b - the customer a cashier picked during an edit is now saved

`LoadForEdit` hydrated `SelectedCustomerId`, the picker stayed live, `UpdateQuoteRequest`
carried no `CustomerId`, and `Quote.Update` never assigned the association. The operator
searched a customer, clicked "Usar", saved, read "actualizada correctamente" - and the
quote was still a walk-in.

Closed in three layers: `CustomerId` on the DTO, the assignment in `Quote.Update`, and the
current selection in the form. `Quote.Update`'s doc states the consequence plainly: because
`null` now means *unlink*, this cannot be expressed as "leave unchanged" - a caller that
must not touch the association passes the quote's current `CustomerId`. The existing EF
configuration already honoured this: `CustomerId` is nullable with
`OnDelete(DeleteBehavior.Restrict)`, exactly as creation relies on, so no schema or
migration was involved.

Three tests replace one. The old test asserted the field **hydrates**, which is precisely
why the drop-on-save survived: now the customer has to **survive a save**, clearing it has
to clear the association, and an untouched selection has to preserve the original.

### C5a - the contract now says what the code does

`ReplaceItems` claimed the repository could tell a dropped line from a corrected one by
`Id`. That is false: `QuoteItem`'s constructor assigns a fresh `Guid` and
`QuoteLineBuilder` builds fresh items on every save, so `keptItemIds.Contains(entry.Entity.Id)`
is always false and an edit is DELETE-all plus INSERT-all. The comment now says that, and
names the earlier claim as false so nobody re-adds it.

The load-bearing half of that inference - "a surviving line needs no write" - rests
entirely on `QuoteItem` having no mutators. That was documented only in
`QuoteRepository`, a different project from the file a maintainer opens to add a setter.
The constraint now lives on `QuoteItem`.

The `(I4)` and `(I7)` references in `src/**` resolved only in this document, which is
archived per change; they are gone. `QuoteNotEditableException` lost its duplicated
`Converted` wording (that path raises `QuoteAlreadyConvertedException`) and its unreachable
`_` arm - every call site constructs it only after ruling `Converted` out.

### C5b - conversion against real EF and SQLite

`ConvertQuoteToSaleUseCase` is the riskier caller of the rewritten `UpdateAsync`: it runs it
**inside** `ExecuteInTransactionAsync` while `CreateSaleUseCase` writes through the same
`AppDbContext`, over a quote whose `Items` arrived via `.Include(q => q.Items)` and are
therefore already tracked. `CommercialPresentationFlowTests` uses a hand-rolled
`SingleQuoteRepository`, and `QuoteExpirationTests` throws before `UpdateAsync`.

New test on a real `SqliteConnection` + real `AppDbContext` + real `QuoteRepository`,
asserting `Converted`, `ConvertedSaleId` set, both lines intact with the right total, and
no change in the `QUOTE_ITEMS` row count - re-read from a *fresh* context, so nothing the
converting context tracked can paper over a lost row.

### C5c - hermetic assertions

Three tests waited on `Task.Delay(50)` for a fire-and-forget continuation
(`ViewModelBase.RunSafe`). They now await a `TaskCompletionSource` completed by the event
handler, with a 10s timeout as a backstop against a hang rather than as the mechanism. The
two negative-control tests needed no wait at all and take none: their guards are synchronous
inside `RequestQuoteEdit`, which is itself what those tests prove.

The raw-table counter took no `WHERE`, so it could only pass for the right reason while each
fixture seeded exactly one quote. It is now scoped by quote id.

### C6a - one guard and one projection

`TryBuildCartItems` is extracted: both save paths now get their guards, their cart
projection, and therefore their field list from one place. A `Theory` pins both paths to
the same message for the same incomplete form. The irony is recorded in the code: the layer
that extracted `QuoteLineBuilder` specifically to stop price resolution drifting duplicated
the cart mapping one level above, unexamined - and that is exactly how the customer
association went missing in the first place.

### C6b - the validity window has a ceiling

`Quote.Update` rejected only `<= 0`, so a quote could be re-armed for ~2.9M days.

**The ceiling is not about the exception.** `DateTime` does not overflow until roughly 2.9
million days from today (`DateTime.MaxValue` is year 9999), so every absurd value below
that - 500,000 days included - is perfectly representable, passed every previous guard, and
got **persisted**. It then reached the client as a rendered deadline, because `PdfService`
prints "Válida hasta" from `ExpiresAt`. A quote valid until the year 3395 is a worse
outcome than a crash, because it looks like data.

**Ceiling: 3650 days - a decade.** Deliberately a sanity bound, not a business policy:

- The form's `IntegerUpDown` caps at 365, so no value the system can legitimately produce
  falls outside it and the domain can never contradict the control the operator actually
  uses. A 365-day domain ceiling would be a second, undocumented rule about how long a
  quote may live, in a codebase whose design document fixes no ceiling at all.
- Reporting horizons in this app are weeks to months (`PurchasesViewModel` defaults to one
  month back), so nothing legitimate approaches a year.
- A decade is already nonsense for a commercial proposal, so every absurd value is refused
  - which is the job - while lifting the bound later stays a one-line change touching no
  schema.
- `installer/EPOS.iss` was checked as the brief suggested and contributes **nothing**: it is
  a 41-line Inno Setup script with no validity, horizon or expiry concept. The ceiling is
  justified against the form control and the reporting horizons, not invented, and not
  borrowed from the installer.

`QuoteValidityDaysOutOfRangeException` is a domain exception whose message names the rule,
the quote and the value typed - not an `ArgumentOutOfRangeException` from `DateTime.AddDays`
telling the operator nothing about which rule they broke. One `EnsureValidityDaysInRange`
serves the constructor and `Update`, so the two entry points cannot disagree, and both run
it before assigning any field, so a refused window leaves the aggregate intact. The form
clamps to the same constants, read from the domain rather than restated.

Tests: four over-ceiling values on `Update` (3651, 500,000, 2,900,000, `int.MaxValue`),
four out-of-range values on the constructor, the untouched-aggregate guarantee, a use-case
test proving nothing is written on a refused window, and a negative control that the
ceiling accepts the form's own 365-day maximum.

### C6c - the status set is stated once

`CanConvertToSale`, `CanEdit` and `CanCancel` each restated a status set by hand, alongside
`Quote.CanEdit` in the domain. `IsOpenProposal` centralises it. A `Theory` contrasts all
three against the aggregate for every `QuoteStatus`, so a new member cannot leave an
affordance behind. `CanCancel` legitimately differs from `CanEdit` on `Expired` - retiring
a lapsed quote is how the shop cancels one - and the test says so rather than papering over it.

## Batch 1 assertion reviewed independently

Batch 1 changed `ExecuteAsync_WhenQuoteIsConverted_RejectsBeforeAnyWrite` from
`QuoteNotEditableException` to `QuoteAlreadyConvertedException`. **Reviewed, not trusted,
and the change is correct.** Evidence:

1. **It was required, not cosmetic.** `QuoteAlreadyConvertedException` derives directly from
   `DomainException` - it is a sibling of `QuoteNotEditableException`, not a subclass - and
   xUnit's `Assert.Throws<T>` matches the exact type. The old assertion therefore *could not*
   pass against the post-batch-1 code.
2. **The new expectation is the domain's own contract.** `QuoteTests.Update_WhenQuoteIsFrozen_ThrowsAndLeavesTheQuoteUntouched`
   pins `typeof(QuoteAlreadyConvertedException)` for `QuoteStatus.Converted`, and both
   `Quote.Update` and `EnsureEditableAt` throw exactly that. The use case delegates to the
   aggregate, so the two paths agree - which is the point of the change.
3. **The operator-facing behaviour is unchanged.** The view-model renders `ex.Message`, and
   both exceptions carry the same "ya fue convertida a venta" wording. The test still asserts
   that substring.
4. **The guarantee was not weakened.** `Assert.Equal(0, unitOfWork.SaveCount)` survives: the
   test still proves the edit is refused before any write.
5. **No blast radius.** Neither type is caught anywhere in `src/`; both are throw-only.

The reasoning behind it - the old assertion was pinning a duplicated guard, and a test that
protects a duplicate is what let that duplicate drift on the lapsed case - holds up.

## Test assertions changed in batch 2, and why

| Assertion | Was | Now | Why |
|---|---|---|---|
| `Update_WithNonPositiveValidityDays_Throws` | `Assert.Throws<ArgumentOutOfRangeException>` | `QuoteValidityDaysOutOfRangeException` + message + aggregate untouched | the domain now refuses the window itself, and the aggregate being left untouched was never asserted |
| `QuoteHistoryViewModelTests` (3 tests) | `await Task.Delay(50)` | `TaskCompletionSource` from the event handler, 10s timeout backstop | a fixed sleep is a timing bet on an idle runner |
| `QuoteEditingPersistenceTests` raw count | `SELECT COUNT(*) FROM "QUOTE_ITEMS"` | same, `WHERE "QuoteId" = @quoteId` | a whole-table count only passes for the right reason while one quote exists |

No assertion was weakened or removed.

## Final state after batch 2

`dotnet build POS.sln --no-restore` -> 0 errors, 0 warnings (incremental).
`-t:Rebuild` -> 132 warnings, all pre-existing and unrelated to this change:
115 `xUnit1051`, 12 `EF1002`, 3 `CS8604`, 1 `CS8629`, 1 `xUnit1031`, in
`POS.Application.Tests` (130) and `POS.Presentation.Shared.Tests` (2, both in
`InventoryViewModelTests.cs`). Reported, not fixed.

`dotnet test POS.sln --no-restore` -> **1078 passed, 0 failed**
(Domain 186, Presentation.Shared 365, SmokeTests 18, Application 509).
Batch 1 ended at 1043; batch 2 added 35 tests.

## Outstanding Before Archive (revised)

1. **The edit form's total is still frozen, not live** (C2). Making it live needs a by-id
   lookup on `IProductLookupService` and an async `LoadForEdit`. The divergence is surfaced
   rather than hidden, which closes the money hole but leaves the nicer form outstanding.
2. **`QuoteStatus.Active` has no transition.** Unchanged by batch 2. Either the state is
   vestigial and the `CanEdit` branch for it should go, or a transition is missing.
3. **Manual runtime check still not run** - now owing more than before. Batch 2 changed what
   the operator physically touches: "Descartar" has to enable on entering edit mode and
   disable after discarding; a customer picked during an edit has to survive the save; and a
   validity value past the ceiling has to be refused without an opaque error. No headless
   test can prove the WPF interaction. The earlier items still owe: `QuoteNumber` unchanged
   across two edits, the edit affordance gone once converted, the success message carrying
   the total, and a lapsed row showing "Vencida" with the renewal guidance.
4. **`CreateQuoteUseCase` and `CancelQuoteUseCase` are ungated at the use-case level.**
   Pre-existing app-wide pattern, recorded in C1b, needs its own change.

## Visual fixes after batch 2 (`6a6c941`)

Two operator-reported visual defects in the quotes screen, fixed in one commit. Neither
touches the money path or the edit flow.

| # | Defect | Root cause | Fix |
|---|---|---|---|
| V1 | A product with a single presentation still rendered a one-entry dropdown | `AvailablePresentations` always contains the loose unit, so "one presentation" means `Count == 1`; the `ComboBox` was unconditional | `CashierProductSearchResultViewModel.HasMultiplePresentations` (`Count > 1`) drives the `ComboBox` visibility. A product with Unidad + Caja still shows it. Test: `HasMultiplePresentations_IsFalseOnlyWhenTheLooseUnitIsTheSoleOption` |
| V2 | "Aplicar a todas" and "Quitar" drew text on top of each other in the global discount row | both buttons were declared in `Grid.Column="2"` of `QuoteView.xaml` | "Quitar" moved to `Grid.Column="3"`, which already existed as `Auto` and was empty |

`dotnet build src/POS.Presentation` -> 0 errors. V1 test: 2/2 passing in
`CashierProductSearchResultViewModelTests`. V2 is XAML layout only and has no headless
test; it is covered by the manual runtime check below.

**Review status:** low risk (+17 lines, one XAML attribute each, no domain or persistence
change), so no lens fan-out. The native review status returned `managed_assets_outdated`
(a `gentle-ai sync` is owed), so no receipt was issued for this commit.

**Added to "Outstanding Before Archive":** the manual runtime check also owes confirming
that a single-presentation product shows no dropdown and that the global discount row
shows both buttons side by side. Whether "Quitar" should hide when no discount is applied
was left as is (it is always visible, enabled only when a discount exists).
