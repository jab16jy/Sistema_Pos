# Delta for Invoice Management

## MODIFIED Requirements

### Requirement: SaleReturnItem records per-item return detail

A SaleReturnItem MUST record per-item detail including the specific sale line item identifier `SaleItemId`, ProductId, QuantityReturned (>= 1 and <= remaining quantity of the sale line item), and RefundAmount = QuantityReturned × UnitPrice. Returns MUST be validated and grouped by `SaleItemId` instead of `ProductId` to correctly track return limits when the same product exists in multiple sale lines.
(Previously: Returned quantities were validated and grouped by ProductId, which prevented accurate tracking and verification if the same product was present in multiple lines of the same sale; also, SaleItemId was not persisted.)

#### Scenario: Return quantity exceeding remaining quantity is rejected

- GIVEN Sale S1 contains a line item with ID "line-1" for ProductY, Qty=3
- AND a previous SaleReturn returned 1 unit for "line-1"
- WHEN an Admin attempts a SaleReturn for 3 units of ProductY for "line-1"
- THEN the system MUST reject it as exceeding the remaining quantity of that line

#### Scenario: Multiple partial returns allowed

- GIVEN Sale S1 contains a line item with ID "line-1" for ProductY, Qty=3
- AND a previous SaleReturn returned 1 unit for "line-1"
- WHEN an Admin attempts to create a second SaleReturn for 1 unit of ProductY for "line-1"
- THEN the second return is persisted and sale status remains Completed

#### Scenario: Return with zero items is rejected

- GIVEN Sale S1
- WHEN an Admin submits a SaleReturn with empty items
- THEN the system MUST reject it

#### Scenario: Returns grouped by SaleItemId for same product on different lines

- GIVEN Sale S1 has line "line-1" for ProductY (Qty=2) and line "line-2" for ProductY (Qty=3)
- WHEN an Admin processes a return for 2 units of "line-1" and 1 unit of "line-2"
- THEN the system MUST allow the return
- AND persist separate SaleReturnItem records referencing "line-1" and "line-2" respectively

### Requirement: Return restores inventory atomically

When a SaleReturn is confirmed, for each SaleReturnItem an InventoryMovement of type RETURN (positive quantity) MUST be created, provided that the product tracks inventory (`TracksInventory` is true). All InventoryMovements and the SaleReturn + SaleReturnItems MUST be saved in a single database transaction. If any part fails, the transaction MUST be rolled back entirely. If a product does not track inventory, no InventoryMovement is created, but the SaleReturn records MUST still be persisted in the transaction.
(Previously: Stock was restored on return without checking if the product tracked inventory.)

#### Scenario: Return rollback when InventoryMovement insert fails

- GIVEN a valid SaleReturn is being processed for tracking products
- WHEN the InventoryMovement insert raises a database error
- THEN the entire transaction is rolled back
- AND no SaleReturn, SaleReturnItem, or InventoryMovement record is persisted
- AND the Application layer surfaces the error to the Presentation layer

### Requirement: Sale cancellation stock restoration

When a sale (completed or held) is cancelled, the system MUST restore the stock of its items to the inventory. The restoration quantity MUST be calculated by multiplying the sale item quantity by its presentation scale (`UnitsPerPresentation`). Stock restoration and movement creation MUST only apply to products that track inventory (`TracksInventory` is true).
(Previously: Stock was restored on cancellation without verifying `TracksInventory`, and units were not scaled by `UnitsPerPresentation` in `CancelHeldSaleUseCase` / `CancelSaleUseCase`.)

#### Scenario: Cancel sale restores scaled stock for tracking products

- GIVEN a completed sale containing 2 boxes of Product Z (where `UnitsPerPresentation` = 12 and `TracksInventory` = true)
- WHEN the sale is cancelled
- THEN the stock of Product Z MUST increase by 24 units
- AND an InventoryMovement with type "CANCEL" and quantity 24 MUST be recorded

#### Scenario: Cancel sale does not restore stock for non-tracking products

- GIVEN a completed sale containing 5 units of Product W (where `TracksInventory` is false)
- WHEN the sale is cancelled
- THEN the stock of Product W MUST remain 0 (or unchanged)
- AND no InventoryMovement MUST be recorded
