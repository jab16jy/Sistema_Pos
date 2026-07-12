# Proposal: Complete Supplier Purchasing Refactor

## Intent

Complete the supplier-facing purchase workflow so operators maintain supplier details and a supplier-specific purchasing catalog, can see supplier availability from products without editing it there, and can register purchases only from the selected supplier's active catalog.

## Current-State Evidence

- `ProductSupplier` is only a `ProductId`/`SupplierId` pair. It cannot store a supplier reference, purchase unit, conversion, current cost, or active state.
- `SaveSupplierUseCase` supports an update request, but the current purchase ViewModel always sends `Id: null`; the WPF screen therefore exposes creation, not supplier editing or a supplier profile catalog.
- `RegisterPurchaseUseCase` accepts any product identifier and caller-provided total cost. It does not require an active `ProductSupplier` entry, so the UI can register an arbitrary product against a supplier.
- The current purchase entry lets the operator select a general product lookup result or type a product identifier. It has no supplier-catalog source of truth and no responsive supplier-profile layout.
- Current EF Core mappings and migrations persist the minimal pair and the legacy purchase shape. Existing data must remain readable and retain its original meaning.

## Scope

### In Scope

1. **Supplier edit and supplier profile catalog**
   - Let an operator select an existing supplier, load its details, edit its contact/active fields, and save the selected supplier rather than creating a duplicate.
   - Add a supplier-profile catalog for managing that supplier's product relationships. Each relationship will hold the supplier reference/SKU (optional), purchase-unit label, positive conversion to inventory units, positive current purchase-unit cost, and active state.
   - Enforce one product relationship per supplier and validate catalog values in Domain/Application code.

2. **Read-only supplier visibility from products**
   - Show the supplier relationship summary for a product in the product experience as read-only information.
   - Keep catalog creation, edits, activation, and cost/unit maintenance in the supplier profile. Product screens must not become a second catalog editor.

3. **Valid catalog-driven purchase entry**
   - A selected active supplier determines the available purchase lines: only that supplier's active catalog items may be added.
   - The entry flow uses catalog snapshots for product name/reference, purchase unit, conversion, and unit cost. Operators may enter an allowed quantity, but cannot submit an arbitrary product ID or a client-invented cost outside the catalog contract.
   - Application use cases must revalidate supplier activity, catalog membership/activity, positive quantities, and product eligibility; UI filtering is not authorization or business validation.
   - Purchase persistence, stock receipt, inventory movement, and any resulting product-cost update must remain atomic. A failed validation or persistence operation must leave no partial receipt, movement, stock, or cost change.

4. **Responsive WPF layout**
   - Restructure the supplier profile and purchase entry around adaptable grids/panels, sensible minimum widths, wrapping/scrolling where needed, and readable narrow-window behavior.
   - Preserve keyboard-friendly selection and clear inline feedback for unavailable suppliers, empty catalogs, invalid quantities, and save failures.

5. **Legacy migration compatibility**
   - Introduce additive, data-compatible persistence changes for catalog metadata and the purchase data required by the validated flow.
   - Preserve existing `PRODUCT_SUPPLIERS`, suppliers, purchases, purchase lines, stock, and inventory-history values exactly as recorded. Legacy pair rows must not be guessed to have a unit, conversion, cost, or active catalog status; they require explicit operator completion before they qualify for new catalog-driven purchases.
   - Validate the migration against representative SQLite databases containing legacy supplier/product relationships and purchase history before release.

6. **Concrete verification**
   - Add focused Domain/Application tests for catalog invariants, supplier update selection, server-side catalog validation, and no-partial-write behavior.
   - Add Infrastructure/SQLite migration coverage proving legacy rows survive and are not silently reinterpreted.
   - Add Presentation.Shared ViewModel tests for supplier edit, catalog-driven selection, and unavailable/invalid states; run the Windows WPF smoke path for the responsive view composition.

### Out of Scope

- Building new supplier-quotation, accounts-payable, invoice/payment-settlement, returns, or credit-note functionality. (Supplier quotations already shipped to `main` via a parallel change before this one merged — see Reconciliation Note below. This proposal does not add or modify that feature, only coexists with it.)
- Retrospective recalculation of historic purchases.
- Editing supplier catalog relationships from product screens.
- Reinterpreting legacy unit quantities or historical costs to synthesize new catalog values.
- Changes to customer quotations, sales, or unrelated inventory workflows.

## Architecture and Boundaries

| Layer | Responsibility |
| --- | --- |
| Domain | `ProductSupplier` invariants and purchase/catalog value rules. |
| Application | Supplier-profile, catalog, product-visibility, and purchase-entry use cases; server-side validation and transactional orchestration ports. |
| Infrastructure | EF Core configurations, repositories/query services, SQLite transactions, indexes, and additive migration behavior. |
| Presentation.Shared / WPF | ViewModels, commands, bindings, responsive layout, and user feedback only. |

WPF **MUST NOT** access SQLite directly and **MUST NOT** own business rules such as catalog eligibility, conversion validation, costs, stock changes, or transaction boundaries. The Presentation layer invokes Application use cases; Infrastructure owns EF Core and SQLite.

## Expected File Areas

- `src/POS.Domain/Entities/ProductSupplier.cs` and related Domain interfaces/value types
- `src/POS.Application/DTOs/Purchases/`, `Interfaces/`, and `UseCases/Purchases/`
- Product query DTOs/use cases needed for read-only supplier visibility
- `src/POS.Infrastructure/Persistence/Configurations/`, `Repositories/`, `Queries/`, `AppDbContext.cs`, DI registration, and a new EF Core migration/snapshot update
- `src/POS.Presentation.Shared/ViewModels/PurchasesViewModel.cs` and associated presentation tests
- `src/POS.Presentation/Views/PurchasesView.xaml` (and product view only for read-only visibility)
- Domain, Application, Infrastructure/SQLite, Presentation.Shared, and Windows smoke-test projects

## Reconciliation Note

Commit `0c73d1a` (merged to `main` as PR #111, "auditable supplier catalog flow with weighted cost, VAT, quotations, and history") landed before this change's implementation was rebased. It shipped a broader, independently-designed version of the same underlying problem — weighted-average cost, VAT, supplier quotations, and purchase-history search — with its own migration (`20260712014458_SupplierPurchasingRefactor`).

This change's implementation was rebased onto that merged `main` and reconciled rather than replacing it: `main`'s quotation-acceptance flow, weighted-cost/VAT logic, and purchase-history search remain fully wired (DI, use cases, and the PurchasesView History/Quotation tabs), and this proposal's catalog-edit scope (supplier profile catalog editing, product-screen read-only visibility, catalog-driven purchase-line validation) was layered on top in the same transaction path — `RegisterPurchaseUseCase` now enforces both the quotation-acceptance check and the catalog eligibility guard together. The original migration this branch authored (`20260712023023_SupplierPurchasingRefactor`, written against the pre-#111 schema) was discarded and replaced with a small incremental migration (`20260712034743_SupplierCatalogEditSupport`) on top of `main`'s already-shipped schema.

## Risks and Rollback

| Risk | Mitigation | Rollback |
| --- | --- | --- |
| Legacy data is changed or misclassified by migration | Use additive schema changes; leave incomplete legacy pairs ineligible until explicitly completed; test production-like SQLite copies. | Take a verified database backup before deployment. If new operational data exists, apply a forward corrective migration or restore the verified backup; do not use a destructive downgrade. |
| A catalog bypass creates an invalid receipt | Revalidate the selected supplier and every catalog line in Application inside the transaction. | Disable the new entry path and retain the backed-up database until a corrected build/migration is available. |
| Partial stock or cost updates | Persist purchase, inventory movements, stock, and cost changes in one Infrastructure-managed transaction. | Roll back the transaction on failure; use a forward correction for any post-commit operational error. |
| Responsive changes hide essential actions at narrow widths | Exercise narrow and normal WPF window sizes in the Windows smoke/manual verification path. | Revert presentation-only layout changes independently if necessary. |

## Acceptance Criteria

- An existing supplier can be selected, edited, and saved without creating a second supplier.
- A supplier profile can create and maintain valid product catalog entries, and a product shows the related suppliers read-only.
- A purchase cannot be submitted for an inactive supplier, an absent/inactive catalog entry, an invalid quantity, or a product outside the selected supplier catalog, even if the request bypasses WPF.
- A successful purchase records the catalog snapshots used and applies stock/cost effects atomically; a failure leaves no partial effects.
- Legacy records retain their original stored values and incomplete legacy supplier-product pairs do not become silently valid catalog entries.
- The WPF supplier/purchase experience remains usable at supported narrow and normal window widths, and WPF has no direct SQLite access or business-rule implementation.
- Verification includes focused unit/integration/presentation coverage plus the configured Windows WPF smoke test; the full repository commands remain `dotnet test POS.sln --no-restore`, `dotnet build POS.sln --no-restore`, and `dotnet format POS.sln --verify-no-changes` when the later verification phase is authorized.
