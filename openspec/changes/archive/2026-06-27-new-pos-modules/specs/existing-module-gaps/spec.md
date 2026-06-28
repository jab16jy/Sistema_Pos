# Spec: Existing Module Gaps

**Change**: new-pos-modules
**Features**: invoice-management (partial returns), product-catalog (category CRUD), cashier-sales-flow (payment validation + loyalty), reporting-cash-closing (extended reports)
**Version**: 1.0
**Date**: 2026-06-27

---

## Purpose

Define what must be true after the four existing-module gap features are implemented. These are correctness and completeness gaps in modules that are already partially built.

---

## Feature 4: Invoice Management — Item-Level Partial Returns

### R-IR-01: SaleReturn entity exists as the return container

A `SaleReturn` entity must exist in POS.Domain.

**Attributes required:**
- `Id` (Guid)
- `OriginalSaleId` (Guid, FK to Sale)
- `ReturnDate` (DateTimeOffset)
- `ProcessedByUserId` (Guid, FK to User)
- `Reason` (string, required, max 500 chars)
- `TotalRefundAmount` (decimal) — sum of all SaleReturnItem.RefundAmount values
- `Items` (IReadOnlyList<SaleReturnItem>)

**Database constraint:** A unique index on `OriginalSaleId` is maintained, enforcing one SaleReturn per Sale. Attempting to create a second SaleReturn for the same Sale must be rejected.

---

### R-IR-02: SaleReturnItem entity records per-item return detail

A `SaleReturnItem` entity must exist in POS.Domain.

**Attributes required:**
- `Id` (Guid)
- `SaleReturnId` (Guid, FK to SaleReturn)
- `ProductId` (Guid, FK to Product)
- `OriginalQuantity` (int) — quantity in the original SaleItem
- `QuantityReturned` (int) — must be >= 1 and <= OriginalQuantity
- `UnitPrice` (decimal) — price from the original SaleItem
- `RefundAmount` (decimal) = `QuantityReturned × UnitPrice`

---

### R-IR-03: Return restores inventory atomically

When a SaleReturn is confirmed:
1. For each SaleReturnItem, an InventoryMovement of type RETURN (positive quantity) is created.
2. All InventoryMovements and the SaleReturn + SaleReturnItems are saved in a single database transaction.
3. If any part fails, the transaction is rolled back entirely.

---

### R-IR-04: Partial return business rules

- `QuantityReturned` per item must be >= 1.
- `QuantityReturned` per item must be <= the original SaleItem quantity.
- A SaleReturn must include at least one SaleReturnItem.
- The sum of returned quantities per product must not exceed what was sold in the original sale.
- A second SaleReturn for the same OriginalSaleId must be rejected at the Application layer (before reaching the database unique constraint).

---

### R-IR-05: Administrator authorization is required for returns

Initiating a partial return requires the current user to have the Administrator role. Cashier-role users must see an error message if they attempt to access the return workflow.

---

### Acceptance Scenarios — Invoice Management

#### Scenario IR-S1: Partial return of 2 out of 5 units of a single item

```
GIVEN Sale S1 contains SaleItem: ProductX, Qty=5, UnitPrice=$10,000 COP
AND no SaleReturn exists for S1
WHEN an Administrator creates a SaleReturn for S1 with:
  - SaleReturnItem: ProductX, QuantityReturned=2, UnitPrice=$10,000
  - Reason="Producto dañado"
THEN a SaleReturn record is persisted with OriginalSaleId=S1, TotalRefundAmount=$20,000
AND a SaleReturnItem record is persisted with QuantityReturned=2, RefundAmount=$20,000
AND an InventoryMovement of type RETURN with Quantity=+2 for ProductX is persisted
AND all records are saved in the same database transaction
AND the stock of ProductX increases by 2
```

#### Scenario IR-S2: Second return attempt for the same sale is rejected

```
GIVEN Sale S1 already has a SaleReturn (SaleReturn_A)
WHEN an Administrator attempts to create a second SaleReturn for S1
THEN the Application layer returns an error: "Ya existe una devolución registrada para esta venta"
AND no new SaleReturn record is created
AND the database unique index on OriginalSaleId remains intact
```

#### Scenario IR-S3: Return quantity exceeding original sale quantity is rejected

```
GIVEN Sale S1 contains SaleItem: ProductY, Qty=3, UnitPrice=$5,000 COP
WHEN an Administrator attempts a SaleReturn with SaleReturnItem: ProductY, QuantityReturned=4
THEN the Application layer returns a validation error: "La cantidad a devolver (4) supera la cantidad vendida (3) para ProductY"
AND no SaleReturn is persisted
```

#### Scenario IR-S4: Return with zero items is rejected

```
GIVEN Sale S1 with valid items
WHEN an Administrator submits a SaleReturn with an empty Items list
THEN the Application layer returns a validation error: "Debe incluir al menos un artículo en la devolución"
AND no SaleReturn is persisted
```

#### Scenario IR-S5: Cashier cannot initiate a return

```
GIVEN a user with role=Cashier is logged in
WHEN the cashier attempts to access the return workflow for any sale
THEN the Application layer returns an authorization error
AND the return screen is not accessible to the cashier
```

#### Scenario IR-S6: Return rollback when InventoryMovement insert fails

```
GIVEN a valid SaleReturn is being processed
WHEN the InventoryMovement insert raises a database error
THEN the entire transaction is rolled back
AND no SaleReturn, SaleReturnItem, or InventoryMovement record is persisted
AND the Application layer surfaces the error to the Presentation layer
```

---

## Feature 5: Product Catalog — Category CRUD UI

### R-PC-01: ICategoryRepository is defined in POS.Application

A port interface `ICategoryRepository` must exist in POS.Application with the following methods:
```
Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
Task<Category> CreateAsync(Category category, CancellationToken ct = default);
Task<Category> UpdateAsync(Category category, CancellationToken ct = default);
Task DeleteAsync(Guid categoryId, CancellationToken ct = default);
```

---

### R-PC-02: CATEGORIES table exists as the canonical table name

A reversible EF Core migration must rename the existing `Categories` table to `CATEGORIES`. After migration:
- All EF Core entity mappings must use `ToTable("CATEGORIES")`.
- The migration must include a Down step that renames `CATEGORIES` back to `Categories`.

---

### R-PC-03: CategoryViewModel exposes CRUD operations to the UI

A `CategoryViewModel` in POS.Presentation must expose:
- A list of all categories (bound to a list view).
- A form for creating and editing a category (Name required, max 100 chars).
- Commands: `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`.
- Validation messages visible inline when a command fails.

---

### R-PC-04: Category deletion is guarded against orphan products

The Application layer (not Presentation) must check whether any Product references the category before deletion. If products exist:
- DeleteAsync must throw or return a domain error: "No se puede eliminar la categoría: tiene productos asociados".
- No category is deleted when products reference it.

---

### R-PC-05: Category names must be unique (case-insensitive)

Attempting to create or rename a category to a name that already exists (case-insensitive comparison) must be rejected by the Application layer with an error: "Ya existe una categoría con ese nombre".

---

### Acceptance Scenarios — Product Catalog

#### Scenario PC-S1: New category created successfully

```
GIVEN no category named "Bebidas" exists
WHEN an administrator creates a new category with Name="Bebidas"
THEN a Category record is persisted in the CATEGORIES table with Name="Bebidas"
AND the category appears in the GetAllAsync list
AND the CategoryViewModel refreshes the list to include "Bebidas"
```

#### Scenario PC-S2: Category renamed successfully

```
GIVEN category C1 with Name="Abarrotes" exists
AND no category named "Víveres" exists
WHEN an administrator renames C1 to "Víveres"
THEN the CATEGORIES table record for C1 has Name="Víveres"
AND all Products that reference C1 still reference C1 (FK unchanged)
AND the CategoryViewModel displays "Víveres" in the list
```

#### Scenario PC-S3: Category deletion rejected when products reference it

```
GIVEN category C2 with Name="Carnes" has 3 products assigned to it
WHEN an administrator attempts to delete C2
THEN the Application layer returns an error: "No se puede eliminar la categoría: tiene productos asociados"
AND C2 remains in the CATEGORIES table
AND the 3 products retain their CategoryId reference
```

#### Scenario PC-S4: Category deletion succeeds when no products reference it

```
GIVEN category C3 with Name="Test" has 0 products assigned to it
WHEN an administrator deletes C3
THEN the C3 record is removed from the CATEGORIES table
AND C3 no longer appears in the GetAllAsync list
```

#### Scenario PC-S5: Duplicate category name rejected

```
GIVEN category "Lácteos" already exists
WHEN an administrator attempts to create a new category with Name="lácteos" (different casing)
THEN the Application layer returns an error: "Ya existe una categoría con ese nombre"
AND no duplicate record is created in CATEGORIES
```

#### Scenario PC-S6: CATEGORIES table migration is reversible

```
GIVEN the database has the CATEGORIES table (after migration applied)
WHEN the EF Core migration is reversed (Down step executed)
THEN the table is renamed back to "Categories"
AND existing data is preserved
```

---

## Feature 6: Cashier Sales Flow — Payment Platform Validation + Loyalty Integration

### R-CF-01: Payment platform is validated against a canonical list at the Application layer

The Application layer must define and enforce the canonical list of valid payment platforms:
- "Nequi"
- "Bancolombia"
- "Daviplata"
- "Efectivo"

**This list must be defined as an enum or a constant list in POS.Application**, not hardcoded in the ViewModel or in the Database.

Any payment submission with a platform value not in this list must be rejected with a validation error before the sale is persisted.

---

### R-CF-02: Payment platform validation rejects empty, null, or unrecognized values

The Application layer must reject:
- Null or empty platform string → error: "La plataforma de pago es requerida".
- A string that does not match the canonical list (case-sensitive) → error: "Plataforma de pago no válida: '{value}'. Válidas: Nequi, Bancolombia, Daviplata, Efectivo".

---

### R-CF-03: Customer association during checkout is optional

The cashier flow must allow associating a Client with a sale at any point before final payment confirmation. Association is optional. A sale without a Client proceeds normally with no loyalty side effects.

---

### R-CF-04: Points accumulate atomically with sale confirmation when customer is associated

When a sale with an associated Client is confirmed:
1. Point accumulation is triggered per R-LP-04 (see loyalty-points spec).
2. This must happen within the same database transaction as the sale save.
3. No separate "earn points" step is required from the cashier.

---

### R-CF-05: Loyalty redemption at checkout is optional and precedes sale total calculation

During checkout, if a Client is associated and has sufficient balance:
1. The cashier may optionally enter a redemption amount (in points).
2. The sale total is reduced by `RedeemedPoints × COPPerPoint` COP before payment is tendered.
3. The reduced total is the basis for payment validation (e.g., cash tendered must cover the reduced total).
4. Redemption rules from R-LP-06, R-LP-07, and R-LP-08 apply.

---

### Acceptance Scenarios — Cashier Sales Flow

#### Scenario CF-S1: Nequi payment accepted

```
GIVEN a sale in progress with total = $45,000 COP
AND the operator selects platform = "Nequi"
WHEN the sale is confirmed
THEN the Payment is persisted with Platform="Nequi"
AND the sale is saved successfully
```

#### Scenario CF-S2: Unknown platform rejected

```
GIVEN a sale in progress
AND the operator submits platform = "Efectivo2" (not in canonical list)
WHEN the Application layer validates the payment
THEN an error is returned: "Plataforma de pago no válida: 'Efectivo2'. Válidas: Nequi, Bancolombia, Daviplata, Efectivo"
AND no sale is persisted
AND the cashier screen displays the error message without crashing
```

#### Scenario CF-S3: Empty platform rejected

```
GIVEN a sale in progress
AND the operator submits platform = "" (empty string)
WHEN the Application layer validates the payment
THEN an error is returned: "La plataforma de pago es requerida"
AND no sale is persisted
```

#### Scenario CF-S4: Sale without customer association saves without loyalty side effects

```
GIVEN a sale in progress with no Client associated
WHEN the operator confirms the sale with platform = "Efectivo", Amount=$20,000 COP
THEN the sale is saved successfully
AND no LoyaltyTransaction is created
AND no loyalty error is surfaced
```

#### Scenario CF-S5: Points accumulate after sale with customer association

```
GIVEN a sale with Client associated (LoyaltyAccountId=AccountA, balance=0)
AND sale total = $35,000 COP
AND platform = "Daviplata"
WHEN the sale is confirmed
THEN the sale is persisted with ClientId associated
AND a LoyaltyTransaction EARN with Points=35 is persisted for AccountA within the same transaction
AND AccountA balance = 35 points
```

#### Scenario CF-S6: Loyalty redemption reduces sale total at checkout

```
GIVEN Client with balance = 200 points is associated to the current sale
AND sale total (before redemption) = $50,000 COP
AND operator redeems 100 points (= $100 COP discount)
WHEN the operator confirms the sale with platform = "Efectivo"
THEN effective total = $49,900 COP
AND REDEEM transaction (-100 pts) and EARN transaction (+FLOOR(49900*0.001) = +49 pts) are both persisted
AND Client balance = 200 - 100 + 49 = 149 points
AND Payment amount = $49,900 COP
```

---

## Feature 7: Reporting Cash Closing — Extended Reports

### R-RC-01: ReportsViewModel exposes top-sellers table (top 10)

The existing `ReportsViewModel` must include a top-sellers section:
- Default: top 10 products by units sold for the selected period.
- Columns displayed: Rank, Product Name, Units Sold, Total Revenue.
- Data loaded via `IProfitQueryService.GetTopSellingProductsAsync(period, topN: 10)`.

---

### R-RC-02: ReportsViewModel exposes period comparison panel

The existing `ReportsViewModel` must include a period comparison section:
- "Esta semana vs semana pasada" comparison.
- "Este mes vs mes pasado" comparison.
- Shows Revenue delta and Gross Margin delta with percentage.
- Data loaded via `IProfitQueryService.GetPeriodComparisonAsync`.

---

### R-RC-03: ReportsViewModel exposes payment platform breakdown table

The existing `ReportsViewModel` must include a platform breakdown section:
- Columns: Platform, Number of Transactions, Total Amount, % of Total.
- Data loaded via `IProfitQueryService.GetPaymentPlatformBreakdownAsync`.
- Restricted to the canonical platform list: Nequi, Bancolombia, Daviplata, Efectivo.

---

### R-RC-04: ReportsViewModel exposes gross margin per day table

The existing `ReportsViewModel` must include a gross margin per day section:
- Columns: Date, Total Revenue, Total Cost, Gross Margin, Margin %.
- Data loaded via `IProfitQueryService.GetGrossMarginByPeriodAsync` broken down by day.
- Covers the selected date range (default: current month).

---

### R-RC-05: All extended report data uses DB-side aggregation

All four extended report sections must source their data from `IProfitQueryService` implementations that use EF Core DB-side GroupBy (per R-PA-07). Loading full SaleItem or Payment rows into memory before aggregating is not permitted.

---

### Acceptance Scenarios — Reporting Cash Closing

#### Scenario RC-S1: Top-sellers table loads correctly for current week

```
GIVEN it is 2026-06-27 (Saturday) and the current week has sales
AND ProductA sold 30 units, ProductB sold 25 units, ... (10+ products with sales)
WHEN the user opens the Reports screen and views the Top Sellers section for "Esta Semana"
THEN the table shows exactly 10 rows
AND the first row shows ProductA with 30 units and correct revenue
AND rows are sorted by units sold descending
```

#### Scenario RC-S2: Period comparison shows revenue and margin deltas

```
GIVEN this week revenue = $1,200,000 COP, gross margin = $350,000 COP
AND last week revenue = $1,000,000 COP, gross margin = $300,000 COP
WHEN the user views the Period Comparison panel
THEN "Esta semana vs semana pasada" shows:
  Revenue: +$200,000 (+20.0%)
  Gross Margin: +$50,000 (+16.67%)
```

#### Scenario RC-S3: Payment breakdown table sums all platforms

```
GIVEN in the current month:
  Nequi: 15 transactions, $450,000 COP
  Bancolombia: 8 transactions, $240,000 COP
  Daviplata: 2 transactions, $60,000 COP
  Efectivo: 40 transactions, $1,250,000 COP
WHEN the user views the Payment Platform Breakdown table
THEN 4 rows are displayed (all platforms regardless of 0-count)
AND Efectivo shows $1,250,000 with the highest % of total
AND all percentages sum to 100%
```

#### Scenario RC-S4: Gross margin per day table shows daily rows

```
GIVEN the user selects "Este Mes" (June 2026) as the date range
AND sales exist on 15 of the 27 days in the month so far
WHEN the user views the Gross Margin per Day table
THEN 15 rows are displayed (only days with sales, or all 27 days with 0s — either is acceptable)
AND each row shows: Date, Revenue, Cost, Gross Margin, Margin %
AND Gross Margin = Revenue - Cost for each row
```

#### Scenario RC-S5: Extended report sections show empty state when no data exists

```
GIVEN the selected date range has no sales (e.g., future dates)
WHEN the user views any of the four extended report sections
THEN each section shows an empty-state message (e.g., "Sin datos para el período seleccionado")
AND no exception is thrown
AND Revenue/Margin values are shown as $0 or N/A where applicable
```

---

## Cross-Feature Constraint

All four existing-module gap features must not regress the existing behavior of their respective modules. Integration tests must confirm:
- Existing sale flow without customer or loyalty still completes correctly.
- Existing basic reports (daily summary, date range) still load correctly after OperationalReportQueryService rewrite.
- Existing invoice search, reprint, and detail view still function after SaleReturn entities are added.
- Existing product list still shows categories correctly after CATEGORIES table rename migration.
