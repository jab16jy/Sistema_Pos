# Tasks: new-pos-modules

**Change**: new-pos-modules
**Phase**: tasks
**Date**: 2026-06-27
**Total tasks**: 73 (2 pre-apply prerequisites + 10 Phase 1 + 28 Phase 2 + 20 Phase 3 + 15 Phase 4)
**Specs satisfied**: loyalty-points, profit-analytics, barcode-labels, existing-module-gaps (invoice-management, product-catalog, cashier-sales-flow, reporting-cash-closing)

---

## Pre-Apply Prerequisites (blocking — resolve before starting phases)

> These are not implementation tasks. They must be resolved before the indicated phases can begin.

- [PREREQUISITE — blocks Phase 3 barcode tasks (3.17–3.19)] Verify that NuGet package `ZXing.Net` version `0.16.10` with SkiaSharp binding builds successfully on .NET 9 WPF. Create a throwaway branch, add the package reference, confirm the build compiles and the SkiaSharp binding resolves before writing any barcode infrastructure code.

- [DECISION NEEDED — before Phase 3.2 migration is written] Decide how the first `LoyaltyRule` row is created: (a) a hardcoded seed row inside the `AddLoyaltyTables` migration (fastest, non-configurable) or (b) a Settings UI panel. Confirm the choice with the product owner. The migration must reflect this decision.

---

## Phase 1 — Domain Layer

**Execution note**: Tasks 1.1–1.5 can run in parallel (all new files). Tasks 1.6 → 1.7 are sequential (Sale depends on SaleItem). Task 1.8 depends on 1.5. Tests (1.9–1.10) are last after all entities.

---

### New entities and enums

- [ ] Task 1.1 — Create enum `src/POS.Domain/Enums/LoyaltyTransactionType.cs`
  - Values: `Earned = 1`, `Redeemed = 2`
  - Spec: R-LP-02

- [ ] Task 1.2 — Create entity `src/POS.Domain/Entities/LoyaltyAccount.cs`
  - Props: `Guid Id`, `Guid CustomerId` (unique FK to Customer), `DateTime CreatedAt`
  - Factory ctor: `LoyaltyAccount(Guid customerId)` — guard against `Guid.Empty`
  - No stored Balance column — balance is computed at query time via `SUM(Points)`
  - Spec: R-LP-01

- [ ] Task 1.3 — Create entity `src/POS.Domain/Entities/LoyaltyRule.cs`
  - Props: `Guid Id`, `decimal PointsPerCurrencyUnit` (default: 0.001 for 1 pt per 1,000 COP), `decimal RedemptionValuePerPoint`, `int MinimumRedeemPoints`, `bool IsActive`
  - Methods: `int PointsFor(Money saleTotal)` using `(int)Math.Floor(saleTotal.Amount * PointsPerCurrencyUnit)`; `Money DiscountFor(int points)` using `points * RedemptionValuePerPoint`
  - Spec: R-LP-03, R-LP-04, R-LP-05

- [ ] Task 1.4 — Create entity `src/POS.Domain/Entities/LoyaltyTransaction.cs`
  - Props: `Guid Id`, `Guid LoyaltyAccountId`, `LoyaltyTransactionType Type`, `int Points` (signed: + for earn, – for redeem), `Guid? SaleId`, `DateTime CreatedAt`
  - Ctor guards: `Type == Earned` requires `Points > 0`; `Type == Redeemed` requires `Points < 0`
  - Depends on: 1.1, 1.2
  - Spec: R-LP-02, R-LP-07

- [ ] Task 1.5 — Create entity `src/POS.Domain/Entities/SaleReturnItem.cs`
  - Props: `Guid Id`, `Guid SaleReturnId`, `Guid ProductId`, `string ProductName`, `Quantity QuantityReturned`, `Money UnitPrice`, `Money RefundAmount`
  - Ctor validates `QuantityReturned > 0` and `RefundAmount >= 0`
  - Spec: R-IR-02

### Modified entities

- [ ] Task 1.6 — Modify `src/POS.Domain/Entities/SaleItem.cs`
  - Add `Money UnitCost` property (private setter)
  - Add `unitCost` parameter to the existing ctor (after existing positional params)
  - Add `Money Profit()` helper: `(UnitPrice - UnitCost) * Quantity.Value - Discount.Amount` (returns Money)
  - Note: update any existing callers of the SaleItem ctor (in Sale.AddItem and test factories)
  - Spec: R-PA-02; design ADR #2

- [ ] Task 1.7 — Modify `src/POS.Domain/Entities/Sale.cs`
  - In `AddItem(Product product, Quantity qty, Money discount)`: capture `product.CostPrice` as the `unitCost` arg to `new SaleItem(..., unitCost: product.CostPrice)`
  - No other behavioural change; existing `Total()` is unchanged
  - Depends on: 1.6
  - Spec: R-PA-02

- [ ] Task 1.8 — Modify `src/POS.Domain/Entities/SaleReturn.cs`
  - Add `private readonly List<SaleReturnItem> _items = new()`
  - Add `IReadOnlyCollection<SaleReturnItem> Items => _items.AsReadOnly()`
  - Add `void AddItem(SaleReturnItem item)` — no duplicate ProductId check (same product returned once per event is valid)
  - Add `Money TotalRefund()` — `_items.Sum(i => i.RefundAmount.Amount)` wrapped in Money
  - Depends on: 1.5
  - Spec: R-IR-01, R-IR-02

### Domain layer tests

- [ ] Task 1.9 — Create `tests/POS.Domain.Tests/LoyaltyTests.cs`
  - `LoyaltyRule.PointsFor(1_000_000 COP at rate 0.001)` → 1000 pts
  - FLOOR rounding: 1_500 COP at 0.001 → 1 pt (not 2)
  - `LoyaltyRule.DiscountFor(500)` → `500 * RedemptionValuePerPoint` as Money
  - `LoyaltyTransaction(Type=Earned, Points=-1)` → throws `DomainException`
  - `LoyaltyTransaction(Type=Redeemed, Points=+1)` → throws `DomainException`
  - `LoyaltyAccount(Guid.Empty)` → throws `DomainException` or `ArgumentException`
  - Depends on: 1.1, 1.2, 1.3, 1.4
  - Spec: R-LP-03, R-LP-04 (unit coverage)

- [ ] Task 1.10 — Create/update `tests/POS.Domain.Tests/SaleReturnTests.cs` and update `SaleTests.cs`
  - `SaleReturn.AddItem` with qty=0 → throws
  - `SaleReturn.TotalRefund()` sums item `RefundAmount` values correctly
  - `Sale.AddItem` captures `product.CostPrice` in `SaleItem.UnitCost`
  - `SaleItem.Profit()` = `(UnitPrice - UnitCost) * Quantity - Discount` (happy path + zero-cost fallback)
  - Update any existing `SaleTests` that construct `SaleItem` directly to pass `unitCost`
  - Depends on: 1.5, 1.6, 1.7, 1.8
  - Spec: R-IR-02, R-PA-02

---

## Phase 2 — Application Layer

**Execution note**: Group A (interfaces, DTOs, common — tasks 2.1–2.11) all parallel. Group B (use cases — 2.12–2.21) sequential within feature groups; loyalty group (2.12–2.14), category group (2.15–2.17), barcode group (2.18–2.19), modifications (2.20–2.21) can run in parallel with each other. Group C (tests — 2.22–2.28) can start in parallel with Group B once Group A is complete.

---

### Group A — Interfaces, DTOs, and common (all parallel)

- [ ] Task 2.1 — Create interface `src/POS.Application/Interfaces/ILoyaltyRepository.cs`
  - `Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)`
  - `Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default)`
  - `Task AddTransactionAsync(LoyaltyTransaction tx, CancellationToken ct = default)`
  - `Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid accountId, int take, CancellationToken ct = default)`
  - `Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default)`
  - `Task<int> GetBalanceAsync(Guid accountId, CancellationToken ct = default)` // SUM(Points)
  - Spec: R-LP-01, R-LP-02, R-LP-03, R-LP-09

- [ ] Task 2.2 — Create interface `src/POS.Application/Interfaces/IProfitQueryService.cs`
  - `Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(ReportRangeRequest r, CancellationToken ct = default)`
  - `Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(ReportRangeRequest r, int top = 10, CancellationToken ct = default)`
  - `Task<PeriodComparisonResult> GetPeriodComparisonAsync(ReportRangeRequest current, ReportRangeRequest prior, CancellationToken ct = default)`
  - `Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(ReportRangeRequest r, CancellationToken ct = default)`
  - Spec: R-PA-01, R-RC-01..04

- [ ] Task 2.3 — Create interface `src/POS.Application/Interfaces/IBarcodeGenerationService.cs`
  - `byte[] Generate(string value, BarcodeSymbology symbology)` — synchronous (CPU-bound, no I/O)
  - `byte[] GenerateAuto(string value)` — EAN-13 if exactly 12 numeric chars; Code128 otherwise
  - Spec: R-BL-01, R-BL-02

- [ ] Task 2.4 — Create interface `src/POS.Application/Interfaces/ILabelPrintService.cs`
  - `Task<byte[]> BuildLabelsAsync(IReadOnlyList<LabelRequest> requests, CancellationToken ct = default)` — returns PDF bytes
  - `Task PrintAsync(IReadOnlyList<LabelRequest> requests, CancellationToken ct = default)`
  - Spec: R-BL-03

- [ ] Task 2.5 — Create interface `src/POS.Application/Interfaces/ICategoryRepository.cs`
  - `Task<IReadOnlyList<Category>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)`
  - `Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)`
  - `Task AddAsync(Category category, CancellationToken ct = default)`
  - `Task UpdateAsync(Category category, CancellationToken ct = default)`
  - `Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default)` // guard for delete
  - Spec: R-PC-01, R-PC-04

- [ ] Task 2.6 — Create `src/POS.Application/Common/PaymentPlatforms.cs`
  - `public static class PaymentPlatforms`
  - `public static readonly IReadOnlyList<string> All = ["Nequi", "Bancolombia", "Daviplata", "Efectivo"]`
  - `public static bool IsValid(string? platform)` — `All.Contains(platform, StringComparer.OrdinalIgnoreCase)`
  - Spec: R-CF-01, R-CF-02

- [ ] Task 2.7 — Create `src/POS.Application/DTOs/Loyalty/LoyaltyDtos.cs`
  - `record LoyaltyBalanceResult(int Balance, IReadOnlyList<LoyaltyTransactionResult> RecentTransactions)`
  - `record LoyaltyTransactionResult(Guid Id, string Type, int Points, DateTime CreatedAt, Guid? SaleId)`
  - `record EarnPointsRequest(Guid CustomerId, Guid SaleId, decimal EffectiveSaleTotal)`
  - `record RedeemPointsRequest(Guid CustomerId, int PointsToRedeem)`
  - `record RedeemPointsResult(bool Success, decimal DiscountApplied, string? ErrorMessage)`
  - Spec: R-LP-06, R-LP-07, R-LP-09

- [ ] Task 2.8 — Create `src/POS.Application/DTOs/Reporting/ProfitDtos.cs`
  - `record ProductMarginResult(Guid ProductId, string ProductName, decimal TotalRevenue, decimal TotalCost, decimal GrossMargin, decimal MarginPercent)`
  - `record TopSellerResult(Guid ProductId, string ProductName, int UnitsSold, decimal TotalRevenue)`
  - `record PeriodComparisonResult(decimal CurrentRevenue, decimal PriorRevenue, decimal RevenueDelta, decimal CurrentMargin, decimal PriorMargin)`
  - `record PlatformBreakdownResult(string Platform, decimal TotalAmount, int TransactionCount, decimal Percentage)`
  - Spec: R-PA-03..06

- [ ] Task 2.9 — Create `src/POS.Application/DTOs/Barcode/BarcodeDtos.cs`
  - `public enum BarcodeSymbology { Code128 = 1, Ean13 = 2 }`
  - `record LabelRequest(Guid ProductId, string ProductName, decimal Price, string BarcodeValue, int Copies = 1)`
  - Spec: R-BL-01, R-BL-05

- [ ] Task 2.10 — Create `src/POS.Application/DTOs/Products/CategoryDtos.cs`
  - `record CreateCategoryRequest(string Name)`
  - `record UpdateCategoryRequest(Guid Id, string Name)`
  - `record CategoryResult(Guid Id, string Name)`
  - Spec: R-PC-03

- [ ] Task 2.11 — Modify `src/POS.Application/DTOs/Invoices/InvoiceDtos.cs` (or locate the existing ProcessReturnRequest file)
  - Add `record ReturnLineRequest(Guid ProductId, int Quantity)`
  - Add `IReadOnlyList<ReturnLineRequest> Items` property to `ProcessReturnRequest`
  - Note: find the correct file path by searching for `ProcessReturnRequest` in the Application project
  - Spec: R-IR-02, R-IR-04

---

### Group B — Use Cases (loyalty, category, barcode in parallel; modifications sequential after their dependencies)

- [ ] Task 2.12 — Create `src/POS.Application/UseCases/Loyalty/AccumulateLoyaltyPointsUseCase.cs`
  - Deps injected: `ILoyaltyRepository`, `IUnitOfWork`
  - Flow: get active rule → skip if no rule; compute `points = rule.PointsFor(effectiveSaleTotal)`; skip if points == 0; create-or-get LoyaltyAccount for customer; `AddTransactionAsync(LoyaltyTransaction(Earned, +points, saleId))` within UoW
  - Does not call `SaveChanges` itself — caller (CreateSaleUseCase) owns the UoW boundary
  - Depends on: 2.1, 2.7
  - Spec: R-LP-04, R-LP-05

- [ ] Task 2.13 — Create `src/POS.Application/UseCases/Loyalty/RedeemLoyaltyPointsUseCase.cs`
  - Deps: `ILoyaltyRepository`, `IUnitOfWork`
  - Flow: get account; get balance; get rule; validate `pointsToRedeem <= balance` and `pointsToRedeem >= MinimumRedeemPoints`; reject with `RedeemPointsResult(false, ...)` if checks fail; `AddTransactionAsync(LoyaltyTransaction(Redeemed, -pointsToRedeem, saleId: null))`; return discount Money
  - Throws `DomainException` if attempted total < 0 (safety guard)
  - Depends on: 2.1, 2.7
  - Spec: R-LP-06, R-LP-07, R-LP-08

- [ ] Task 2.14 — Create `src/POS.Application/UseCases/Loyalty/GetLoyaltyBalanceUseCase.cs`
  - Deps: `ILoyaltyRepository`
  - Returns `LoyaltyBalanceResult(balance, lastN transactions)` where N=20
  - Returns null or empty result if customer has no LoyaltyAccount
  - Depends on: 2.1, 2.7
  - Spec: R-LP-09

- [ ] Task 2.15 — Create `src/POS.Application/UseCases/Categories/CreateCategoryUseCase.cs`
  - Deps: `ICategoryRepository`, `IUnitOfWork`
  - Validates name non-empty; checks `GetAllAsync` for case-insensitive duplicate → throws `DomainException("Category name already exists")`; creates `Category`; `AddAsync`; `SaveChanges`
  - Depends on: 2.5, 2.10
  - Spec: R-PC-04 (delete guard in repo), R-PC-05 (unique name)

- [ ] Task 2.16 — Create `src/POS.Application/UseCases/Categories/UpdateCategoryUseCase.cs`
  - Deps: `ICategoryRepository`, `IUnitOfWork`
  - Gets by Id (throws if not found); validates name unique excluding self; updates entity; `UpdateAsync`; `SaveChanges`
  - Depends on: 2.5, 2.10
  - Spec: R-PC-05

- [ ] Task 2.17 — Create `src/POS.Application/UseCases/Categories/SearchCategoriesUseCase.cs`
  - Deps: `ICategoryRepository`
  - Returns `IReadOnlyList<CategoryResult>` via `GetAllAsync`; maps entity → DTO
  - Depends on: 2.5, 2.10
  - Spec: R-PC-01

- [ ] Task 2.18 — Create `src/POS.Application/UseCases/Barcode/GenerateBarcodeUseCase.cs`
  - Deps: `IBarcodeGenerationService`
  - Validates `value` non-empty; delegates to `GenerateAuto(value)`; returns `byte[]` PNG
  - Wraps `Exception` from generation service in a user-friendly `ApplicationException`
  - Depends on: 2.3, 2.9
  - Spec: R-BL-01, R-BL-02

- [ ] Task 2.19 — Create `src/POS.Application/UseCases/Barcode/PrintLabelsUseCase.cs`
  - Deps: `ILabelPrintService`
  - Validates `requests` non-empty; validates each `Copies >= 1`; delegates to `PrintAsync`
  - On `Exception` from print service: wraps in `ApplicationException` (not crash)
  - Depends on: 2.4, 2.9
  - Spec: R-BL-03, R-BL-07

- [ ] Task 2.20 — Modify `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
  - Accept `ProcessReturnRequest.Items` (`IReadOnlyList<ReturnLineRequest>`)
  - Validate: `Items` count >= 1 (throw `DomainException` if empty)
  - For each item: validate `QuantityReturned` in [1, originalSaleItem.Quantity]; build `SaleReturnItem(productId, productName, qty, unitPrice, refundAmount)`
  - Call `saleReturn.AddItem(...)` for each valid line
  - Restore stock only for `QuantityReturned` (not full OriginalQuantity)
  - One-return-per-sale unique index check stays in place (EF will throw on duplicate OriginalSaleId)
  - Depends on: 2.11
  - Spec: R-IR-03, R-IR-04

- [ ] Task 2.21 — Modify `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs`
  - Add `PaymentPlatforms.IsValid(request.BankOrPlatform)` guard before `sale.Complete()`; throw `DomainException("Plataforma de pago no reconocida: {x}")` if invalid
  - Accept optional `ILoyaltyRepository? loyaltyRepository = null` ctor param (nullable to keep optional)
  - After `_unitOfWork.SaveChangesAsync()`, if `request.CustomerId != null && loyaltyRepository != null`: call `AccumulateLoyaltyPointsUseCase.ExecuteAsync(customerId, saleId, effectiveTotal)`
  - `SaleItem.UnitCost` is captured automatically by `sale.AddItem` in Domain (no change needed here beyond calling AddItem)
  - Depends on: 2.6, 2.12
  - Spec: R-CF-01, R-CF-02, R-CF-03, R-CF-04, R-LP-04

---

### Group C — Application layer tests (parallel with Group B after Group A)

- [ ] Task 2.22 — Create `tests/POS.Application.Tests/AccumulateLoyaltyPointsUseCaseTests.cs`
  - Points earned correctly per rule on sale close
  - Zero-point sale (e.g. very small total) creates no transaction record
  - Missing active rule → use case exits without error (no transaction)
  - No account → account created automatically
  - Spec: R-LP-04 (unit coverage)
  - Depends on: 2.12

- [ ] Task 2.23 — Create `tests/POS.Application.Tests/RedeemLoyaltyPointsUseCaseTests.cs`
  - Below-minimum points request → returns `(false, 0, error message)`
  - Over-balance request → returns `(false, 0, error message)`
  - Valid redemption → returns `(true, correctDiscount, null)`
  - Discount amount = `points * RedemptionValuePerPoint`
  - Spec: R-LP-06, R-LP-08
  - Depends on: 2.13

- [ ] Task 2.24 — Create `tests/POS.Application.Tests/CategoryUseCaseTests.cs`
  - `CreateCategoryUseCase`: duplicate name (case-insensitive) → throws `DomainException`
  - `UpdateCategoryUseCase`: rename to existing name of other category → throws
  - `SearchCategoriesUseCase`: returns all categories mapped to `CategoryResult`
  - `ICategoryRepository.HasProductsAsync`: delete guard scenario (checked at Application layer before delete)
  - Spec: R-PC-04, R-PC-05
  - Depends on: 2.15, 2.16, 2.17

- [ ] Task 2.25 — Create `tests/POS.Application.Tests/PaymentPlatformValidationTests.cs`
  - "Nequi" → `IsValid` returns `true`
  - "nequi" (lowercase) → `IsValid` returns `true`
  - "MercadoPago" → `IsValid` returns `false`; `CreateSaleUseCase` throws `DomainException`
  - null / empty → `IsValid` returns `false`; `CreateSaleUseCase` throws
  - Spec: R-CF-01, R-CF-02
  - Depends on: 2.6, 2.21

- [ ] Task 2.26 — Create `tests/POS.Application.Tests/BarcodeGenerationServiceTests.cs`
  - Mock `IBarcodeGenerationService` returning fake byte arrays
  - `GenerateBarcodeUseCase` with 12-digit numeric value → calls `GenerateAuto`
  - Empty value → throws `ApplicationException` (not crash)
  - Service failure → wrapped in `ApplicationException` (not rethrown raw)
  - Spec: R-BL-01, R-BL-02
  - Depends on: 2.18

- [ ] Task 2.27 — Update `tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs`
  - Partial return with 2 items (1 each) → only those quantities restored to stock
  - Empty `Items` list → `DomainException` thrown before DB write
  - `QuantityReturned > OriginalQuantity` → `DomainException` before DB write
  - Second return attempt for same sale → EF throws (unique index enforced)
  - Spec: R-IR-03, R-IR-04
  - Depends on: 2.20

- [ ] Task 2.28 — Update `tests/POS.Application.Tests/CreateSaleUseCaseTests.cs`
  - Sale with valid platform ("Efectivo") → saves successfully
  - Sale with invalid platform ("PayPal") → `DomainException` before save
  - After sale save with associated customer → `AccumulateLoyaltyPointsUseCase` invoked
  - `SaleItem.UnitCost` matches `Product.CostPrice` at time of sale (verify via persisted data)
  - Spec: R-CF-01, R-CF-02, R-CF-04, R-PA-02 (coverage)
  - Depends on: 2.21

---

## Phase 3 — Infrastructure Layer

**Execution note**: Migrations are strictly sequential (3.1 → 3.2 → 3.3). EF configurations (3.4–3.10) can be written in parallel with each other (and with migration writing). AppDbContext modification (3.11) must follow all configurations. Repositories and services (3.12–3.19) can run in parallel after 3.11. DI registration (3.20) is last.

---

### EF Migrations (sequential — must be added in this order)

- [ ] Task 3.1 — Create EF migration `RenameCategoriesTable`
  - File: `src/POS.Infrastructure/Migrations/{timestamp}_RenameCategoriesTable.cs`
  - `Up()`: `migrationBuilder.RenameTable(name: "Categories", newName: "CATEGORIES")`
  - `Down()`: `migrationBuilder.RenameTable(name: "CATEGORIES", newName: "Categories")`
  - This migration must run first so CategoryConfiguration's `ToTable("CATEGORIES")` matches
  - Spec: R-PC-02; design ADR #6

- [ ] Task 3.2 — Create EF migration `AddLoyaltyTables`
  - File: `src/POS.Infrastructure/Migrations/{timestamp}_AddLoyaltyTables.cs`
  - `Up()`:
    - Create `LOYALTY_ACCOUNTS` (Id TEXT PK, CustomerId TEXT NOT NULL UNIQUE, CreatedAt TEXT)
    - Create `LOYALTY_RULES` (Id TEXT PK, PointsPerCurrencyUnit REAL, RedemptionValuePerPoint REAL, MinimumRedeemPoints INT, IsActive INT)
    - Create `LOYALTY_TRANSACTIONS` (Id TEXT PK, LoyaltyAccountId TEXT NOT NULL FK → LOYALTY_ACCOUNTS, Type INT, Points INT, SaleId TEXT nullable, CreatedAt TEXT); index on `LoyaltyAccountId`
  - `Down()`: drop tables in reverse order (LOYALTY_TRANSACTIONS → LOYALTY_RULES → LOYALTY_ACCOUNTS)
  - If seeding decision = "seed row": add data migration for default LoyaltyRule in this migration's `Up()`
  - Depends on: 3.1 (sequential); [DECISION NEEDED] LoyaltyRule seeding
  - Spec: R-LP-01, R-LP-02, R-LP-03

- [ ] Task 3.3 — Create EF migration `AddSaleReturnItemsAndUnitCost`
  - File: `src/POS.Infrastructure/Migrations/{timestamp}_AddSaleReturnItemsAndUnitCost.cs`
  - `Up()`:
    - Create `SALE_RETURN_ITEMS` (Id TEXT PK, SaleReturnId TEXT NOT NULL FK → SALE_RETURNS, ProductId TEXT, ProductName TEXT, QuantityReturned INT, UnitPrice REAL, RefundAmount REAL)
    - Add column `UnitCost REAL NOT NULL DEFAULT 0` to `SALE_ITEMS` (legacy rows default 0)
  - `Down()`: drop `SALE_RETURN_ITEMS`; drop `UnitCost` column from `SALE_ITEMS`
  - Depends on: 3.2 (sequential)
  - Spec: R-IR-01, R-IR-02, R-PA-02 (column presence)

---

### EF Configurations (can write in parallel)

- [ ] Task 3.4 — Create `src/POS.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
  - `ToTable("CATEGORIES")`, `HasKey(c => c.Id)`, `Property(c => c.Name).IsRequired().HasMaxLength(200)`
  - Satisfies design ADR #6; ensures EF uses correct table name post-migration
  - Spec: R-PC-02

- [ ] Task 3.5 — Create `src/POS.Infrastructure/Persistence/Configurations/LoyaltyAccountConfiguration.cs`
  - `ToTable("LOYALTY_ACCOUNTS")`
  - Unique index on `CustomerId`: `HasIndex(a => a.CustomerId).IsUnique()`
  - FK to Customers table on `CustomerId`
  - Spec: R-LP-01

- [ ] Task 3.6 — Create `src/POS.Infrastructure/Persistence/Configurations/LoyaltyRuleConfiguration.cs`
  - `ToTable("LOYALTY_RULES")`
  - Map `RedemptionValuePerPoint` as decimal column
  - Spec: R-LP-03

- [ ] Task 3.7 — Create `src/POS.Infrastructure/Persistence/Configurations/LoyaltyTransactionConfiguration.cs`
  - `ToTable("LOYALTY_TRANSACTIONS")`
  - `HasOne` FK to `LOYALTY_ACCOUNTS` on `LoyaltyAccountId`
  - `HasIndex(t => t.LoyaltyAccountId)` for balance SUM performance
  - `Property(t => t.SaleId).IsRequired(false)` (nullable FK)
  - Spec: R-LP-02

- [ ] Task 3.8 — Create `src/POS.Infrastructure/Persistence/Configurations/SaleReturnItemConfiguration.cs`
  - `ToTable("SALE_RETURN_ITEMS")`
  - FK to `SALE_RETURNS` on `SaleReturnId`
  - Map `RefundAmount` using existing `Money` value converter convention (check `SaleItemConfiguration` for Money conversion pattern)
  - Map `UnitPrice` using same Money converter
  - Spec: R-IR-02

- [ ] Task 3.9 — Modify `src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs`
  - Add: `HasMany(r => r.Items).WithOne().HasForeignKey(i => i.SaleReturnId).OnDelete(DeleteBehavior.Cascade)`
  - Depends on: 3.8
  - Spec: R-IR-01

- [ ] Task 3.10 — Modify `src/POS.Infrastructure/Persistence/Configurations/SaleItemConfiguration.cs`
  - Map `UnitCost` property using the existing `Money` value converter (same pattern as `UnitPrice` in this file)
  - Use column name `UnitCost` with default value 0 for legacy rows
  - Depends on: 1.6 (SaleItem has the property)
  - Spec: R-PA-02

---

### AppDbContext modification

- [ ] Task 3.11 — Modify `src/POS.Infrastructure/Persistence/AppDbContext.cs`
  - Add `DbSet<LoyaltyAccount> LoyaltyAccounts { get; set; }`
  - Add `DbSet<LoyaltyRule> LoyaltyRules { get; set; }`
  - Add `DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }`
  - Add `DbSet<SaleReturnItem> SaleReturnItems { get; set; }`
  - Ensure all new configurations are discovered (if using `ApplyConfigurationsFromAssembly`, no action needed)
  - Depends on: 3.4–3.10 (configurations must exist first so EF model is valid)
  - Spec: design Infrastructure section

---

### Repositories and Services (parallel after 3.11)

- [ ] Task 3.12 — Create `src/POS.Infrastructure/Persistence/Repositories/LoyaltyRepository.cs`
  - Implements `ILoyaltyRepository`
  - `GetBalanceAsync`: `await _ctx.LoyaltyTransactions.Where(t => t.LoyaltyAccountId == accountId).SumAsync(t => t.Points)`
  - `GetTransactionsAsync`: `.Where(...).OrderByDescending(t => t.CreatedAt).Take(take).ToListAsync()`
  - `GetAccountByCustomerIdAsync`: `.FirstOrDefaultAsync(a => a.CustomerId == customerId)`
  - `GetActiveRuleAsync`: `.FirstOrDefaultAsync(r => r.IsActive)`
  - Depends on: 3.11, 2.1
  - Spec: R-LP-01, R-LP-09

- [ ] Task 3.13 — Create `src/POS.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
  - Implements `ICategoryRepository`
  - `GetAllAsync(activeOnly)`: queries `_ctx.Categories`; if `activeOnly`, filter by active flag (or return all if no soft-delete)
  - `HasProductsAsync(categoryId)`: `_ctx.Products.AnyAsync(p => p.CategoryId == categoryId)` — used by Application layer delete guard
  - Depends on: 3.11, 2.5
  - Spec: R-PC-01, R-PC-04

- [ ] Task 3.14 — Modify `src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs`
  - In `GetBySaleIdAsync` (or equivalent lookup method): add `.Include(r => r.Items)` to the query chain
  - Depends on: 3.9
  - Spec: R-IR-01

- [ ] Task 3.15 — Create `src/POS.Infrastructure/Persistence/Queries/ProfitQueryService.cs`
  - Implements `IProfitQueryService`
  - All methods use `IQueryable` GroupBy — no `ToList()` before aggregation
  - `GetMarginByProductAsync`: GroupBy `SaleItem.ProductId`, project `(SaleItem.UnitPrice, SaleItem.UnitCost, SaleItem.Quantity, SaleItem.Discount)` as raw columns (no domain Money methods in LINQ)
  - `GetTopSellersAsync`: GroupBy `ProductId`, `Sum(Quantity)`, order desc, `Take(top)`
  - `GetPeriodComparisonAsync`: two separate IQueryable aggregations over different date ranges
  - `GetPlatformBreakdownAsync`: GroupBy `Payment.BankOrPlatform` filtering to `PaymentPlatforms.All`; compute percentage client-side after scalar aggregate query
  - Depends on: 3.11, 2.2, 2.8
  - Spec: R-PA-01, R-PA-07, R-RC-01..04, R-RC-05

- [ ] Task 3.16 — Modify `src/POS.Infrastructure/Persistence/Queries/OperationalReportQueryService.cs`
  - Replace all `ToListAsync()` followed by in-memory LINQ aggregation with server-side EF GroupBy equivalents
  - Add platform breakdown method using `PaymentPlatforms.All` canonical list
  - Note: this is a prerequisite for profit analytics (R-PA-PRE-01); must be verified working before Task 3.15 is considered complete
  - Depends on: 3.11
  - Spec: R-PA-PRE-01, R-RC-05

- [ ] Task 3.17 — Add ZXing.Net NuGet reference (AFTER prerequisite verification)
  - `src/POS.Infrastructure/POS.Infrastructure.csproj`: add `<PackageReference Include="ZXing.Net" />`
  - Also add `<PackageReference Include="ZXing.Net.Bindings.SkiaSharp" />` if SkiaSharp binding is a separate package
  - `Directory.Packages.props`: add `<PackageVersion Include="ZXing.Net" Version="0.16.10" />` and binding version
  - Run `dotnet restore && dotnet build` to confirm .NET 9 build success before proceeding to 3.18
  - Depends on: [PREREQUISITE] ZXing.Net .NET 9 build verification
  - Spec: R-BL-04

- [ ] Task 3.18 — Create `src/POS.Infrastructure/Services/ZXingBarcodeGenerationService.cs`
  - Implements `IBarcodeGenerationService`
  - `Generate(value, symbology)`:
    ```csharp
    var writer = new ZXing.SkiaSharp.BarcodeWriter
    {
        Format = symbology == BarcodeSymbology.Ean13 ? BarcodeFormat.EAN_13 : BarcodeFormat.CODE_128,
        Options = new EncodingOptions { Width = 300, Height = 120, Margin = 4 }
    };
    using var bmp = writer.Write(value);
    using var img = SKImage.FromBitmap(bmp);
    return img.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    ```
  - `GenerateAuto(value)`: detect 12 pure numeric digits → EAN-13; else Code128
  - ZXing types must not leak outside this class (wrap in `try/catch`, rethrow as `ApplicationException`)
  - Depends on: 3.17, 2.3
  - Spec: R-BL-02, R-BL-04

- [ ] Task 3.19 — Create `src/POS.Infrastructure/Services/LabelPrintService.cs`
  - Implements `ILabelPrintService`
  - `BuildLabelsAsync`: QuestPDF `Document.Create(...)` with one section per label at 57×32mm; each label contains: product name (small text), barcode image from `IBarcodeGenerationService.GenerateAuto` (rendered as Image element), price (formatted string)
  - Repeat each label `request.Copies` times
  - `PrintAsync`: calls `BuildLabelsAsync` then sends PDF bytes to default printer (reuse pattern from existing `PrintService.cs` if available)
  - On any exception: wrap and throw as `ApplicationException` with message suitable for UI display
  - Depends on: 3.18, 2.4
  - Spec: R-BL-05, R-BL-07

---

### DI Registration (last in Phase 3)

- [ ] Task 3.20 — Modify `src/POS.Infrastructure/DependencyInjection.cs`
  - Add repository registrations:
    ```csharp
    services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
    services.AddScoped<ICategoryRepository, CategoryRepository>();
    ```
  - Add service registrations:
    ```csharp
    services.AddScoped<IProfitQueryService, ProfitQueryService>();
    services.AddScoped<IBarcodeGenerationService, ZXingBarcodeGenerationService>();
    services.AddScoped<ILabelPrintService, LabelPrintService>();
    ```
  - Add use case registrations:
    ```csharp
    services.AddScoped<AccumulateLoyaltyPointsUseCase>();
    services.AddScoped<RedeemLoyaltyPointsUseCase>();
    services.AddScoped<GetLoyaltyBalanceUseCase>();
    services.AddScoped<CreateCategoryUseCase>();
    services.AddScoped<UpdateCategoryUseCase>();
    services.AddScoped<SearchCategoriesUseCase>();
    services.AddScoped<GenerateBarcodeUseCase>();
    services.AddScoped<PrintLabelsUseCase>();
    ```
  - Depends on: 3.12–3.19, 2.12–2.19
  - Spec: design DI section

---

## Phase 4 — Presentation Layer

**Execution note**: New ViewModels (4.1, 4.2, 4.3) and modified ViewModels (4.4–4.7) can be developed in parallel. New Views (4.8, 4.9) depend on their respective ViewModels. Modified Views (4.10, 4.11) depend on their modified ViewModels. Tests (4.13–4.15) parallel with views. App.xaml.cs wiring (4.12) is last.

---

### New ViewModels

- [ ] Task 4.1 — Create `src/POS.Presentation/ViewModels/LoyaltyViewModel.cs`
  - Inherits `ViewModelBase`; no direct DB access
  - Props: `int Balance`, `CustomerResult? SelectedCustomer`, `ObservableCollection<LoyaltyTransactionResult> Transactions`, `string Message`, `bool IsLoading`
  - Commands:
    - `LoadBalanceCommand` (async): calls `GetLoyaltyBalanceUseCase`; populates `Balance` and `Transactions`; handles null (no account)
    - `RedeemCommand` (async): calls `RedeemLoyaltyPointsUseCase`; on success updates `Message`; on failure shows error in `Message`
  - Constructor deps: `GetLoyaltyBalanceUseCase`, `RedeemLoyaltyPointsUseCase`
  - Depends on: 2.13, 2.14
  - Spec: R-LP-09, R-LP-10

- [ ] Task 4.2 — Create `src/POS.Presentation/ViewModels/BarcodeViewModel.cs`
  - Inherits `ViewModelBase`
  - Props: `string BarcodeValue`, `ImageSource? PreviewImage`, `ObservableCollection<ProductLookupResult> SelectedProducts`, `int Copies` (default 1), `string Message`, `bool IsLoading`
  - Commands:
    - `GenerateCommand` (sync): calls `GenerateBarcodeUseCase.Execute(BarcodeValue)`; converts `byte[]` to `BitmapImage`; assigns to `PreviewImage`
    - `PrintSingleCommand` (async): builds single `LabelRequest` from `BarcodeValue` + selected product info; calls `PrintLabelsUseCase`
    - `PrintBulkCommand` (async): builds `LabelRequest` per `SelectedProducts`; calls `PrintLabelsUseCase`
  - Handles `ApplicationException` from use cases: sets `Message` (no crash)
  - Constructor deps: `GenerateBarcodeUseCase`, `PrintLabelsUseCase`, `IProductLookupService`
  - Depends on: 2.18, 2.19
  - Spec: R-BL-06, R-BL-07, R-BL-08

- [ ] Task 4.3 — Create `src/POS.Presentation/ViewModels/CategoriesViewModel.cs`
  - Inherits `ViewModelBase`
  - Props: `ObservableCollection<CategoryResult> Categories`, `CategoryResult? SelectedCategory`, `string Name`, `string Message`
  - Commands:
    - `LoadCommand` (async): calls `SearchCategoriesUseCase`; populates `Categories`
    - `SaveCommand` (async): if `SelectedCategory == null` → `CreateCategoryUseCase`; else → `UpdateCategoryUseCase`; then reload
    - `ClearCommand`: clears `SelectedCategory` and `Name`
  - Constructor deps: `SearchCategoriesUseCase`, `CreateCategoryUseCase`, `UpdateCategoryUseCase`
  - Depends on: 2.15, 2.16, 2.17
  - Spec: R-PC-03, R-PC-04

---

### Modified ViewModels

- [ ] Task 4.4 — Modify `src/POS.Presentation/ViewModels/ReportsViewModel.cs`
  - Add props: `ObservableCollection<TopSellerResult> TopSellers`, `PeriodComparisonResult? PeriodComparison`, `ObservableCollection<PlatformBreakdownResult> PlatformBreakdown`, `ObservableCollection<ProductMarginResult> ProductMargins`
  - Add `LoadAnalyticsCommand` (async): calls all four `IProfitQueryService` methods with the current `ReportRangeRequest`; populates collections
  - Add `IProfitQueryService` to constructor injection
  - Depends on: 2.2, 2.8 (DTOs), 3.15 (infra, but VM only knows the interface)
  - Spec: R-RC-01..05, R-PA-08

- [ ] Task 4.5 — Modify `src/POS.Presentation/ViewModels/CashierViewModel.cs`
  - Add `CustomerResult? SelectedCustomer` property (optional; null when no customer)
  - Add `decimal LoyaltyDiscount` property (read-only display)
  - Add `ApplyRedemptionCommand` (async): calls `RedeemLoyaltyPointsUseCase` with entered points; on success sets `LoyaltyDiscount`; on failure shows `Message`
  - Pass `SelectedCustomer?.Id` and `BankOrPlatform` string into `CreateSaleRequest` when confirming sale
  - `BankOrPlatform` binding must already exist or be added (string property for payment platform ComboBox)
  - Depends on: 2.6, 2.13, 2.21
  - Spec: R-CF-01..05

- [ ] Task 4.6 — Modify `src/POS.Presentation/ViewModels/ProductsViewModel.cs`
  - Add `ObservableCollection<CategoryResult> Categories` property
  - Add `Guid? SelectedCategoryId` property bound to product form
  - Load categories via `SearchCategoriesUseCase` on VM initialization (async init or `LoadCommand`)
  - Add `SearchCategoriesUseCase` to constructor deps
  - Depends on: 2.17
  - Spec: R-PC-03

- [ ] Task 4.7 — Modify `src/POS.Presentation/ViewModels/MainShellViewModel.cs`
  - Add `LoyaltyViewModel? Loyalty { get; init; }`
  - Add `BarcodeViewModel? Barcode { get; init; }`
  - Add sidebar/navigation entries for both (same null-check pattern as existing `Products`, `Invoices` properties)
  - Depends on: 4.1, 4.2
  - Spec: R-LP-10, R-BL-08

---

### New Views

- [ ] Task 4.8 — Create `src/POS.Presentation/Views/LoyaltyView.xaml` + `LoyaltyView.xaml.cs`
  - `DataContext` binds to `LoyaltyViewModel`
  - Layout: ComboBox for customer selection (bound `SelectedCustomer`); Label showing `Balance`; TextBox for points-to-redeem; "Aplicar Descuento" Button (bound `RedeemCommand`); DataGrid showing `Transactions` (last 20: Type, Points, Date, SaleId); TextBlock for `Message`
  - "Cargar" Button (bound `LoadBalanceCommand`)
  - Follow existing view style conventions (check `ProductsView.xaml` for patterns)
  - Depends on: 4.1
  - Spec: R-LP-09, R-LP-10

- [ ] Task 4.9 — Create `src/POS.Presentation/Views/BarcodeView.xaml` + `BarcodeView.xaml.cs`
  - `DataContext` binds to `BarcodeViewModel`
  - Layout: TextBox for `BarcodeValue`; Image control bound to `PreviewImage`; "Generar Vista Previa" Button (`GenerateCommand`); DataGrid for `SelectedProducts`; NumberBox/TextBox for `Copies`; "Imprimir Etiqueta" Button (`PrintSingleCommand`); "Imprimir Por Categoría" Button (`PrintBulkCommand`); TextBlock for `Message`
  - Depends on: 4.2
  - Spec: R-BL-06, R-BL-07, R-BL-08

---

### Modified Views

- [ ] Task 4.10 — Modify `src/POS.Presentation/Views/ProductsView.xaml`
  - Add a "Categorías" sub-tab (or sub-section) inside the existing Products screen containing:
    - DataGrid bound to `CategoriesViewModel.Categories`
    - Inline form: TextBox for `Name`, Save/Clear buttons
    - Message TextBlock
  - The sub-tab requires `CategoriesViewModel` to be available as a property on `ProductsViewModel` or the parent shell's DataContext
  - Add Category ComboBox to product create/edit form (bound to `ProductsViewModel.Categories`, selected value to `SelectedCategoryId`)
  - Depends on: 4.3, 4.6
  - Spec: R-PC-03

- [ ] Task 4.11 — Modify `src/POS.Presentation/Views/ReportsView.xaml`
  - Add "Análisis de Rentabilidad" section containing:
    - "Top 10 Productos" DataGrid (bound `TopSellers`): columns ProductName, UnitsSold, TotalRevenue
    - "Comparación de Períodos" panel (bound `PeriodComparison`): cards/labels for CurrentRevenue, PriorRevenue, RevenueDelta
    - "Desglose por Plataforma" table (bound `PlatformBreakdown`): Platform, TotalAmount, Percentage
    - "Margen por Producto" DataGrid (bound `ProductMargins`): ProductName, GrossMargin, MarginPercent
  - Add "Cargar Análisis" Button (bound `LoadAnalyticsCommand`)
  - Depends on: 4.4
  - Spec: R-RC-01..05, R-PA-08

---

### App.xaml.cs wiring (last in Phase 4)

- [ ] Task 4.12 — Modify `src/POS.Presentation/App.xaml.cs`
  - Resolve `LoyaltyViewModel` from DI (or compose manually) — wrap in try/catch nullable pattern (per existing `MainShellViewModel.Loyalty = ...` wiring style)
  - Resolve `BarcodeViewModel` from DI or compose manually — same try/catch pattern
  - Resolve `CategoriesViewModel` for use inside `ProductsViewModel` composition
  - Pass `IProfitQueryService` into `ReportsViewModel` constructor
  - Pass loyalty use cases into `CashierViewModel` constructor
  - Pass `SearchCategoriesUseCase` into `ProductsViewModel` constructor
  - Pass `LoyaltyViewModel` and `BarcodeViewModel` into `MainShellViewModel` via init props
  - Depends on: 3.20, 4.1–4.7
  - Spec: design Presentation section

---

### Presentation layer tests

- [ ] Task 4.13 — Create `tests/POS.Presentation.Shared.Tests/LoyaltyViewModelTests.cs`
  - `LoadBalanceCommand` sets `Balance` and populates `Transactions`
  - `LoadBalanceCommand` with no LoyaltyAccount → sets `Balance = 0`, empty `Transactions`, no exception
  - `RedeemCommand` on success → sets `Message` with positive confirmation
  - `RedeemCommand` on failure (over-balance) → sets `Message` with error string; no crash
  - Depends on: 4.1
  - Spec: design Testing table (Presentation)

- [ ] Task 4.14 — Create `tests/POS.Presentation.Shared.Tests/BarcodeViewModelTests.cs`
  - `GenerateCommand` with valid value → `PreviewImage` is non-null
  - `GenerateCommand` with empty value → `Message` shows error; `PreviewImage` remains null
  - `PrintBulkCommand` with no products selected → `Message` shows "no products" error; use case not invoked
  - `PrintBulkCommand` with products → `PrintLabelsUseCase` invoked with correct requests
  - Depends on: 4.2
  - Spec: design Testing table (Presentation)

- [ ] Task 4.15 — Update `tests/POS.Presentation.Shared.Tests/ReportsViewModelTests.cs`
  - `LoadAnalyticsCommand` populates `TopSellers` (count > 0 when data exists)
  - `PeriodComparison` is non-null after load
  - `PlatformBreakdown` has exactly 4 entries (or fewer when platforms have no data)
  - `ProductMargins` collection populates
  - Depends on: 4.4
  - Spec: R-RC-01..05

---

## Review Workload Forecast

| Phase | Scope | Estimated Changed Lines |
|-------|-------|------------------------|
| Phase 1 — Domain | 5 new entities/enums, 3 modified entities, 2 test files | ~500 |
| Phase 2 — Application | 5 interfaces, 1 common, 5 DTO files, 10 use cases (8 new + 2 modify), 7 test files | ~1,300 |
| Phase 3 — Infrastructure | 3 migrations, 5 new EF configs, 3 modified configs, 1 AppDbContext mod, 2 new repos, 3 modified repos/services, 3 new services, 2 .csproj mod, 1 DI mod | ~1,100 |
| Phase 4 — Presentation | 3 new VMs, 4 modified VMs, 2 new Views (XAML+cs), 2 modified Views, 1 App.xaml.cs mod, 3 test files | ~1,100 |
| **Total** | **73 tasks across 4 phases** | **~4,000** |

**400-line budget risk**: HIGH (10x the single-PR budget)

**Chained PRs recommended**: YES

**Suggested split (layer-aligned, 3 PRs)**:

| PR | Content | Estimated Lines | Gate |
|----|---------|----------------|------|
| PR 1 — Foundation | Phase 1 (Domain) + Phase 2 (Application) | ~1,800 | All domain invariants + use case contracts established; no DB changes |
| PR 2 — Persistence | Phase 3 (Infrastructure) — migrations, repos, services, DI | ~1,100 | Migrations applied; DB-side aggregation verified; ZXing builds |
| PR 3 — UI | Phase 4 (Presentation) — ViewModels, Views, App.xaml.cs wiring | ~1,100 | Full end-to-end user flows working |

Each PR is independently reviewable. PR 1 can be merged before PR 2 starts. PR 2 requires PR 1 merged (interfaces must exist). PR 3 requires PR 2 merged (DI registrations must be in place).

---

## Task Dependency Summary

```
Phase 1 (Domain) ──────────────────────────────────────────────────┐
  1.1-1.5 parallel                                                   │
  1.6 → 1.7 sequential                                               │
  1.8 after 1.5                                                      │
  1.9-1.10 after all entities                                        │
                                                                     ▼
Phase 2 (Application) ─────────────────────────────────────────────┐
  Group A (2.1-2.11) all parallel                                    │
  Group B (2.12-2.21) start after Group A; feature groups parallel   │
  Group C (2.22-2.28) parallel with Group B after Group A            │
                                                                     ▼
Phase 3 (Infrastructure) ──────────────────────────────────────────┐
  3.1 → 3.2 → 3.3 strictly sequential (migrations)                  │
  3.4-3.10 parallel (configurations)                                 │
  3.11 after all configurations                                      │
  3.12-3.16 parallel after 3.11                                      │
  3.17 → 3.18 → 3.19 sequential (barcode chain)                     │
  3.20 last (DI, after all above)                                    │
                                                                     ▼
Phase 4 (Presentation) ────────────────────────────────────────────┐
  4.1-4.3 parallel (new VMs)                                        │
  4.4-4.7 parallel (modified VMs)                                   │
  4.8 after 4.1; 4.9 after 4.2 (views after VMs)                   │
  4.10 after 4.3+4.6; 4.11 after 4.4                               │
  4.12 last (wiring)                                                │
  4.13-4.15 parallel with views (tests)                             │
```
