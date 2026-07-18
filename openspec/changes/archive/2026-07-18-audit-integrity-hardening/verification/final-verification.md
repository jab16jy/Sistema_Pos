# 7.3 Full Build, Tests, Format, and Migration Smoke

Date: 2026-07-17
Machine: Windows 11 Pro, real WindowsDesktop SDK available (no platform limitations
on this run).

## Commands run and results

### dotnet test POS.sln --no-restore

PASS. 761/761 total (up from the 760 baseline re-verified at the start of this
session, plus 1 new test added in this packet, see Migration smoke section):

| Project | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| POS.Domain.Tests | 101 | 0 | 0 | 101 |
| POS.Presentation.Shared.Tests | 249 | 0 | 0 | 249 |
| POS.Presentation.SmokeTests | 10 | 0 | 0 | 10 |
| POS.Application.Tests | 401 | 0 | 0 | 401 |

### dotnet build POS.sln --no-restore

PASS. 0 warnings, 0 errors.

### dotnet format POS.sln --verify-no-changes

FAIL (exit code 2). Four pre-existing files carry real trailing/indentation
WHITESPACE violations, unrelated to this verification packet's own new file.

| File | Lines | Nature |
|---|---|---|
| src/POS.Domain/Entities/InventoryMovement.cs | 36, 37 | Trailing whitespace after the newReason ternary lines (packet 1.1's UpdateReason method) |
| src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs | 282 | Trailing whitespace on a blank line inside LoadShiftsAsync (packet 1.5.2/3.6 area) |
| tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs | 417 | Trailing whitespace on a blank line before var sale equals... |
| tests/POS.Presentation.Shared.Tests/InvoicesViewModelTests.cs | 158-175 | An 18-line block (var session = new FakeSession(); ... closing brace) is over-indented by 4 spaces relative to its surrounding scope (packet 1.1.1's return-consumer handoff test) |

All four are pure whitespace/indentation drift with zero semantic content: no
logic, no test assertions, no behavior affected. Verified via dotnet format
--verify-no-changes --verbosity diagnostic: the diagnostic output lists these as
error WHITESPACE (an exact-match formatting diff dotnet format can apply
automatically), not an analyzer/code-quality warning. The xUnit1051/xUnit1031
analyzer warnings also present in the output are pre-existing test-only style
suggestions (not build-breaking, not WHITESPACE-category, unrelated to this
finding) and do not affect the exit code by themselves. Confirmed these four
WHITESPACE errors were already present before this packet's new test file was
added, so they are not something this verification session introduced.

Per this packet's explicit constraint (no product-code edits unless a failed
check is assigned to a new bounded packet, and if so, stop and report instead of
implementing it yourself), these four files are not edited here. They are all
owned by already-applied, already-merged packets (1.1, 1.1.1, and the 1.5.2/3.6
shift area) and fall outside packet 7.3's exclusive-files list, which is the
verification document plus tasks.md only.

New packet proposed to close this gap:

- Name: 7.4 Whitespace formatting cleanup
- Depends on: 7.3 (this report)
- Exclusive files:
  - src/POS.Domain/Entities/InventoryMovement.cs
  - src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs
  - tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs
  - tests/POS.Presentation.Shared.Tests/InvoicesViewModelTests.cs
- Implement: run dotnet format POS.sln (without --verify-no-changes) to apply the
  automatic whitespace/indentation fix to exactly these four files, or hand-fix
  the specific lines listed above. No logic change; do not touch any other file.
- Tests: dotnet format POS.sln --verify-no-changes exits 0; re-run
  dotnet test POS.sln --no-restore to confirm the untouched-logic assumption
  (expect the same 761/761 total).
- Focused command: dotnet format POS.sln --verify-no-changes

This is a non-blocking, low-risk, mechanical finding. It does not affect any
financial, authorization, audit, or migration behavior verified in 7.1/7.2. It is
reported here rather than fixed inline because 7.3's own instructions require
that.

### Copied-valid-legacy SQLite migration smoke

Evaluated the two existing options per this packet's instructions.

1. Existing coverage (tests/POS.Application.Tests/Persistence/IdentityReceivableMigrationTests.cs
   and InventoryReturnQuoteMigrationTests.cs, both already part of the 401-test
   Application suite verified above): these migrate a fresh, empty in-memory
   SQLite database through the entire migration chain via Database.MigrateAsync,
   then seed data with the live EF model and assert orphan rejection, valid-data
   survival, and (in InventoryReturnQuoteMigrationTests.ValidLegacyData_MigratesUnchanged_AndAllowsValidWrites)
   a passing IntegrityPreflight.CheckAsync post-migration. This proves the
   migration chain and preflight gate are individually correct, but does not
   exercise the literal "operator copies a pre-existing legacy database file, then
   migrates the copy" scenario from design.md's migration sequence (section
   "Migration sequence and rollback", step 5). Judged as not fully proving the
   end-to-end chain as written.

2. New consolidated smoke test (added in this packet, since the judgment call
   favored closing the literal gap):
   tests/POS.Application.Tests/Persistence/CopiedLegacyDatabaseMigrationSmokeTests.cs,
   test method CopiedLegacyDatabaseFile_MigratesCleanly_AndPassesPostMigrationPreflight.
   This test:
   - Creates a real on-disk SQLite file and migrates it only up to
     20260715031926_MakeSaleReturnIndexNonUnique (the last migration before this
     change's own migrations begin), using IMigrator.MigrateAsync(targetMigration).
   - Seeds representative legacy rows (a role, a user/cashier, a customer, a sale,
     a sale return with no OperationId, and a receivable) via raw SQL matching the
     exact pre-cutoff schema, verified column-by-column against sqlite_master for
     Roles, Users, Customers, SALES, SALE_RETURNS, and RECEIVABLES at that
     migration state. The live EF model cannot write a genuinely older row shape,
     since it always assumes the latest columns exist.
   - Physically copies the file with File.Copy, exactly as an operator would.
   - Runs IntegrityPreflight.CheckAsync against the copy (passes, matching
     DatabaseSchemaRollout's pre-migration gate).
   - Runs Database.MigrateAsync on the copy, applying every remaining
     audit-hardening migration (AddReturnResolution through AddQuoteSequence).
   - Confirms the legacy sale, sale return (still OperationId is null), and
     receivable are all readable unchanged through the current (post-hardening)
     entity model.
   - Runs IntegrityPreflight.CheckAsync again post-migration (passes).

   Result: PASS (1/1). This is the one new test contributing to the 401 total
   above (400 to 401 in POS.Application.Tests).

Both together satisfy this packet's exit criterion: the existing tests prove
orphan rejection and preflight-gate correctness across the full chain; the new
test proves the specific "copy a legacy file, migrate the copy" operational
scenario end to end.

### Windows-only POS.Presentation.SmokeTests

Already included in and covered by the dotnet test POS.sln run above (10/10
passed). This machine has the WindowsDesktop SDK, so the suite ran natively, not
skipped. No separate re-run needed; also independently re-verified during 7.1
with identical results.

## Backup/restore verification status

Not exercised by an automated test in this repository (no backup/restore
automation exists in the codebase to invoke). design.md's migration sequence step
1 ("create and restore-verify a database backup") and the proposal's Migration
and Rollback Policy item 1 describe this as an operator procedure performed
before running a schema-changing migration in production, not a piece of
application code with its own test surface. IntegrityPreflight and the migration
smoke test above verify the pieces that ARE application code (the preflight gate
and the migration application itself). Recorded here as an explicit non-blocking
scope note, not a finding: backup/restore is out of this change's implementation
scope per proposal.md's "Out of Scope" section (no mention of a backup automation
feature), so there is nothing further to verify at this layer.

## Platform limitations

None. This is a real Windows 11 Pro machine with the WindowsDesktop SDK
available; every check in this packet's instructions ran natively, including
both POS.Presentation.Shared.Tests and POS.Presentation.SmokeTests.

## Deferred non-blocking observations

- InventoryViewModel.OperatorUserId (src/POS.Presentation.Shared/ViewModels/InventoryViewModel.cs:286)
  is a dead/unused property, already noted in 7.1 and 7.2 reports; cosmetic only,
  zero functional risk, no packet assigned (too trivial to warrant one; fold into
  any future edit that touches that file).
- The four-file whitespace formatting gap above, assigned to proposed packet 7.4
  as required by this packet's own constraints; blocks a clean
  dotnet format --verify-no-changes run but does not block build, tests, or any
  behavior verified in 7.1/7.2.

## Exit criterion

NOT fully met. dotnet test POS.sln, dotnet build POS.sln, the copied-legacy
migration smoke, and the Windows-only smoke suite are all green. dotnet format
POS.sln --verify-no-changes fails due to pre-existing whitespace drift in four
files outside this packet's ownership. Per this packet's explicit instructions,
that failure is assigned to a new bounded packet (7.4, above) rather than fixed
here, and 7.3 is therefore NOT marked done in tasks.md pending that packet.
