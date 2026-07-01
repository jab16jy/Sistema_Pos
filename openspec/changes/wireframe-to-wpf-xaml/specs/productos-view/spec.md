# Productos View Specification

## Purpose

Defines the ProductsView UI translation to the wireframe's richer catalog: a two-subtab layout ("Productos" / "Categorías"), a list+form product editor with margin preview and stock-status preview, CSV import, and a link to Barcode label generation.

## Requirements

### Requirement: Two subtabs — Productos and Categorías

The view MUST expose "Productos" (list + editor) and "Categorías" (grid + editor) subtabs.

#### Scenario: Switching to Categorías shows the category grid

- GIVEN the operator is on the "Productos" subtab
- WHEN the operator activates "Categorías"
- THEN a data grid of categories (name, active/inactive) MUST render with a category edit form

### Requirement: Product list supports search and links to Barcode and CSV import

The product list toolbar MUST provide: a search box (name or barcode), an "Etiquetas" link to the Barcode module, a "CSV" import trigger, and a "Nuevo" action, plus a paginated/scrollable product list showing name, code+category, price, and a stock-status chip.

#### Scenario: Searching filters the product list

- GIVEN the product list contains "Ibuprofeno 400mg" and "Crema Cicatrizante 50g"
- WHEN the operator types "crema" in the search box
- THEN only "Crema Cicatrizante 50g" MUST remain visible

#### Scenario: No results shows an empty state

- GIVEN the search text matches no product
- WHEN the list renders
- THEN an empty state with "Sin resultados" MUST display instead of an empty list

### Requirement: Product form computes margin and stock-status preview live

The product editor MUST show a live margin preview (amount and percentage, colored negative when cost exceeds price) as cost/price are entered, and a live stock-status preview chip (`Sin stock`, `Stock bajo`, `En stock`) as stock/minimum-stock are entered.

#### Scenario: Margin preview updates as cost and price change

- GIVEN cost = $5,500 and price = $8,500
- WHEN both fields have values
- THEN the margin preview MUST show "$3,000 (54.5%)"

#### Scenario: Negative margin is visually flagged

- GIVEN cost = $10,000 and price = $8,000
- WHEN the margin preview renders
- THEN it MUST use the negative/error styling

#### Scenario: Zero stock shows the "Sin stock" preview chip

- GIVEN stock = 0
- WHEN the stock preview renders
- THEN the chip MUST read "Sin stock" using error styling

### Requirement: CSV import is a collapsible section with documented columns

The product form MUST include a collapsible "Importar desde CSV" section showing expected columns (name, categoryId, costPrice, salePrice, currentStock, minimumStock, description, barcode, taxRate) and a file path field with an "Importar productos" action.

#### Scenario: Collapsible CSV section toggles open and closed

- GIVEN the CSV section is collapsed
- WHEN the operator clicks the section header
- THEN the section MUST expand to show the column hints and import controls
- AND clicking again MUST collapse it

### Requirement: Deleting a product or category requires confirmation feedback

Deleting a selected product or category MUST show a confirmation toast and refresh the corresponding list.

#### Scenario: Deleting a product refreshes the list

- GIVEN a product is selected with its delete action visible
- WHEN the operator confirms deletion
- THEN an info toast ("Producto eliminado") MUST appear
- AND the product list MUST no longer show that product
