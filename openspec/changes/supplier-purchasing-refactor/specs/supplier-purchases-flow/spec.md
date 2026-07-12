# Supplier Purchasing Refactor Specification

## Purpose

Define a supplier-maintained purchasing catalog and catalog-driven purchase receipt workflow while preserving legacy supplier-product relationships and historic purchases.

## Requirements

### Requirement: Edit supplier records without duplication

The system MUST let an operator select an existing supplier, load it into the supplier form, update that same supplier record, and cancel the edit safely.

#### Scenario: Update selected supplier

- GIVEN an existing supplier is selected
- WHEN the operator changes its contact data and saves
- THEN the system MUST send the selected supplier identifier to the supplier save use case
- AND the existing supplier record MUST be updated rather than a new record being created
- AND the supplier list MUST show the saved values

#### Scenario: Cancel supplier edit

- GIVEN an operator is editing a supplier
- WHEN the operator cancels the edit
- THEN the form fields, selected supplier, and editing identifier MUST be cleared
- AND no supplier data MUST be persisted

### Requirement: Maintain a supplier purchasing catalog

The system MUST let an operator maintain supplier-product catalog relationships from the selected supplier profile.

Each relationship MUST contain a product, optional supplier reference, non-empty purchase-unit label, conversion factor greater than zero, purchase-unit cost greater than zero, and active state.

#### Scenario: Create valid catalog relationship

- GIVEN an active supplier and an existing product
- WHEN an operator saves a unique relationship with valid unit, factor, and cost
- THEN the relationship MUST be persisted and shown in that supplier's catalog

#### Scenario: Reject invalid or duplicate catalog relationship

- GIVEN a catalog save request has a missing unit, non-positive factor or cost, an unknown product, inactive supplier, or an existing supplier-product pair
- WHEN the request is processed
- THEN the system MUST reject it with a recoverable validation error
- AND no relationship MUST be added or changed

#### Scenario: Retire catalog relationship without deletion

- GIVEN a catalog relationship exists
- WHEN the operator deactivates it
- THEN the relationship MUST remain in the supplier profile and product visibility results
- AND it MUST not be eligible for a new purchase

#### Scenario: Display incomplete legacy relationship

- GIVEN a legacy supplier-product pair has no configured unit, factor, or cost
- WHEN an operator opens the supplier profile
- THEN the relationship MUST be shown as requiring configuration
- AND it MUST remain ineligible for purchase until valid metadata is saved

### Requirement: Show product supplier relationships read-only

The product experience MUST show a read-only supplier summary containing supplier, supplier reference, purchase unit, conversion factor, current cost, and active state.

#### Scenario: View product suppliers

- GIVEN a product has active, inactive, or incomplete supplier relationships
- WHEN an operator views the product detail
- THEN all relationships MUST be shown with their current metadata and status
- AND the screen MUST not offer a second catalog-editing workflow

### Requirement: Build purchase drafts from an eligible supplier catalog

The purchase entry workflow MUST offer only the selected active supplier's active, fully configured catalog items.

#### Scenario: Select supplier with configured catalog

- GIVEN an active supplier has active catalog items with valid unit, factor, and cost
- WHEN the operator selects that supplier
- THEN the product selector MUST list only those catalog items
- AND each option MUST display product name, supplier reference, purchase unit, and purchase-unit price

#### Scenario: Select supplier without eligible catalog items

- GIVEN an active supplier has no active, fully configured catalog items
- WHEN the operator selects that supplier
- THEN the UI MUST explain that the supplier catalog requires configuration
- AND it MUST not permit an arbitrary product to be added

#### Scenario: Add catalog-backed purchase line

- GIVEN the operator selects an eligible catalog item and a positive purchase quantity
- WHEN the line is added
- THEN the draft MUST snapshot its purchase unit, conversion factor, and purchase-unit cost
- AND the UI MUST show purchased quantity, purchase unit, received inventory units, unit cost, and line subtotal

#### Scenario: Override purchase line cost explicitly

- GIVEN a catalog-backed line exists
- WHEN the operator explicitly changes its purchase-unit cost
- THEN the line snapshot MUST retain the changed cost for that purchase only
- AND the supplier catalog's current cost MUST remain unchanged

### Requirement: Confirm catalog-backed purchases atomically

The application MUST revalidate each purchase against an active supplier and active, configured catalog relationship and MUST confirm the receipt, inventory movement, and weighted-average product cost update atomically.

#### Scenario: Confirm valid receipt

- GIVEN every line references an eligible catalog item with positive quantity, cost, and factor
- WHEN the purchase is confirmed
- THEN received inventory units MUST equal `purchaseQuantity × unitsPerPurchaseUnit`
- AND line subtotal MUST equal `purchaseQuantity × purchaseUnitCost`
- AND the persisted receipt MUST retain the line snapshots
- AND stock and product cost MUST update in the same transaction

#### Scenario: Reject catalog bypass or invalid line

- GIVEN a request references an inactive supplier, absent/inactive/incomplete catalog item, zero quantity, zero cost, or zero factor
- WHEN the request reaches the application use case
- THEN the system MUST reject it even if WPF validation was bypassed
- AND no purchase, stock movement, or product-cost update MUST persist

### Requirement: Preserve VAT and historic purchase meaning

The confirmed purchase MUST persist additional VAT rate, VAT amount, subtotal, and total without altering historic purchase amounts.

#### Scenario: Calculate persisted totals

- GIVEN valid receipt lines and an additional VAT rate
- WHEN the receipt is confirmed
- THEN subtotal MUST equal the sum of line subtotals
- AND VAT MUST be calculated from the persisted rate and subtotal
- AND total MUST equal subtotal plus VAT

### Requirement: Keep WPF layout responsive and non-overlapping

The supplier profile and new-purchase screens MUST use intentional `Grid`/`DockPanel`/`ScrollViewer` composition rather than absolute positioning or overlapping sibling controls.

#### Scenario: Resize supplier and purchase screens

- GIVEN a new supplier, edited supplier, empty or long catalog, or a multi-line purchase draft
- WHEN the WPF window is reduced within the supported application resolution
- THEN forms MUST scroll vertically instead of covering adjacent sections
- AND catalog/history grids MUST remain height-bounded
- AND grouped primary actions MUST remain visible and operable
