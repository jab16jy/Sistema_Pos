# Proposal: fix-inventory-bugs

## Intent
Resolve five distinct inventory integration issues identified in the audit: aligning stock tracking invariants, correcting sale cancellation stock logic, tracking return quantities accurately, aligning draft/held/completed sale ledger descriptions, and blocking stock adjustments on non-inventory items.

## Scope
- Fix stock increases/decreases checking `TracksInventory` on `Product`.
- Update `CancelSaleUseCase`/`CancelHeldSaleUseCase` to check tracking and restore stock using `UnitsPerPresentation`.
- Group returns in `ProcessReturnUseCase` by `SaleItemId` instead of `ProductId` and persist `SaleItemId`.
- Update ledger description when completing a held sale from "Venta en espera" to "Venta".
- Prevent manual stock adjustments for non-inventory products.

## Capabilities
Modified capabilities:
- `inventory-management`: Aligning invariants, manual adjustments, and ledger descriptions.
- `invoice-management`: Cancellation and return use cases logic.
- `cashier-sales-flow`: Held sales complete ledger descriptions.
- `product-catalog`: Stock tracking consistency on the product entity.

## Approach
1. **Domain**: Check `TracksInventory` in `Product.IncreaseStock`. Add `SaleItemId` to `SaleReturnItem` and `UpdateReason` to `InventoryMovement`.
2. **Infrastructure**: Add EF migration for `SaleItemId`. Implement `GetMovementsByReferenceIdAsync` in `InventoryRepository`.
3. **Application**:
   - Guard `AdjustStockUseCase` with `TracksInventory`.
   - Update `CancelSaleUseCase`/`CancelHeldSaleUseCase` to multiply quantity by presentation units and check `TracksInventory`.
   - Group past returns by `SaleItemId` in `ProcessReturnUseCase`.
   - Update movement reasons in `CompleteHeldSaleUseCase`.

## Affected Areas
- **Domain layer**: `Product.cs`, `SaleReturnItem.cs`, `InventoryMovement.cs`.
- **Application layer**: Use cases (`AdjustStockUseCase.cs`, `CancelSaleUseCase.cs`, `CancelHeldSaleUseCase.cs`, `ProcessReturnUseCase.cs`, `CompleteHeldSaleUseCase.cs`).
- **Infrastructure layer**: `InventoryRepository.cs`, `IInventoryRepository.cs`, `SaleReturnItemConfiguration.cs`, and DB migrations.
- **Tests**: Domain and application unit tests, persistence repository fakes.

## Risks
- Database schema migration for existing records on `SALE_RETURN_ITEMS` table in SQLite. Setting `SaleItemId` as nullable in SQLite configuration prevents migration failures.

## Rollback Plan
- Revert schema migrations using EF Core CLI, remove the added column, and revert domain/application changes.

## Dependencies
- EF Core migrations framework.

## Success Criteria
- Stock is only modified when `TracksInventory` is true.
- Box sales restore correct units on cancellation.
- Returns are validated per sale line.
- Ledger descriptions match for direct and completed held sales.
- Manual stock adjustments are blocked for non-tracking products.
- All unit and integration tests pass.
