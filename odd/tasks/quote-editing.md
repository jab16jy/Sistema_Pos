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
