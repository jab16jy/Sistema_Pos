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
- THEN a stock movement record MUST be created with type "OUT", quantity 3, and reference to the sale

#### Scenario: View movement history for product

- GIVEN stock movements exist for product X
- WHEN the user views movement history for product X
- THEN all movements MUST be displayed in reverse chronological order
- AND each movement MUST show type, quantity, date, and reference

### Requirement: Stock adjustment

The system MUST allow manual stock adjustments for corrections (damage, loss, count corrections).

#### Scenario: Manual stock increase

- GIVEN a product with current stock = 20
- WHEN the user adjusts stock by +5 with reason "Count correction"
- THEN the stock MUST become 25
- AND a movement record with type "ADJUSTMENT" MUST be created

#### Scenario: Manual stock decrease

- GIVEN a product with current stock = 20
- WHEN the user adjusts stock by -3 with reason "Damaged goods"
- THEN the stock MUST become 17
- AND a movement record with type "ADJUSTMENT" MUST be created
