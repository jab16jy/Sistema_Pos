# Tasks: Complete Supplier Purchasing Refactor

## Review Workload Forecast

| Field | Value |
| --- | --- |
| Estimated changed lines | ~1,200 source and tests, plus migration and SDD artifacts |
| 400-line budget risk | High |
| Delivery strategy | Coherent feature branch; preserve reviewable work units for domain/application, persistence, and presentation/tests |
| Rationale | A usable catalog-driven receipt requires all Clean Architecture layers, migration compatibility, and WPF bindings. |

## Phase 1: Catalog and Receipt Behavior

- [x] 1.1 Expand `ProductSupplier` with validated catalog metadata, active/incomplete eligibility, and non-destructive update/deactivation. [Req: Maintain supplier purchasing catalog]
- [x] 1.2 Add catalog DTOs, repository/query ports, and use cases for save/list and read-only product supplier visibility. [Req: Maintain supplier purchasing catalog] [Req: Show product supplier relationships read-only]
- [x] 1.3 Rework receipt DTO/domain/application flow to use catalog snapshots, explicit line cost override, receipt arithmetic, VAT, and weighted cost. [Req: Build purchase drafts] [Req: Confirm catalog-backed purchases] [Req: Preserve VAT]
- [x] 1.4 Revalidate active supplier, product, catalog membership/activity/configuration, quantity, cost, and factor inside purchase confirmation. [Req: Reject catalog bypass]

## Phase 2: Persistence and Compatibility

- [x] 2.1 Map catalog/receipt fields, repositories, queries, DI, and transaction behavior in Infrastructure. [Req: Confirm catalog-backed purchases]
- [x] 2.2 Add an additive EF Core migration and snapshot update that preserves legacy relationship and historic purchase data without inferred catalog values. [Req: Preserve VAT] [Req: Maintain supplier purchasing catalog]
- [x] 2.3 Add migration regression coverage using legacy SQLite data. [Req: Display incomplete legacy relationship]

## Phase 3: Supplier and Purchase Experience

- [x] 3.1 Add supplier selection, `EditingSupplierId`, New/Save/Cancel edit commands, field loading, and success/error feedback. [Req: Edit supplier records]
- [x] 3.2 Add supplier profile catalog list/editor, status, product selection, and activation/deactivation commands. [Req: Maintain supplier purchasing catalog]
- [x] 3.3 Add read-only supplier summary to product detail with optional navigation to supplier catalog. [Req: Show product supplier relationships read-only]
- [x] 3.4 Replace arbitrary purchase product entry with selected-supplier eligible catalog options and catalog-backed line displays. [Req: Build purchase drafts]
- [x] 3.5 Restructure WPF layout with distinct grid cells, scrolling long forms, bounded grids, and visible action groups. [Req: Keep WPF layout responsive]

## Phase 4: Verification

- [x] 4.1 Add Domain/Application tests for supplier update, catalog create/update/deactivate, invalid metadata, receipt validation, calculations, VAT, and legacy ineligibility. [Req: All]
- [x] 4.2 Add Shared ViewModel tests for edit/cancel, catalog workflow, eligible filtering, and purchase default snapshots. [Req: Edit supplier records] [Req: Build purchase drafts]
- [x] 4.3 Run `dotnet test POS.sln --no-restore`, `dotnet build POS.sln --no-restore`, `dotnet format POS.sln --verify-no-changes`, and `git diff --check`. Formatting verification is blocked only by pre-existing trailing whitespace in `ProfitQueryService.cs:92`. [Req: All]
- [ ] 4.4 Run the Windows WPF smoke project and record manual layout results for new/edit supplier, empty/long catalog, narrow window, and multi-line purchase. [Req: Keep WPF layout responsive]
