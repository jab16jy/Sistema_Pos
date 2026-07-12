# Design: Complete Supplier Purchasing Refactor

## Technical Approach

The supplier profile is the single editing surface for supplier identity and supplier-product catalog metadata. The product module queries that metadata only for visibility. New purchases consume immutable draft snapshots from the selected supplier's eligible catalog; the Application layer revalidates them before passing a receipt to the Domain/Infrastructure transaction.

## Architecture Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Catalog identity | Composite supplier/product key with metadata on `ProductSupplier`. | One product can be supplied by multiple suppliers, while each supplier-product relationship is unique. |
| Legacy rows | Nullable/empty metadata plus inactive/incomplete eligibility. | A migration must never invent a unit, factor, or cost from historic data. |
| Catalog validation | Domain entity and Application use case, revalidated at receipt confirmation. | WPF filtering improves usability but cannot protect business integrity. |
| Purchase pricing | Catalog defaults become editable line snapshots. | A negotiated invoice price must be auditable without rewriting future catalog pricing. |
| Inventory receipt | `quantity × factor`, confirmed in the transaction. | Keeps purchasing units separate from inventory units. |
| Cost policy | Weighted average only on confirmed purchase. | Keeps `Product.CostPrice` meaningful while historic lines retain their actual values. |
| Product module boundary | Read-only supplier summary. | Prevents duplicated catalog-editing rules and screens. |
| Presentation layout | Parent grid separates profile, catalog, purchase draft, totals, and actions; long sections scroll internally. | Prevents controls sharing the same layout cell and hiding actions at reduced sizes. |

## Data Flow

```text
WPF Purchases view
  -> PurchasesViewModel
  -> supplier/profile/catalog Application use cases
  -> domain validation + repository ports
  -> EF Core / SQLite

WPF New purchase
  -> eligible catalog query
  -> catalog-backed line snapshot
  -> RegisterPurchaseUseCase revalidation
  -> purchase + stock movement + weighted cost in one transaction
```

## Migration Strategy

The migration is additive for catalog metadata and purchase receipt data. Existing `PRODUCT_SUPPLIERS` rows retain their original keys and receive neither invented values nor active eligibility. They surface as incomplete until an operator configures them. Existing purchase and purchase-item data remains untouched; new receipt fields only describe future confirmed purchases. Migration verification uses a representative SQLite database with legacy supplier pairs and historical purchases.

## File Changes

| Area | Action | Description |
| --- | --- | --- |
| Domain | Modify/add | Catalog metadata/invariants, receipt snapshots, VAT totals, and weighted-cost behavior. |
| Application | Modify/add | Catalog DTOs, profile/catalog and product-visibility queries, server-side receipt validation and orchestration. |
| Infrastructure | Modify/add | EF mappings, repositories/query services, DI, transactional UoW support, additive migration and snapshot. |
| Presentation.Shared | Modify | Supplier editing state, catalog state, catalog-backed purchase lines, clear messages, and commands. |
| WPF | Modify | Structured responsive grids, bounded data grids, scrolling forms, and read-only product supplier visibility. |
| Tests | Modify/add | Domain/Application/SQLite/Presentation tests plus Windows smoke/manual layout checklist. |

## Verification Strategy

- Unit-test catalog invariants, supplier update identity, purchase arithmetic, VAT, and weighted cost.
- Test Application catalog membership, active/incomplete filtering, explicit line override, and rollback/no partial effects.
- Test SQLite migration preserves legacy pairs and historic purchase amounts.
- Test shared ViewModel selection/edit/cancel/catalog and purchase defaults.
- Build the full Windows solution and run configured test projects; manually exercise new/edit supplier, empty/long catalog, narrow window, and multi-line purchase states.
