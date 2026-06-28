# Invoice Management Specification

## Purpose

Defines receipt reprinting, sales history lookup, and return/refund processing.

## Requirements

### Requirement: Receipt reprint

The system MUST allow reprinting receipts for completed sales.

#### Scenario: Reprint receipt by sale ID

- GIVEN a completed sale with ID "abc-123"
- WHEN the user requests a reprint
- THEN the system MUST generate the same receipt content as the original
- AND the receipt MUST be sent to the configured printer

#### Scenario: Reprint receipt by invoice number

- GIVEN a completed sale with invoice number "FAC-0001"
- WHEN the user searches for "FAC-0001" and requests reprint
- THEN the system MUST find the sale and generate the receipt

#### Scenario: Cannot reprint cancelled sale

- GIVEN a sale with status "cancelled"
- WHEN the user attempts to reprint
- THEN the system MUST reject the operation
- AND the system MUST display "Cannot reprint: sale was cancelled"

### Requirement: Sales history lookup

The system MUST provide searchable sales history with filtering.

#### Scenario: Search sales by date range

- GIVEN sales on dates 2026-06-25, 2026-06-26, 2026-06-27
- WHEN the user filters for 2026-06-26
- THEN only sales from 2026-06-26 MUST be returned

#### Scenario: Search sales by customer name

- GIVEN sales for customers "Juan Pérez" and "María López"
- WHEN the user searches for "Pérez"
- THEN only sales for "Juan Pérez" MUST be returned

#### Scenario: Search sales by invoice number

- GIVEN a sale with invoice "FAC-0042"
- WHEN the user searches for "FAC-0042"
- THEN exactly that sale MUST be returned

#### Scenario: Empty result for no match

- GIVEN no sales match the search criteria
- WHEN the user searches
- THEN the result set MUST be empty (not an error)

### Requirement: Sale return processing

The system MUST allow processing full returns for completed sales.

#### Scenario: Process full return

- GIVEN a completed sale with 3 items
- WHEN the user processes a full return
- THEN the sale status MUST change to "returned"
- AND stock MUST be restored for all 3 items
- AND a return record MUST be created with the original sale reference

#### Scenario: Cannot return already returned sale

- GIVEN a sale with status "returned"
- WHEN the user attempts to return it again
- THEN the system MUST reject the operation
- AND the system MUST display "Sale already returned"

#### Scenario: Cannot return cancelled sale

- GIVEN a sale with status "cancelled"
- WHEN the user attempts to return it
- THEN the system MUST reject the operation

#### Scenario: Return restores stock

- GIVEN a completed sale that reduced product X stock from 20 to 17 (sold 3)
- WHEN the sale is returned
- THEN product X stock MUST increase from 17 to 20
- AND a stock movement with type "RETURN" MUST be recorded

### Requirement: Item-level partial return (SaleReturn entity)

The system MUST support item-level partial returns via a SaleReturn container entity that holds one or more SaleReturnItem records. A unique index on OriginalSaleId enforces one return event per sale.

**Attributes required:**
- `Id` (Guid)
- `OriginalSaleId` (Guid, FK to Sale)
- `ReturnDate` (DateTimeOffset)
- `ProcessedByUserId` (Guid, FK to User)
- `Reason` (string, required, max 500 chars)
- `TotalRefundAmount` (decimal) — sum of all SaleReturnItem.RefundAmount values
- `Items` (IReadOnlyList<SaleReturnItem>)

#### Scenario: Partial return of 2 out of 5 units of a single item

- GIVEN Sale S1 contains SaleItem: ProductX, Qty=5, UnitPrice=$10,000 COP
- AND no SaleReturn exists for S1
- WHEN an Administrator creates a SaleReturn for S1 with:
  - SaleReturnItem: ProductX, QuantityReturned=2, UnitPrice=$10,000
  - Reason="Producto dañado"
- THEN a SaleReturn record is persisted with OriginalSaleId=S1, TotalRefundAmount=$20,000
- AND a SaleReturnItem record is persisted with QuantityReturned=2, RefundAmount=$20,000
- AND an InventoryMovement of type RETURN with Quantity=+2 for ProductX is persisted
- AND all records are saved in the same database transaction
- AND the stock of ProductX increases by 2

### Requirement: SaleReturnItem records per-item return detail

A SaleReturnItem entity MUST record per-item return detail including ProductId, QuantityReturned (>= 1 and <= original SaleItem quantity), UnitPrice from the original SaleItem, and RefundAmount = QuantityReturned × UnitPrice.

#### Scenario: Return quantity exceeding original sale quantity is rejected

- GIVEN Sale S1 contains SaleItem: ProductY, Qty=3, UnitPrice=$5,000 COP
- WHEN an Administrator attempts a SaleReturn with SaleReturnItem: ProductY, QuantityReturned=4
- THEN the Application layer returns a validation error: "La cantidad a devolver (4) supera la cantidad vendida (3) para ProductY"
- AND no SaleReturn is persisted

#### Scenario: Second return attempt for the same sale is rejected

- GIVEN Sale S1 already has a SaleReturn (SaleReturn_A)
- WHEN an Administrator attempts to create a second SaleReturn for S1
- THEN the Application layer returns an error: "Ya existe una devolución registrada para esta venta"
- AND no new SaleReturn record is created

#### Scenario: Return with zero items is rejected

- GIVEN Sale S1 with valid items
- WHEN an Administrator submits a SaleReturn with an empty Items list
- THEN the Application layer returns a validation error: "Debe incluir al menos un artículo en la devolución"
- AND no SaleReturn is persisted

### Requirement: Return restores inventory atomically

When a SaleReturn is confirmed, for each SaleReturnItem an InventoryMovement of type RETURN (positive quantity) MUST be created. All InventoryMovements and the SaleReturn + SaleReturnItems MUST be saved in a single database transaction. If any part fails, the transaction MUST be rolled back entirely.

#### Scenario: Return rollback when InventoryMovement insert fails

- GIVEN a valid SaleReturn is being processed
- WHEN the InventoryMovement insert raises a database error
- THEN the entire transaction is rolled back
- AND no SaleReturn, SaleReturnItem, or InventoryMovement record is persisted
- AND the Application layer surfaces the error to the Presentation layer

### Requirement: Administrator authorization for returns

Initiating a partial return MUST require the current user to have the Administrator role. Cashier-role users MUST see an error message if they attempt to access the return workflow.

#### Scenario: Cashier cannot initiate a return

- GIVEN a user with role=Cashier is logged in
- WHEN the cashier attempts to access the return workflow for any sale
- THEN the Application layer returns an authorization error
- AND the return screen is not accessible to the cashier
