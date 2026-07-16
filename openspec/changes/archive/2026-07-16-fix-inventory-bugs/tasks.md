# Tasks: Fix Inventory Bugs

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

## Phase 1: Foundation (database entities, configurations, migration)

- [x] 1.1 Add non-nullable string-mapped `SaleItemId` to `SaleReturnItem` and constructor parameters.
- [x] 1.2 Map `SaleItemId` as string in `SaleReturnItemConfiguration`.
- [x] 1.3 Add `UpdateReason` method to `InventoryMovement` entity.
- [x] 1.4 Generate EF Core migration `AddSaleItemIdToSaleReturnItem` with default value of empty GUID string for SQLite compatibility.

## Phase 2: Domain & Repositories (Product TracksInventory fixes, repository method to query movements)

- [x] 2.1 Enforce `TracksInventory` guard in `Product.IncreaseStock` and `DecreaseStock` methods.
- [x] 2.2 Add `GetMovementsByReferenceIdAsync` signature to `IInventoryRepository`.
- [x] 2.3 Implement `GetMovementsByReferenceIdAsync` in `InventoryRepository`.

## Phase 3: Application (use case fixes: CancelSale, AdjustStock, CompleteHeldSale, ProcessReturn)

- [x] 3.1 Block manual stock adjustments in `AdjustStockUseCase` if product `TracksInventory` is false.
- [x] 3.2 Restrict stock restoration in `CancelSaleUseCase` and `CancelHeldSaleUseCase` to products with `TracksInventory`.
- [x] 3.3 Scale quantity restored in `CancelSaleUseCase` by multiplying `item.Quantity` by `item.UnitsPerPresentation`.
- [x] 3.4 Query and group returns by `SaleItemId` instead of `ProductId` in `ProcessReturnUseCase` to isolate calculations per line.
- [x] 3.5 Align ledger reasons in `CompleteHeldSaleUseCase` by querying movements and updating their reason to `"Venta"`.

## Phase 4: Verification / Testing (Unit and Integration tests)

- [x] 4.1 Update `ProductTests` to verify `TracksInventory` guard ignores stock adjustments.
- [x] 4.2 Fix existing unit tests to supply `SaleItemId` to `SaleReturnItem` constructor/builders.
- [x] 4.3 Add integration tests for `AdjustStockUseCase` blocking non-tracked product adjustments.
- [x] 4.4 Add integration tests for `CancelSaleUseCase` and `CancelHeldSaleUseCase` verifying stock restoration quantities and tracking checks.
- [x] 4.5 Add integration tests for `ProcessReturnUseCase` verifying returns limit isolation by `SaleItemId`.
- [x] 4.6 Add integration tests for `CompleteHeldSaleUseCase` verifying movement reasons are updated correctly.
- [x] 4.7 Run solution build and test execution to ensure all scenarios pass successfully.
