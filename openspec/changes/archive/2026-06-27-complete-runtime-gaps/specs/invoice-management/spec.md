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
