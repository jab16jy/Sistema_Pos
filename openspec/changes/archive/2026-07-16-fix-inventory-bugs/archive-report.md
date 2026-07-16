# Archive Report: fix-inventory-bugs

- **Date**: 2026-07-16
- **Change Name**: fix-inventory-bugs
- **Status**: Completed & Archived

## 1. Tasks Verification
All implementation tasks inside `tasks.md` have been successfully completed and verified:
- [x] Phase 1: Foundation (database entities, configurations, migration)
- [x] Phase 2: Domain & Repositories (Product TracksInventory fixes, repository method to query movements)
- [x] Phase 3: Application (use case fixes: CancelSale, AdjustStock, CompleteHeldSale, ProcessReturn)
- [x] Phase 4: Verification / Testing (Unit and Integration tests)

## 2. Specification Merges
- **inventory-management**: Integrated delta updates for manual stock adjustments restricted to tracking products (`TracksInventory` is true), stock change traceability invariant with no stock effects for non-tracking products, and ledger description update to "Venta" instead of "Venta en espera" when completing held sales.
- **product-catalog**: Integrated delta updates for product creation with the `TracksInventory` boolean indicator, and initial balance recording as an inventory movement only for products that track inventory (otherwise defaults to zero stock).
- **invoice-management**: Integrated delta updates for partial returns tracked and validated by line-level `SaleItemId` instead of `ProductId` to group returns correctly, restoring inventory on return only for tracking products, and restoring scaled inventory (using presentation scale `UnitsPerPresentation`) during sale cancellation only for tracking products.
- **cashier-sales-flow**: Integrated delta updates to update inventory movement reasons to "Venta" when a cashier completes a previously held sale.

## 3. Archive Actions
- Change directory `openspec/changes/fix-inventory-bugs/` moved to `openspec/changes/archive/2026-07-16-fix-inventory-bugs/`.
