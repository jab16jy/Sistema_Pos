# Inventario View Specification

## Purpose

Defines the InventoryView UI translation: a KPI strip, a filterable stock grid with visual stock bars, a stock-adjustment panel with signed delta input and live preview, and a filterable movements history.

## Requirements

### Requirement: KPI strip summarizes stock health

The view MUST show 3 KPI cards: total SKUs, stock bajo count, and sin stock count.

#### Scenario: KPI counts match filtered data

- GIVEN 142 total SKUs, 8 below minimum, and 3 at zero stock
- WHEN the KPI strip renders
- THEN it MUST display "142", "8", and "3" respectively, with warning/error coloring for the low/out counts

### Requirement: Stock grid is searchable, filterable by status, and shows a visual bar

The stock grid MUST support free-text search and a status filter (`Todos`, `En stock`, `Stock bajo`, `Sin stock`), and each row MUST render a stock-level bar colored by status (success/warning/error) alongside the numeric stock value.

#### Scenario: Filtering by "Sin stock" shows only zero-stock products

- GIVEN products with mixed stock levels
- WHEN the operator selects status filter "Sin stock"
- THEN only rows with stock = 0 MUST be displayed

#### Scenario: Row quick-adjust action opens the adjustment panel

- GIVEN a stock grid row for "Alcohol 70% 500ml"
- WHEN the operator activates its row action icon
- THEN the adjustment panel MUST pre-select that product

### Requirement: Adjustment panel uses a signed delta with live before/after preview

The adjustment panel MUST let the operator select a product, toggle a positive/negative sign, enter a quantity, select a required reason, and optionally add a note; it MUST show a live "Stock actual → Nuevo stock" preview as the sign/quantity change, and MUST flag the resulting stock in error color if it falls below the minimum.

#### Scenario: Toggling sign changes the delta direction

- GIVEN the sign is positive and quantity is 5
- WHEN the operator toggles the sign to negative
- THEN the preview MUST recompute using -5 instead of +5

#### Scenario: Applying an adjustment without a reason is blocked

- GIVEN a product and quantity are set but no reason is selected
- WHEN the operator activates "Aplicar ajuste"
- THEN the system MUST show an error ("Selecciona un motivo") and MUST NOT apply the adjustment

#### Scenario: Applying a valid adjustment confirms and resets the panel

- GIVEN a product, quantity, and reason are all set
- WHEN the operator activates "Aplicar ajuste"
- THEN a success toast ("Ajuste aplicado correctamente") MUST appear
- AND the panel MUST reset to its empty state

### Requirement: Movements history is filterable by movement type

The movements panel MUST list entrada/salida/ajuste movements (product, reason, user, time, resulting stock, signed delta) and MUST support filtering by type via a dropdown.

#### Scenario: Filtering movements by "Entradas" narrows the list

- GIVEN movements include entrada, salida, and ajuste types
- WHEN the operator selects filter "Entradas"
- THEN only entrada-type rows MUST be displayed

#### Scenario: No movements for a filter shows an empty message

- GIVEN a filter matches zero movements
- WHEN the list renders
- THEN it MUST display "Sin movimientos" instead of an empty list
