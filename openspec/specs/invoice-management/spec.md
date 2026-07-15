# Invoice Management Specification

## Purpose

Defines receipt reprinting, sales history lookup, and return/refund processing.

## Requirements

### Requirement: Receipt reprint

The system MUST allow reprinting receipts for completed, cancelled, and returned sales.
(Previously: Only completed sales could be reprinted; cancelled ones were blocked.)

#### Scenario: Reprint receipt by sale ID

- GIVEN a completed sale with ID "abc-123"
- WHEN the user requests a reprint
- THEN the system MUST print the original receipt content

#### Scenario: Reprint receipt by invoice number

- GIVEN a completed sale with invoice number "FAC-0001"
- WHEN the user searches and reprints "FAC-0001"
- THEN the system MUST find the sale and print the receipt

#### Scenario: Reprint receipt for cancelled sale

- GIVEN a sale with status "cancelled"
- WHEN the user requests a reprint
- THEN the system MUST print the receipt for the cancelled sale

#### Scenario: Reprint receipt for returned sale

- GIVEN a sale with status "returned"
- WHEN the user requests a reprint
- THEN the system MUST print the receipt for the returned sale

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

The system MUST allow processing partial and full returns for completed sales. The sale status MUST remain "completed" if it is partially returned and MUST only change to "returned" when 100% of all item quantities have been returned.
(Previously: Only full returns were allowed, changing sale status to "returned" immediately.)

#### Scenario: Process full return

- GIVEN a completed sale with 3 items
- WHEN the user processes a 100% return
- THEN the sale status MUST change to "returned"
- AND stock MUST be restored and a return record persisted

#### Scenario: Process partial return

- GIVEN a completed sale with 2 items
- WHEN the user processes a return for 1 unit of one item
- THEN the sale status MUST remain "completed"
- AND stock MUST be restored and a return record persisted

#### Scenario: Cannot return fully returned sale

- GIVEN a sale with status "returned"
- WHEN the user attempts to return it
- THEN the system MUST reject with "Sale already returned"

#### Scenario: Cannot return cancelled sale

- GIVEN a sale with status "cancelled"
- WHEN the user attempts to return it
- THEN the system MUST reject the operation

#### Scenario: Return restores stock

- GIVEN a completed sale that reduced product X stock from 20 to 17
- WHEN the sale is returned
- THEN product X stock MUST increase from 17 to 20
- AND a stock movement with type "RETURN" MUST be recorded

### Requirement: Item-level partial return (SaleReturn entity)

The system MUST support item-level partial returns via a SaleReturn container. Multiple partial returns are supported; a non-unique index on OriginalSaleId allows multiple return records per sale.
(Previously: Unique database index on OriginalSaleId restricted returns to a single return event.)

**Attributes:** `Id` (Guid), `OriginalSaleId` (Guid), `ReturnDate` (DateTimeOffset), `ProcessedByUserId` (Guid), `Reason` (string), `TotalRefundAmount` (decimal), `Items` (IReadOnlyList<SaleReturnItem>).

#### Scenario: Partial return of 2 out of 5 units

- GIVEN Sale S1 has ProductX, Qty=5, Price=$10,000
- WHEN an Admin creates a SaleReturn for 2 units
- THEN a SaleReturn with TotalRefundAmount=$20,000 is persisted
- AND a SaleReturnItem is persisted and ProductX stock increases by 2

### Requirement: SaleReturnItem records per-item return detail

A SaleReturnItem MUST record per-item detail including ProductId, QuantityReturned (>= 1 and <= remaining quantity), and RefundAmount = QuantityReturned × UnitPrice.
(Previously: Validated quantity only against original sale qty instead of remaining qty, and blocked second return attempts.)

#### Scenario: Return quantity exceeding remaining quantity is rejected

- GIVEN Sale S1 contains ProductY, Qty=3
- AND a previous SaleReturn returned 1 unit of ProductY
- WHEN an Admin attempts a SaleReturn for 3 units of ProductY
- THEN the system MUST reject it as exceeding the remaining quantity

#### Scenario: Multiple partial returns allowed

- GIVEN Sale S1 contains ProductY, Qty=3
- AND a previous SaleReturn returned 1 unit of ProductY
- WHEN an Admin attempts to create a second SaleReturn for 1 unit of ProductY
- THEN the second return is persisted and sale status remains Completed

#### Scenario: Return with zero items is rejected

- GIVEN Sale S1
- WHEN an Admin submits a SaleReturn with empty items
- THEN the system MUST reject it

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
