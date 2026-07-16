# Inventory Management Specification

## Purpose

Defines stock queries, movement tracking, and low-stock alert functionality.

## Requirements

### Requirement: Stock query

The system MUST provide real-time stock levels for all active products.

#### Scenario: View current stock

- GIVEN products with varying stock levels
- WHEN the user opens the inventory module
- THEN each product MUST display its current stock quantity
- AND the stock MUST reflect all completed purchases and sales

#### Scenario: Stock reflects completed transactions only

- GIVEN a sale in "pending" status
- WHEN the user views inventory
- THEN the stock MUST NOT be reduced by the pending sale
- AND the stock MUST only decrease when the sale status becomes "completed"

### Requirement: Low-stock alerts

The system MUST highlight products at or below their configured minimum stock level.

#### Scenario: Product at minimum stock

- GIVEN a product with minimum stock = 10 and current stock = 10
- WHEN the user views inventory
- THEN the product MUST be highlighted as low stock

#### Scenario: Product below minimum stock

- GIVEN a product with minimum stock = 10 and current stock = 5
- WHEN the user views inventory
- THEN the product MUST be highlighted as low stock
- AND the display MUST show the deficit (5 units below minimum)

#### Scenario: Product without minimum stock set

- GIVEN a product with no minimum stock configured
- WHEN the user views inventory
- THEN the product MUST NOT appear in low-stock alerts

### Requirement: Stock movement history

The system MUST track all stock movements with type, quantity, and reference.

#### Scenario: Record purchase stock movement

- GIVEN a purchase of 50 units of product X is completed
- WHEN the system processes the purchase
- THEN a stock movement record MUST be created with type "IN", quantity 50, and reference to the purchase

#### Scenario: Record sale stock movement

- GIVEN a sale of 3 units of product X is completed
- WHEN the system processes the sale
- THEN a stock movement record MUST be created with type "Exit", quantity 3, and reference to the sale
- AND the sale's stock decrease and its movement MUST be persisted in the same unit of work

#### Scenario: Record initial-balance stock movement

- GIVEN a product is created (or imported) with an initial balance of 20 units
- WHEN the system persists the product
- THEN the product MUST start at zero stock and a movement with type "InitialBalance" and quantity 20 MUST be created in the same unit of work

#### Scenario: View movement history for product

- GIVEN stock movements exist for product X
- WHEN the user views movement history for product X
- THEN all movements MUST be displayed in reverse chronological order
- AND each movement MUST show type, quantity, date, and reference

### Requirement: Stock adjustment

The system MUST allow manual stock adjustments for corrections (damage, loss, count corrections) only for products that track inventory (`TracksInventory` is true). If a product does not track inventory, manual stock adjustments MUST be blocked.

#### Scenario: Manual stock increase

- GIVEN a product with current stock = 20
- AND the product tracks inventory (`TracksInventory` is true)
- WHEN the user adjusts stock by +5 with reason "Count correction"
- THEN the stock MUST become 25
- AND a movement record with type "ADJUSTMENT" MUST be created

#### Scenario: Manual stock decrease

- GIVEN a product with current stock = 20
- AND the product tracks inventory (`TracksInventory` is true)
- WHEN the user adjusts stock by -3 with reason "Damaged goods"
- THEN the stock MUST become 17
- AND a movement record with type "ADJUSTMENT" MUST be created

#### Scenario: Manual stock adjustment blocked for non-inventory product

- GIVEN a product where `TracksInventory` is false
- WHEN the user attempts to adjust stock manually
- THEN the system MUST reject the adjustment with a validation error
- AND no inventory movement MUST be created

### Requirement: Stock change traceability invariant

Every change to a product's `CurrentStock` MUST correspond to exactly one `InventoryMovement` persisted in the same unit of work, carrying its type, quantity, operator, timestamp, reason (when applicable) and business reference (when one exists), provided that the product tracks inventory (`TracksInventory` is true). If a product does not track inventory, any operation to increase/decrease stock MUST have no effect on `CurrentStock` and MUST NOT persist an `InventoryMovement`. When completing a held sale, the ledger description (movement reason) MUST be updated to "Venta" instead of "Venta en espera". Inventory MUST be the single manual-adjustment path; Compras, Ventas, Devoluciones and anulaciones MUST change stock only through their traceable movements. Stock MUST never become negative for tracking products.

#### Scenario: Stock reconciles with movement history

- GIVEN a tracking product created with an initial balance and later affected by purchases, sales and adjustments
- WHEN its current stock is compared against the signed sum of its inventory movements
- THEN the two MUST be equal

#### Scenario: Reject a change that would drive stock negative

- GIVEN a tracking product with current stock = 5
- WHEN an operation (sale or negative adjustment) would reduce stock below zero
- THEN the operation MUST be rejected
- AND neither the stock nor any movement MUST be persisted

#### Scenario: Stock changes ignored for non-tracking products

- GIVEN a product with `TracksInventory` set to false
- WHEN a purchase, sale, return, or cancellation is processed
- THEN the product's `CurrentStock` MUST remain 0 (or unchanged)
- AND no `InventoryMovement` MUST be recorded
