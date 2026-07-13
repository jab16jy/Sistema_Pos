# Tasks: Correct Loyalty Accounting and Tier Foreground

## Review Workload Forecast

| Slice | Scope | Estimated changed lines | Budget status |
|---|---|---:|---|
| 1 | Domain model, contracts, and unit tests | 250-350 | Under 400 |
| 2 | Net sale earnings and multiplier-floor behavior | 220-320 | Under 400 |
| 3 | Cancellation and full-return compensation | 280-380 | Under 400 |
| 4 | Partial-return allocation and failure behavior | 300-390 | Under 400 |
| 5 | EF persistence, migration, and repository metrics | 280-380 | Under 400 |
| 6 | Shared presentation state and WPF bindings | 300-390 | Under 400 |

Implementation MUST keep these as separate reviewable work units. If a slice exceeds 400 changed lines, split its tests or UI bindings into a follow-up slice before review.

## Phase 1: Domain Model and Contracts

- [x] 1.1 **RED** — Add Domain tests for loyalty-points tender aggregation, non-negative effective earnable total, all four transaction type/sign rules, valid and invalid compensation source pairs, and the tier foreground default/closed vocabulary. [Req: loyalty-points; loyalty-tier-administration]
- [x] 1.2 **GREEN** — Add the `LoyaltyTierTextForeground` enum; extend `LoyaltyTier` with validated `TextForeground` defaulting to `White`; add named `Sale` loyalty-tender and effective-earnable-total calculations; extend loyalty transaction types and enforce non-zero sign/link/source-pair invariants without changing historical rows. [Req: loyalty-points; loyalty-tier-administration]
- [x] 1.3 **GREEN** — Add the sale-scoped loyalty-transaction read contract to `ILoyaltyRepository` and update every affected in-memory fake/test double to compile against it. [Req: loyalty-points]
- [x] 1.4 Run `dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore` and the directly affected Application test project while preserving the Domain-to-outer-layer dependency boundary. [Req: Domain invariants]

## Phase 2: Net Earnings and Zero-Point Protection

- [x] 2.1 **RED** — Add Application tests proving that a same-sale loyalty tender is excluded from the earning base and that a positive base which floors to zero after the pre-sale tier multiplier creates neither an account nor an `Earned` row. Cover a non-zero floored result as a control. [Req: loyalty-points]
- [x] 2.2 **GREEN** — Update sale completion to pass `Sale.EffectiveEarnableTotal()` to loyalty accumulation while retaining its existing single unit-of-work save. [Req: loyalty-points]
- [x] 2.3 **GREEN** — Reorder loyalty accumulation to calculate base points, resolve the pre-sale tier, apply/floor the multiplier, and return without creating an account or transaction when the resulting points are zero. [Req: loyalty-points]
- [x] 2.4 Run `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore` for the earning and accumulation tests. [Req: Net earnings; zero-point protection]

## Phase 3: Exact Sale Compensation

- [x] 3.1 **RED** — Add Application tests for cancellation and full return: each original sale-linked `Earned`/`Redeemed` row receives exactly one inverse typed compensation using its recorded points; current rules or tiers cannot affect the result; existing compensations stage no duplicate rows. [Req: loyalty-points; invoice-management]
- [x] 3.2 **GREEN** — Implement the Application-only `CompensateSaleLoyaltyUseCase` to read only original sale-linked earned/redemption rows, skip already-linked sources, create compatible immutable compensations with source sale and reason, and never call `SaveChangesAsync`. [Req: loyalty-points]
- [x] 3.3 **GREEN** — Invoke the compensator from cancellation and full-return paths after existing validation/staging and before their existing single save; retain sale status, inventory, return, and ledger atomicity. [Req: loyalty-points; invoice-management]
- [x] 3.4 **RED/GREEN** — Add and implement a negative-balance regression: valid compensation may create a deficit, while the existing redemption flow rejects any later requested redemption exceeding that computed balance. [Req: loyalty-points]
- [x] 3.5 Run the focused invoice and loyalty Application tests with `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`. [Req: Exact compensation; negative balance]

## Phase 4: Partial-Return Allocation

- [ ] 4.1 **RED** — Add Application tests for quantity-proportional line-discount allocation, proportionally allocated loyalty tender, money rounding away from zero, floored/capped point allocations, zero denominators, and omission of zero-point compensations. [Req: loyalty-points]
- [ ] 4.2 **RED** — Add regressions showing that changing `RefundAmount`, current loyalty rules, or current tier multipliers does not change partial-return compensation; selecting all original quantities takes the exact full-return branch; and the existing one-return-per-sale guard rejects a second event. [Req: loyalty-points; invoice-management]
- [x] 4.3 **GREEN** — Extend the compensator and return use case to derive partial-return amounts exclusively from immutable original sale items, sale totals, loyalty tender, source transactions, and selected original quantities. Do not change return request types, refund settlement, refund method selection, or return UI workflow. [Req: loyalty-points; invoice-management]
- [ ] 4.4 **RED/GREEN** — Verify failed loyalty or return persistence leaves the original sale and ledger unchanged and commits no return, inventory, or compensation rows. [Req: invoice-management]
- [x] 4.5 Run `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore` for invoice and loyalty allocation coverage. [Req: Partial-return allocation; atomicity]

## Phase 5: Persistence, Migration, and Program History

- [x] 5.1 **RED** — Add SQLite-backed repository/schema tests for nullable historical compensation links, one unique non-null compensation link per source, signed balance including compensations, `Earned`-only issued-points totals, `Redeemed`-only redemption counts, and separate history classifications for all four transaction types. [Req: loyalty-points]
- [x] 5.2 **GREEN** — Implement the repository sale-scoped lookup, compensation-aware history mapping, and type-filtered metrics while preserving all signed rows in balance calculations and avoiding historical repair/backfill. [Req: loyalty-points]
- [x] 5.3 **GREEN** — Configure the optional restrictive self-reference and unique `CompensatesTransactionId` index for loyalty transactions, and configure the non-null tier `TextForeground` column with `White` default. [Req: loyalty-points; loyalty-tier-administration]
- [x] 5.4 Generate the additive EF Core migration named `AddLoyaltyCompensationAndTierTextForeground` (target `src/POS.Infrastructure/POS.Infrastructure.csproj`, using the repository's established startup-project command); include the generated designer and `AppDbContextModelSnapshot` update. Do not edit historical ledger data or generate backfill SQL. [Req: loyalty-points; loyalty-tier-administration]
- [ ] 5.5 **GREEN** — Extend migration rollout coverage to confirm existing tiers load as `White`, existing ledger rows remain unchanged, and no compensation is inferred for pre-rollout cancellations/returns. [Req: loyalty-points; loyalty-tier-administration]
- [x] 5.6 Run `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore` for repository and schema-rollout coverage. [Req: Persistence and migration]

## Phase 6: Tier Editor and Badge Foregrounds

- [x] 6.1 **RED** — Add shared-presentation tests for foreground default/load/create/update/save mapping, two-option foreground selection, foreground-aware preview, customer/cashier badge state, and invalid/blank/zero/malformed multiplier text after a previously valid value. Verify both `1.5` and `1,5` remain valid. [Req: loyalty-tier-administration; customer-tier-badges]
- [x] 6.2 **GREEN** — Carry `TextForeground` through tier DTOs/results/mappers and presentation records; expose only black/white selections, initialize new tiers to white, and map the selected domain value to presentation brushes outside Domain. [Req: loyalty-tier-administration]
- [x] 6.3 **GREEN** — Make multiplier text retain explicit parse validity and a pending parsed value; block save with a recoverable validation message when invalid, without invoking create/update or reusing a prior numeric multiplier. [Req: loyalty-tier-administration]
- [x] 6.4 **GREEN** — Update the Loyalty WPF editor with the two-option selector and combined background/foreground preview. Bind the configured foreground to every customer tier-badge icon/text element and, only when Cashier renders a styled tier badge, to every foreground-bearing badge element; leave unrelated Cashier text and loyalty-discount rows unchanged. [Req: loyalty-tier-administration; customer-tier-badges]
- [x] 6.5 Run `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`. On Windows, run `dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore` to cover WPF composition/startup. [Req: Tier editor and badge bindings]

## Phase 7: Final Verification and Rollout Readiness

- [ ] 7.1 Inspect the generated migration and snapshot for additive-only schema changes: nullable compensation metadata, restrictive self-reference, unique non-null link protection, and non-null `White` tier foreground default. Confirm no historical compensation/backfill or refund-settlement change is present. [Req: Additive migration; non-goals]
- [ ] 7.2 Run focused Domain, Application, and Presentation.Shared suites again after all slices: `dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore`, `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`, and `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`. [Req: Focused verification]
- [ ] 7.3 Run `dotnet format POS.sln --verify-no-changes`, `dotnet build POS.sln --no-restore`, and `dotnet test POS.sln --no-restore`. Execute the presentation smoke suite on Windows; if verification runs on Linux, document the WPF WindowsDesktop limitation and verify the supported non-WPF/shared projects. [Req: Full verification]
- [ ] 7.4 Record deployment/rollback guidance: deploy the additive migration before binaries that write compensations/foregrounds; after a compensation is written, rollback MUST be application-only or use a separately audited data migration and MUST NOT delete ledger evidence. [Req: Rollback]
