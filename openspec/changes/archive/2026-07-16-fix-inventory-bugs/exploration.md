# Exploration: fix-inventory-bugs

## Scope

Investigate and outline the resolution of 5 inventory integration bugs found during the audit:
1. `TracksInventory` Increase/Decrease logic inconsistency in the `Product` entity.
2. `CancelSaleUseCase` missing multiplication by `UnitsPerPresentation` and `TracksInventory` checks when restoring stock.
3. `ProcessReturnUseCase` tracking returns by `ProductId` instead of `SaleItemId`.
4. Hold/Complete stock movement description mismatch in the inventory ledger.
5. Manual adjustments allowing changes for non-inventory products.

This exploration details the current state, recommended design, affected files, and test scenarios without implementing the application changes.

---

## Confirmed Current State

### 1. TracksInventory Increase/Decrease Logic Inconsistency
- **File**: [Product.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Domain/Entities/Product.cs)
- **Current State**:
  - `DecreaseStock` (lines 283-296) has an explicit guard:
    ```csharp
    if (!TracksInventory)
    {
        return;
    }
    ```
  - `IncreaseStock` (line 298) does not check `TracksInventory` and updates `CurrentStock` unconditionally:
    ```csharp
    public void IncreaseStock(Quantity quantity) => CurrentStock += quantity.Value;
    ```
  - This inconsistency results in stock being modified for non-inventory items during stock increases (e.g. purchases, returns, sale cancellations), while remaining unchanged during decreases (sales).

### 2. CancelSaleUseCase Missing Presentation Multiplication and Checks
- **File**: [CancelSaleUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs)
- **Current State**:
  - In lines 79-90, the use case restores stock for cancelled sale items:
    ```csharp
    var quantity = new Quantity(item.Quantity);
    product.IncreaseStock(quantity);
    ```
  - It does not multiply `item.Quantity` by `item.UnitsPerPresentation`. If a product is sold as a box (e.g., 2 boxes of 12 units each), only 2 units are restored instead of 24.
  - It does not check if the product has `TracksInventory` enabled. Stock is updated, and an `InventoryMovement` of type `Return` is added to the ledger regardless of whether the product tracks inventory.

### 3. ProcessReturnUseCase Grouping by ProductId instead of SaleItemId
- **File**: [ProcessReturnUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs)
- **Current State**:
  - In lines 78-81, the use case fetches past returns for the sale and groups them by `ProductId`:
    ```csharp
    var pastReturnedQtyByProduct = pastReturns
        .SelectMany(r => r.Items)
        .GroupBy(i => i.ProductId)
        .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityReturned));
    ```
  - If a sale contains multiple lines for the same product (e.g., different presentations or different unit prices), grouping by `ProductId` mixes returned quantities across different lines.
  - The [SaleReturnItem](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Domain/Entities/SaleReturnItem.cs) entity itself does not persist or store the original `SaleItemId`.

### 4. Hold/Complete Stock Movement Description Mismatch
- **Files**: 
  - [HoldSaleUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Sales/HoldSaleUseCase.cs)
  - [CompleteHeldSaleUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Sales/CompleteHeldSaleUseCase.cs)
- **Current State**:
  - When putting a sale on hold, `HoldSaleUseCase` reserves stock by adding an `Exit` movement with the reason `"Venta en espera"`.
  - When completing the draft sale, `CompleteHeldSaleUseCase` updates the sale status to `Completed`, but does not register another movement (to avoid double-decrementing stock) and leaves the original movement description as `"Venta en espera"`.
  - Completed sales created directly via `CreateSaleUseCase` write an `Exit` movement with the reason `"Venta"`. This creates a mismatch in the inventory ledger for completed sales.

### 5. Manual Adjustments for Non-Inventory Products
- **File**: [AdjustStockUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Inventory/AdjustStockUseCase.cs)
- **Current State**:
  - The use case registers stock adjustments via `product.RegisterStockMovement(InventoryMovementType.Adjustment, request.Delta, request.Reason, request.UserId)` without validating if the product tracks inventory.
  - Non-inventory products can have their stock adjusted manually, generating invalid movements.

---

## Recommended Design

### 1. Align Product Stock Invariants
- Add an explicit `if (!TracksInventory) return;` guard to `Product.IncreaseStock`.
- Ensure `Product.RegisterStockMovement` returns `null` or exits without creating an `InventoryMovement` if `TracksInventory` is false, or ensure that callers only invoke it when tracking is enabled.

### 2. Correct Sale Cancellation Stock Logic
- Update `CancelSaleUseCase` and `CancelHeldSaleUseCase` to check `product.TracksInventory` before restoring stock.
- Multiply the quantity restored by `item.UnitsPerPresentation`:
  ```csharp
  var quantity = new Quantity(item.Quantity * item.UnitsPerPresentation);
  ```

### 3. Track Returns by SaleItemId
- **Modify Entity**: Add a non-nullable `SaleItemId` property to `SaleReturnItem` (with a corresponding parameter in its constructor) and configure its EF mapping in `SaleReturnItemConfiguration`.
- **EF Migration**: Add a new EF migration `AddSaleItemIdToSaleReturnItem` to rollout the schema change in `SALE_RETURN_ITEMS` table.
- **Update ProcessReturnUseCase**:
  - Retrieve and group past returns by `SaleItemId` instead of `ProductId`:
    ```csharp
    var pastReturnedQtyBySaleItem = pastReturns
        .SelectMany(r => r.Items)
        .GroupBy(i => i.SaleItemId)
        .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityReturned));
    ```
  - Map return validation and calculations using `saleItem.Id` as the key.
  - Pass the original `saleItem.Id` to `SaleReturnItem` constructor during instantiation.

### 4. Align Hold/Complete Movement Description
- **Update Entity**: Add a public method `UpdateReason(string reason)` to `InventoryMovement` to allow modifying its description.
- **Update Repository**: Add `Task<IReadOnlyList<InventoryMovement>> GetMovementsByReferenceIdAsync(Guid referenceId, CancellationToken cancellationToken)` to `IInventoryRepository` (and its EF implementation) to query movements by the associated Sale ID.
- **Update CompleteHeldSaleUseCase**: Inject `IInventoryRepository`. When completing a held sale, retrieve movements matching the `sale.Id` and update their reason from `"Venta en espera"` to `"Venta"`.

### 5. Block Adjustments on Non-Inventory Products
- Update `AdjustStockUseCase` to check `product.TracksInventory`. Throw an `InvalidOperationException` if the product does not track inventory.

---

## Affected Files

### Production
- `src/POS.Domain/Entities/Product.cs` — Align `IncreaseStock` guard.
- `src/POS.Domain/Entities/SaleReturnItem.cs` — Add `SaleItemId` property and constructor parameter.
- `src/POS.Domain/Entities/InventoryMovement.cs` — Add `UpdateReason` method.
- `src/POS.Domain/Interfaces/IInventoryRepository.cs` — Add `GetMovementsByReferenceIdAsync` signature.
- `src/POS.Infrastructure/Persistence/Configurations/SaleReturnItemConfiguration.cs` — Configure `SaleItemId` property.
- `src/POS.Infrastructure/Persistence/Repositories/InventoryRepository.cs` — Implement `GetMovementsByReferenceIdAsync`.
- `src/POS.Application/UseCases/Inventory/AdjustStockUseCase.cs` — Guard against adjusting non-inventory products.
- `src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs` — Restore stock using units per presentation and check `TracksInventory`.
- `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs` — Track and group past returns by `SaleItemId`.
- `src/POS.Application/UseCases/Sales/CompleteHeldSaleUseCase.cs` — Resolve Hold/Complete description mismatch.
- `src/POS.Application/UseCases/Sales/CancelHeldSaleUseCase.cs` — Restore stock using units per presentation and check `TracksInventory`.

### Tests & Infrastructure
- `tests/POS.Domain.Tests/SaleReturnTests.cs` — Update fakes/instances of `SaleReturnItem` with mock `SaleItemId`.
- All `IInventoryRepository` test fakes (e.g., `InventoryRepositoryFake`, `InMemoryInventoryRepository`, etc.) — Implement the new `GetMovementsByReferenceIdAsync` signature.
- `src/POS.Infrastructure/Migrations/` — Add a new migration `AddSaleItemIdToSaleReturnItem`.

---

## Test Scenarios

1. **TracksInventory Invariant**:
   - Verify that adding stock to a product with `TracksInventory = false` does not modify its `CurrentStock`.
2. **Sale Cancellation (Units & Boxes)**:
   - Cancel a sale containing a product sold as a box (e.g. 3 boxes of 6 units) and verify that 18 units are restored to the product's stock.
   - Cancel a sale with a product that does not track inventory and verify that no stock is added and no movement is created.
3. **Return Quantities by SaleItemId**:
   - Create a sale with two different lines containing the same product (e.g., one line with Unit presentation, one line with Box presentation).
   - Perform partial returns on each line separately and verify that remaining quantities are validated correctly per line, without mixing them up.
4. **Hold/Complete Ledger Alignment**:
   - Hold a sale and verify the stock movement has the description `"Venta en espera"`.
   - Complete the same held sale and verify the description of the movement in the database changes to `"Venta"`.
5. **Manual Adjustments Block**:
   - Attempt to execute `AdjustStockUseCase` on a product with `TracksInventory = false` and verify that it throws an `InvalidOperationException`.

---

## Risks and Ambiguities Requiring Resolution

1. **EF Migration SQLite Nullability**:
   - Since we are adding `SaleItemId` to an existing table `SALE_RETURN_ITEMS`, SQLite does not support adding non-nullable columns without a default value if there are pre-existing rows. We should either configure `SaleItemId` as nullable `Guid?` at the database configuration level (and map it to a non-nullable property in the domain if appropriate), or provide a default value (e.g., `00000000-0000-0000-0000-000000000000`). Using a nullable `Guid?` in the DB layer is the safest path to preserve existing data.
2. **Description Translation**:
   - The ledger descriptions `"Venta"` and `"Venta en espera"` are hardcoded in Spanish in the codebase. The updated complete reason should match this convention exactly.

---

## Rollback Considerations

- The schema migration is additive. A rollback will require removing the new migration and dropping the `SaleItemId` column from `SALE_RETURN_ITEMS`.
- Since existing return items won't have a `SaleItemId` (mapped to `null` or a default empty GUID), rollback plans should avoid breaking queries relying on the column.
