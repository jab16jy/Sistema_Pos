# Product Catalog Specification

## Purpose

Defines the product catalog domain: creation, editing, search, barcode management, and stock threshold configuration.

## Requirements

### Requirement: Product creation

The system MUST allow creating products with name, price, barcode, minimum stock level, and a valid category.

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

### Requirement: Current stock is not editable from the product catalog

`CurrentStock` is master data that the catalog MAY display but MUST NOT edit directly. Ordinary
product editing MUST NOT change stock; all stock changes go through traceable inventory movements
(Inventario adjustments, Compras receipts, Ventas/Devoluciones/anulaciones). Product creation MAY
accept an initial balance, but the system MUST record it as an `InitialBalance` inventory movement
rather than writing `CurrentStock` directly. When no initial balance is provided, the product MUST
be created with zero stock. `MinimumStock` MUST remain editable from the catalog.

#### Scenario: Editing a product does not change stock

- GIVEN an existing product with current stock = 10
- WHEN the user edits the product and saves (any field)
- THEN the product's current stock MUST remain 10
- AND no inventory movement MUST be created by the edit

#### Scenario: Initial balance on creation is recorded as a movement

- GIVEN a user creates a product with an initial balance of 20 units
- WHEN the product is saved
- THEN the product MUST start at zero stock and reach 20 through exactly one `InitialBalance` inventory movement
- AND the movement MUST record the operator and quantity for traceability

#### Scenario: Creation without initial balance yields zero stock

- GIVEN a user creates a product without specifying an initial balance
- WHEN the product is saved
- THEN the product MUST be created with current stock = 0
- AND no inventory movement MUST be created

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

### Requirement: Category repository port

A port interface `ICategoryRepository` MUST exist in POS.Application with methods: `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`.

#### Scenario: Category repository lists all categories

- GIVEN categories "Bebidas" and "Carnes" exist
- WHEN `GetAllAsync` is called
- THEN both categories MUST be returned

### Requirement: Product category referential integrity

Every Product MUST reference an existing Category. The SQLite schema MUST enforce `PRODUCTS.CategoryId` → `CATEGORIES.Id` with restrictive deletion and an index on `PRODUCTS.CategoryId`.

#### Scenario: Reject an orphan product written directly to SQLite

- GIVEN no Category exists for a product's CategoryId
- WHEN a product is inserted directly into PRODUCTS
- THEN SQLite MUST reject the write

#### Scenario: Prevent deleting a referenced category

- GIVEN a Category has one or more associated Products
- WHEN a user attempts to delete the Category
- THEN SQLite MUST reject the deletion
- AND the associated Products MUST remain unchanged

### Requirement: CATEGORIES table canonical name

A reversible EF Core migration MUST rename the existing `Categories` table to `CATEGORIES`. All EF Core entity mappings MUST use `ToTable("CATEGORIES")`. The migration MUST include a Down step that renames `CATEGORIES` back to `Categories`.

#### Scenario: CATEGORIES table migration is reversible

- GIVEN the database has the CATEGORIES table (after migration applied)
- WHEN the EF Core migration is reversed (Down step executed)
- THEN the table is renamed back to "Categories"
- AND existing data is preserved

### Requirement: Category CRUD UI

A `CategoryViewModel` in POS.Presentation MUST expose a list of all categories, a form for creating and editing a category (Name required, max 100 chars), and commands: `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`. The form MUST bind the category's active state (`IsActive`) to a CheckBox. Validation messages MUST be visible inline when a command fails. The interface MUST dynamically toggle between "Nueva categoría" and "Editando categoría" headers based on whether a category is selected.

#### Scenario: New category created successfully

- GIVEN no category named "Bebidas" exists
- WHEN an administrator creates a new category with Name="Bebidas"
- THEN a Category record is persisted in the CATEGORIES table with Name="Bebidas"
- AND the category appears in the GetAllAsync list

#### Scenario: Category renamed successfully

- GIVEN category C1 with Name="Abarrotes" exists
- AND no category named "Víveres" exists
- WHEN an administrator renames C1 to "Víveres"
- THEN the CATEGORIES table record for C1 has Name="Víveres"
- AND all Products that reference C1 still reference C1 (FK unchanged)

#### Scenario: Bind active status checkbox in category form

- GIVEN the category "Bebidas" has active status set to true
- WHEN the user selects "Bebidas" in the category list
- THEN the active CheckBox in the category form MUST be checked

#### Scenario: Execute delete category command

- GIVEN category C3 has no products associated with it
- WHEN the user triggers the DeleteCategoryCommand for C3
- THEN the category record MUST be deleted from the database
- AND the categories list MUST reload automatically

### Requirement: Category deletion guarded against orphan products

The Application layer MUST check whether any Product references the category before deletion. If products exist, DeleteAsync MUST throw or return a domain error: "No se puede eliminar la categoría: tiene productos asociados".

#### Scenario: Category deletion rejected when products reference it

- GIVEN category C2 with Name="Carnes" has 3 products assigned to it
- WHEN an administrator attempts to delete C2
- THEN the Application layer returns an error: "No se puede eliminar la categoría: tiene productos asociados"
- AND C2 remains in the CATEGORIES table

#### Scenario: Category deletion succeeds when no products reference it

- GIVEN category C3 with Name="Test" has 0 products assigned to it
- WHEN an administrator deletes C3
- THEN the C3 record is removed from the CATEGORIES table
- AND C3 no longer appears in the GetAllAsync list

### Requirement: Category names must be unique

Attempting to create or rename a category to a name that already exists (case-insensitive comparison) MUST be rejected by the Application layer with an error: "Ya existe una categoría con ese nombre".

#### Scenario: Duplicate category name rejected

- GIVEN category "Lácteos" already exists
- WHEN an administrator attempts to create a new category with Name="lácteos" (different casing)
- THEN the Application layer returns an error: "Ya existe una categoría con ese nombre"
- AND no duplicate record is created in CATEGORIES

### Requirement: Category synchronization

The categories selection dropdown in the product editing interface MUST automatically update to reflect any creation, renaming, or deletion of a category in real time.

#### Scenario: Category dropdown synchronizes on creation

- GIVEN a user is viewing the product editing form
- WHEN a new category is created in the category editor
- THEN the product category selection dropdown MUST immediately include the new category
