# Verification: fix-loyalty-accounting-and-tier-foreground

**Status:** Verified with one pre-existing repository formatting limitation.

## Scope reviewed

- Read the exploration, proposal, delta specifications, design, tasks, and `openspec/config.yaml`.
- Reviewed the implementation against baseline `2d7f826` and the Clean Architecture dependency direction.
- Preserved the unrelated pre-existing `src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs` change without editing it.

## Requirement results

| Area | Result | Evidence |
| --- | --- | --- |
| Net sale earning and zero-point protection | Pass | `Sale.EffectiveEarnableTotal()` is the sole sale completion input; accumulation applies the pre-sale tier then exits before account/ledger creation on zero. |
| Immutable ledger and metrics | Pass | Typed signed events, source links, `Earned`-only issuance, `Redeemed`-only count, and separate history labels are implemented and tested. |
| Cancellation/full-return compensation | Pass | The compensator stages exact source-based corrections before the callers' one existing save; source-link uniqueness protects retries/concurrency. |
| Partial-return allocation and idempotency | Pass after fix | Allocation uses snapshots, quantities, original tender, money rounding, and original rows only. Duplicate requested sale-item lines could over-allocate points/stock; validation now rejects them before mutation and the compensator also rejects direct duplicate input. |
| Atomicity and boundaries | Pass | Sale/return/inventory/ledger staging shares the callers' single `IUnitOfWork.SaveChangesAsync`; Domain remains free of EF/WPF types and Presentation maps the foreground enum to brushes. |
| Additive migration | Pass | The migration only adds nullable compensation metadata, a restrictive self-reference, unique link index, and non-null foreground default. A migration-from-existing-tier test confirms `White` is populated. No repair/backfill or settlement change is present. |
| Tier foreground and binding | Pass | The closed `Black`/`White` enum is carried through DTOs, persistence, editor selection/preview, and every Customers badge foreground element. Cashier has no styled tier badge, so no unrelated text is styled. |
| Multiplier parsing | Pass | Invalid/blank/non-positive text clears the pending value and blocks save; dot/comma positive decimals remain accepted. |

## Verification fixes

- Rejected duplicate sale-item entries in partial-return requests before the sale status, inventory, return, or unit of work is mutated.
- Added the same defensive validation to the public compensator so non-UI callers cannot over-allocate compensation.
- Added regression coverage for both paths and a migration upgrade test for an existing tier.
- Rewrote the newly generated migration without a BOM/CRLF so it passes targeted formatter verification.

## Commands

| Command | Result |
| --- | --- |
| `dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore` | Pass: 82 |
| `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore` | Pass: 278 |
| `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore` | Pass: 221 |
| `dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore` | Pass: 5 |
| `dotnet build POS.sln --no-restore` | Pass: 0 errors; one pre-existing obsolete-constructor warning in `PurchasesViewModelTests.cs` |
| `dotnet test POS.sln --no-restore` | Pass: 586 total |
| `dotnet format POS.sln --verify-no-changes --include <touched verify files>` | Pass |
| `git diff --check` | Pass |

## Limitation

The full `dotnet format POS.sln --verify-no-changes` remains blocked by pre-existing whitespace/encoding violations in unrelated `ProfitQueryService.cs`, `ReportExportService.cs`, `InvoicesViewModel.cs`, and earlier migrations. The new migration is clean under targeted formatter verification. No deployment blocker was found for this change; deploy the additive migration before binaries that write compensation rows or tier foregrounds. Once compensation exists, rollback must be application-only or separately audited and must not delete ledger evidence.
