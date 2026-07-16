# Delta for Product Catalog

## MODIFIED Requirements

### Requirement: Product creation

The system MUST allow creating products with name, price, barcode, minimum stock level, a valid category, and an indicator of whether the product tracks inventory (`TracksInventory` boolean, defaults to true).
(Previously: The system only allowed creating products with name, price, barcode, minimum stock level, and a valid category, without a `TracksInventory` property.)

#### Scenario: Create product with all fields

- GIVEN a user provides valid product data including `TracksInventory` = true
- WHEN the product is saved
- THEN the product MUST be persisted with all provided fields
- AND the product MUST receive a unique identifier

#### Scenario: Reject duplicate barcode

- GIVEN a product with barcode "ABC123" already exists
- WHEN a user attempts to create another product with barcode "ABC123"
- THEN the system MUST reject the creation
- AND the system MUST display a validation error

#### Scenario: Allow null barcode

- GIVEN a user creates a product without a barcode
- WHEN the product is saved
- THEN the product MUST be persisted successfully
- AND the barcode field MUST be null

### Requirement: Current stock is not editable from the product catalog

`CurrentStock` is master data that the catalog MAY display but MUST NOT edit directly. Ordinary product editing MUST NOT change stock; all stock changes go through traceable inventory movements (Inventario adjustments, Compras receipts, Ventas/Devoluciones/anulaciones). Product creation MAY accept an initial balance, but the system MUST record it as an `InitialBalance` inventory movement rather than writing `CurrentStock` directly, and only if the product tracks inventory (`TracksInventory` is true). When no initial balance is provided, or the product does not track inventory, the product MUST be created with zero stock. `MinimumStock` MUST remain editable from the catalog.
(Previously: The system accepted initial balance and recorded inventory movements regardless of whether the product tracked inventory.)

#### Scenario: Editing a product does not change stock

- GIVEN an existing product with current stock = 10
- WHEN the user edits the product and saves (any field)
- THEN the product's current stock MUST remain 10
- AND no inventory movement MUST be created by the edit

#### Scenario: Initial balance on creation is recorded as a movement

- GIVEN a user creates a product with an initial balance of 20 units
- AND the product tracks inventory (`TracksInventory` is true)
- WHEN the product is saved
- THEN the product MUST start at zero stock and reach 20 through exactly one `InitialBalance` inventory movement
- AND the movement MUST record the operator and quantity for traceability

#### Scenario: Creation without initial balance yields zero stock

- GIVEN a user creates a product without specifying an initial balance
- WHEN the product is saved
- THEN the product MUST be created with current stock = 0
- AND no inventory movement MUST be created

#### Scenario: Initial balance ignored if product does not track inventory

- GIVEN a user creates a product with an initial balance of 20 units
- AND the product does not track inventory (`TracksInventory` is false)
- WHEN the product is saved
- THEN the product MUST be created with current stock = 0
- AND no `InitialBalance` inventory movement MUST be created
