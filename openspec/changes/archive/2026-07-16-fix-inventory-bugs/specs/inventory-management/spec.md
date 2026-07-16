# Delta for Inventory Management

## MODIFIED Requirements

### Requirement: Stock adjustment

The system MUST allow manual stock adjustments for corrections (damage, loss, count corrections) only for products that track inventory (`TracksInventory` is true). If a product does not track inventory, manual stock adjustments MUST be blocked.
(Previously: Manual stock adjustments were allowed for all products, without checking if they tracked inventory.)

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
(Previously: Inventory tracking was not checked on stock increases/decreases, and completing a held sale kept the ledger description as "Venta en espera".)

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
