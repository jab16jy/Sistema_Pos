# Compras View Specification

## Purpose

Defines the PurchasesView UI translation: a supplier panel with inline new-supplier creation, a purchase header, an item-entry row, and an itemized purchase list with computed totals, matching the wireframe's order/reception flow.

## Requirements

### Requirement: Supplier panel supports search and inline creation

The suppliers panel MUST show a searchable supplier list (name, phone, product count) and MUST offer a collapsible "Nuevo proveedor" form (name required, phone, NIT, email) that expands only after the operator requests it.

#### Scenario: Selecting a supplier updates the purchase header chip

- GIVEN the operator selects "Distribuidora MedFarma"
- WHEN the selection is made
- THEN the purchase header chip MUST display the supplier's name using primary styling

#### Scenario: New-supplier form is hidden until requested

- GIVEN the operator has not clicked "add supplier"
- WHEN the panel renders
- THEN the "Nuevo proveedor" form MUST NOT be visible
- AND clicking the add action MUST reveal it

### Requirement: Purchase header captures invoice metadata

The "Nueva compra" header MUST capture supplier invoice number, purchase date, and payment method (Contado, Crédito 30 días, Crédito 60 días).

#### Scenario: Payment method defaults to Contado

- GIVEN a new purchase is started
- WHEN the header renders
- THEN the payment method MUST default to "Contado"

### Requirement: Item entry adds rows to the purchase with computed subtotal

The item-entry row MUST accept a product (name/barcode, with autocomplete suggestions), quantity, and unit cost, and MUST add a line to the items list on "Agregar", computing subtotal per item and running IVA (19%)/total for the whole purchase.

#### Scenario: Adding an item updates the running totals

- GIVEN an empty items list
- WHEN the operator adds "Bisturí Desechable #22" with qty 100 at cost $800
- THEN the items list MUST show 1 item with subtotal $80,000
- AND total (with 19% IVA) MUST be $95,200

#### Scenario: Removing an item recalculates totals

- GIVEN the items list has 2 items
- WHEN the operator removes one item
- THEN the items list MUST show 1 item
- AND subtotal/IVA/total MUST recompute for the remaining item only

### Requirement: Registering a purchase requires a supplier and at least one item

#### Scenario: Registering without a supplier is blocked

- GIVEN no supplier is selected
- WHEN the operator activates "Registrar compra"
- THEN the system MUST show an error ("Selecciona un proveedor") and MUST NOT register the purchase

#### Scenario: Registering without items is blocked

- GIVEN a supplier is selected but the items list is empty
- WHEN the operator activates "Registrar compra"
- THEN the system MUST show an error ("Agrega al menos un producto") and MUST NOT register the purchase

#### Scenario: Successful registration confirms and clears the items list

- GIVEN a supplier is selected and items are present
- WHEN the operator activates "Registrar compra"
- THEN a success toast referencing the supplier name MUST appear
- AND the items list MUST reset to empty
