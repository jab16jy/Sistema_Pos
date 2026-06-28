# Product Catalog Specification

## Purpose

Defines the product catalog domain: creation, editing, search, barcode management, and stock threshold configuration.

## Requirements

### Requirement: Product creation

The system MUST allow creating products with name, price, barcode, minimum stock level, and optional category.

#### Scenario: Create product with all fields

- GIVEN a user provides valid product data
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

### Requirement: Product editing

The system MUST allow editing any mutable field of an existing product.

#### Scenario: Update product price

- GIVEN an existing product with price $10.00
- WHEN the user updates the price to $12.50
- THEN the product MUST reflect the new price
- AND existing sales referencing this product MUST NOT be affected

#### Scenario: Prevent editing deleted product

- GIVEN a product that has been soft-deleted
- WHEN a user attempts to edit it
- THEN the system MUST reject the operation

### Requirement: Product search

The system MUST support searching products by name, barcode, or category.

#### Scenario: Search by name substring

- GIVEN products "Aspirina" and "Aspirin Forte" exist
- WHEN the user searches for "Aspir"
- THEN both products MUST appear in results

#### Scenario: Search by exact barcode

- GIVEN a product with barcode "7501234567890"
- WHEN the user searches for "7501234567890"
- THEN exactly that product MUST be returned

#### Scenario: Search returns empty for no match

- GIVEN no products match the search term
- WHEN the user searches
- THEN the result set MUST be empty (not an error)

### Requirement: Product deletion

The system MUST support soft-deleting products to preserve sale history integrity.

#### Scenario: Soft-delete product

- GIVEN an active product
- WHEN the user deletes it
- THEN the product MUST be marked as inactive
- AND the product MUST NOT appear in active product searches
- AND existing sales MUST still reference it correctly

### Requirement: Minimum stock threshold

The system MUST allow configuring a minimum stock level per product for inventory alerts.

#### Scenario: Set minimum stock

- GIVEN a product with no minimum stock set
- WHEN the user sets minimum stock to 10
- THEN the product MUST store this threshold
- AND the inventory module MUST use this for low-stock alerts
