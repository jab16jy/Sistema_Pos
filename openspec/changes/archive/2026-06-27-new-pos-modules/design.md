# Design: New POS Modules (Loyalty, Profit, Barcode) + Module Gaps

## Technical Approach

All seven features follow the established Clean Architecture flow with strict inward dependencies: `Domain -> Application (ports) -> Infrastructure (adapters) -> Presentation (MVVM)`. No architectural change is introduced — each feature reuses existing patterns: entities with private setters and factory constructors, repository interfaces in `POS.Domain.Interfaces`, single-responsibility use cases injected via DI, `IEntityTypeConfiguration<T>` per entity, code-first migrations applied by `DatabaseSchemaRollout.MigrateAsync`, and `ViewModelBase`-derived ViewModels composed manually in `App.xaml.cs`. Work is grouped into 3 EF migrations. The only new third-party dependency is ZXing.Net (barcode imaging); QuestPDF (already referenced) renders label sheets.

## Architecture Decisions

### Decision: Loyalty balance is computed, never denormalized
**Choice**: Balance = `SUM(LoyaltyTransaction.Points)` for an account; no stored balance column.
**Alternatives considered**: Denormalized `Balance` column on `LoyaltyAccount` updated on each transaction.
**Rationale**: A stored balance can drift from its transaction ledger under partial failures. The transaction list is the single source of truth, matching the domain skill's "inventory as movements" principle. Volume per customer is low, so the aggregate query is cheap.

### Decision: SaleItem gains a UnitCost snapshot for margin accuracy
**Choice**: Add `Money UnitCost` to `SaleItem`, captured from `product.CostPrice` at `Sale.AddItem` time. Profit = `(UnitPrice - UnitCost) * Quantity - Discount`.
**Alternatives considered**: Join `SaleItem.ProductId -> Product.CostPrice` (current cost) at report time.
**Rationale**: Current cost gives wrong historical margins whenever a product's cost changes after the sale. Snapshotting is additive, low-risk, and makes the profit formula correct and auditable. Legacy rows (null/zero snapshot) fall back to current `Product.CostPrice` in the query service.

### Decision: Partial returns use one container + child items, unique index KEPT
**Choice**: Keep the `SALE_RETURNS.OriginalSaleId` unique index (one return event per sale). Add `SALE_RETURN_ITEMS` (qty + refund amount per line) owned by `SaleReturn`. `ProcessReturnUseCase` restores stock only for returned quantities.
**Alternatives considered**: Drop the unique index to allow many return events over time.
**Rationale**: Proposal scope is one return event per sale holding item-level detail; relaxing the index is out of scope. Container+items models partial refunds without breaking the existing invariant.

### Decision: Payment platforms are a canonical Application-layer list over Payment.BankOrPlatform
**Choice**: `PaymentPlatforms` static readonly list (`Nequi`, `Bancolombia`, `Daviplata`, `Efectivo`) in `POS.Application`. Validated against the existing `Payment.BankOrPlatform` string. The `PaymentMethod` enum (Cash/DebitCard/CreditCard/Transfer) is NOT changed.
**Alternatives considered**: Add platforms as new `PaymentMethod` enum values; a DB-backed configurable table.
**Rationale**: Platforms are an orthogonal reporting/validation dimension already modeled by `BankOrPlatform`; conflating them with `PaymentMethod` (which drives cash-vs-digital drawer logic) would corrupt cash-closing math. Hardcoded list matches the proposal's "fixed canonical list" and avoids a migration.

### Decision: Profit/top-seller/comparison aggregation pushed to SQLite via EF GroupBy
**Choice**: New `IProfitQueryService` builds `IQueryable` GroupBy projections translated to SQL. `OperationalReportQueryService` is rewritten to stop `ToListAsync`-then-LINQ-in-memory.
**Alternatives considered**: Keep loading all sales into memory and aggregating client-side.
**Rationale**: In-memory loading does not scale and the proposal flags it as a perf risk. EF GroupBy executes server-side. `Money` value-object conversions and `Sale.Total()` domain methods are not SQL-translatable, so the query service projects raw columns (`UnitPrice`, `UnitCost`, `Quantity`, `Discount`) directly rather than calling domain methods.

### Decision: Category table rename via new configuration + reversible migration
**Choice**: Add `CategoryConfiguration : IEntityTypeConfiguration<Category>` with `ToTable("CATEGORIES")` and `Id`/`Name` mapping. Migration renames `Categories -> CATEGORIES`.
**Alternatives considered**: Leave default `Categories` table name.
**Rationale**: `Category` currently has no explicit configuration (default convention name). The proposal standardizes on SCREAMING_SNAKE_CASE; `RenameTable` is reversible in `Down()`.

### Decision: ZXing.Net imaging + QuestPDF label sheets; symbology chosen by content
**Choice**: `IBarcodeGenerationService` (Application port) implemented by `ZXingBarcodeGenerationService` (Infrastructure) — Code128 default, EAN-13 when value is 12-13 numeric digits, returns `byte[]` PNG. `ILabelPrintService` composes labels (name + price + barcode image) into a QuestPDF document reusing the existing PDF/print path.
**Alternatives considered**: Direct ESC/POS label commands (out of scope per proposal); System.Drawing imaging (poor cross-platform .NET 9 support).
**Rationale**: ZXing.Net with SkiaSharp backend is .NET 9 compatible and pure-managed. QuestPDF is already a dependency. Symbology auto-selection keeps the cashier flow simple.

### Decision: Loyalty and Barcode are new top-level sidebar entries
**Choice**: Add `Loyalty` and `Barcode` nullable ViewModel properties to `MainShellViewModel` (same `init` pattern as `Products`, `Invoices`). Reports/Cashier are extended, not replaced.
**Rationale**: Both are first-class modules per the domain skill (modules 5 and 11). Reuses the existing nullable-VM navigation pattern; no shell rewrite.

## Data Flow

```
Sale close (CashierVM) ──> CreateSaleUseCase ──> AccumulateLoyaltyPointsUseCase
       │                          │                        │
       │                    SaleItem.UnitCost        LoyaltyTransaction(+earned)
       ▼                          ▼                        ▼
   Payment.BankOrPlatform   ProfitQueryService      LoyaltyRepository
   (validated vs list)       (EF GroupBy → SQLite)        │
                                   │                 balance = SUM(points)
                                   ▼                        ▼
                             ReportsVM tables        CustomersVM balance view

Checkout redeem: CashierVM ──> RedeemLoyaltyPointsUseCase ──> LoyaltyTransaction(-redeemed)
                                          └──> discount applied to sale total

Barcode: BarcodeVM ──> IBarcodeGenerationService (PNG) ──> ILabelPrintService (QuestPDF) ──> print
```

## File Changes

### 1. Domain Layer (`POS.Domain`)

| File | Action | Detail |
|------|--------|--------|
| `Entities/LoyaltyAccount.cs` | Create | `Id`, `CustomerId` (unique), `CreatedAt`. Ctor `(Guid customerId)`. No stored balance. |
| `Entities/LoyaltyRule.cs` | Create | `Id`, `PointsPerCurrencyUnit` (e.g. 1 pt / 1000 COP), `RedemptionValuePerPoint`, `MinimumRedeemPoints`, `IsActive`. Methods: `PointsFor(Money amount)`, `DiscountFor(int points)`. |
| `Entities/LoyaltyTransaction.cs` | Create | `Id`, `LoyaltyAccountId`, `Type` (enum Earned/Redeemed), `Points` (signed: + earn, − redeem), `SaleId?`, `CreatedAt`. Ctor guards: redeem points negative, earn positive. |
| `Enums/LoyaltyTransactionType.cs` | Create | `Earned = 1, Redeemed = 2`. |
| `Entities/SaleReturnItem.cs` | Create | `Id`, `SaleReturnId`, `ProductId`, `ProductName`, `Quantity`, `RefundAmount` (Money). Ctor validates qty > 0. |
| `Entities/SaleReturn.cs` | Modify | Add `private List<SaleReturnItem> _items`; `IReadOnlyCollection<SaleReturnItem> Items`; `AddItem(...)`; `TotalRefund()`. |
| `Entities/SaleItem.cs` | Modify | Add `Money UnitCost`; ctor param `unitCost`; `Profit()` helper optional. |
| `Entities/Sale.cs` | Modify | `AddItem` captures `product.CostPrice` into `SaleItem.UnitCost`. |

### 2. Application Layer (`POS.Application`)

| File | Action | Detail |
|------|--------|--------|
| `Interfaces/ILoyaltyRepository.cs` | Create | `GetAccountByCustomerIdAsync`, `AddAccountAsync`, `AddTransactionAsync`, `GetTransactionsAsync(accountId)`, `GetActiveRuleAsync`, `GetBalanceAsync(accountId)`. |
| `Interfaces/IProfitQueryService.cs` | Create | `GetMarginByProductAsync(ReportRangeRequest)`, `GetMarginByPeriodAsync(...)`, `GetTopSellersAsync(range, int top=10)`, `GetPeriodComparisonAsync(...)`, `GetPlatformBreakdownAsync(range)`. All `Task<...>` async, project to DTOs. |
| `Interfaces/IBarcodeGenerationService.cs` | Create | `byte[] Generate(string value, BarcodeSymbology symbology)`; `byte[] GenerateAuto(string value)`. |
| `Interfaces/ILabelPrintService.cs` | Create | `Task<byte[]> BuildLabelsAsync(IReadOnlyList<LabelRequest>)`, `Task PrintAsync(...)`. |
| `Interfaces/ICategoryRepository.cs` | Create | `GetAllAsync(bool activeOnly)`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`. |
| `Common/PaymentPlatforms.cs` | Create | `static readonly IReadOnlyList<string>` canonical list + `IsValid(string)`. |
| `DTOs/Loyalty/LoyaltyDtos.cs` | Create | `LoyaltyBalanceResult`, `EarnPointsRequest`, `RedeemPointsRequest`, `LoyaltyTransactionResult`. |
| `DTOs/Reporting/ProfitDtos.cs` | Create | `ProductMarginResult`, `PeriodMarginResult`, `TopSellerResult`, `PeriodComparisonResult`, `PlatformBreakdownResult`. |
| `DTOs/Barcode/BarcodeDtos.cs` | Create | `LabelRequest(ProductId, Name, Price, BarcodeValue, Copies)`, `BarcodeSymbology` enum. |
| `DTOs/Products/CategoryDtos.cs` | Create | `CreateCategoryRequest`, `UpdateCategoryRequest`, `CategoryResult`. |
| `UseCases/Loyalty/AccumulateLoyaltyPointsUseCase.cs` | Create | deps: `ILoyaltyRepository`, `IUnitOfWork`. Earns points on completed sale per active rule. |
| `UseCases/Loyalty/RedeemLoyaltyPointsUseCase.cs` | Create | deps: `ILoyaltyRepository`, `IUnitOfWork`. Validates balance ≥ min, writes negative transaction, returns discount. |
| `UseCases/Loyalty/GetLoyaltyBalanceUseCase.cs` | Create | deps: `ILoyaltyRepository`. |
| `UseCases/Categories/CreateCategoryUseCase.cs` | Create | deps: `ICategoryRepository`, `IUnitOfWork`. |
| `UseCases/Categories/UpdateCategoryUseCase.cs` | Create | same deps. |
| `UseCases/Categories/SearchCategoriesUseCase.cs` | Create | deps: `ICategoryRepository`. |
| `UseCases/Barcode/GenerateBarcodeUseCase.cs` | Create | deps: `IBarcodeGenerationService`. |
| `UseCases/Barcode/PrintLabelsUseCase.cs` | Create | deps: `ILabelPrintService`. |
| `UseCases/Invoices/ProcessReturnUseCase.cs` | Modify | Accept `ProcessReturnRequest.Items` (list of `{ProductId, Quantity}`); build `SaleReturnItem`s; restore stock per returned qty only. |
| `DTOs/Invoices/InvoiceDtos.cs` | Modify | `ProcessReturnRequest` gains `IReadOnlyList<ReturnLineRequest> Items`. |
| `UseCases/Sales/CreateSaleUseCase.cs` | Modify | Optional `ILoyaltyRepository?` dep; validate `BankOrPlatform` vs `PaymentPlatforms`; call accumulate-points after `Complete()`. |

### 3. Infrastructure Layer (`POS.Infrastructure`)

| File | Action | Detail |
|------|--------|--------|
| `Persistence/AppDbContext.cs` | Modify | Add DbSets: `LoyaltyAccounts`, `LoyaltyRules`, `LoyaltyTransactions`, `SaleReturnItems`. |
| `Persistence/Configurations/CategoryConfiguration.cs` | Create | `ToTable("CATEGORIES")`, `Id` string conversion, `Name` required. |
| `Persistence/Configurations/LoyaltyAccountConfiguration.cs` | Create | `LOYALTY_ACCOUNTS`, unique index on `CustomerId`. |
| `Persistence/Configurations/LoyaltyRuleConfiguration.cs` | Create | `LOYALTY_RULES`, Money conversions. |
| `Persistence/Configurations/LoyaltyTransactionConfiguration.cs` | Create | `LOYALTY_TRANSACTIONS`, FK to account, index on `LoyaltyAccountId`, `SaleId` nullable. |
| `Persistence/Configurations/SaleReturnItemConfiguration.cs` | Create | `SALE_RETURN_ITEMS`, FK to `SALE_RETURNS`, Money conversion for `RefundAmount`. |
| `Persistence/Configurations/SaleReturnConfiguration.cs` | Modify | `OwnsMany`/`HasMany` items relationship. |
| `Persistence/Configurations/SaleItemConfiguration.cs` | Modify | Map `UnitCost` Money column. |
| `Persistence/Repositories/LoyaltyRepository.cs` | Create | implements `ILoyaltyRepository`; `GetBalanceAsync` = `SumAsync(Points)`. |
| `Persistence/Repositories/CategoryRepository.cs` | Create | implements `ICategoryRepository`. |
| `Persistence/Repositories/SaleReturnRepository.cs` | Modify | `GetBySaleIdAsync` includes `.Include(r => r.Items)`. |
| `Persistence/Queries/ProfitQueryService.cs` | Create | implements `IProfitQueryService` via EF GroupBy → SQLite. |
| `Persistence/Queries/OperationalReportQueryService.cs` | Modify | Replace in-memory load with server-side aggregation; add platform breakdown. |
| `Services/ZXingBarcodeGenerationService.cs` | Create | implements `IBarcodeGenerationService` using ZXing.Net SkiaSharp `BarcodeWriter`. |
| `Services/LabelPrintService.cs` | Create | implements `ILabelPrintService` via QuestPDF `Document`. |
| `POS.Infrastructure.csproj` | Modify | Add `<PackageReference Include="ZXing.Net" />` (versionless, CPM). |
| `Directory.Packages.props` | Modify | Add `<PackageVersion Include="ZXing.Net" Version="0.16.10" />` (verify .NET 9 build before apply). |
| `DependencyInjection.cs` | Modify | Register new repositories, services, use cases. |

### 4. Presentation Layer (`POS.Presentation` / `POS.Presentation.Shared`)

| File | Action | Detail |
|------|--------|--------|
| `ViewModels/LoyaltyViewModel.cs` | Create | Props: `Balance`, `SelectedCustomer`, `Transactions` (ObservableCollection), `Message`. Commands: `LoadBalanceCommand`, `RedeemCommand` (async). Deps: `GetLoyaltyBalanceUseCase`, `RedeemLoyaltyPointsUseCase`. |
| `ViewModels/BarcodeViewModel.cs` | Create | Props: `BarcodeValue`, `PreviewImage` (byte[]/ImageSource), `SelectedProducts`, `Copies`, `Message`. Commands: `GenerateCommand`, `PrintSingleCommand`, `PrintBulkCommand` (async). Deps: `GenerateBarcodeUseCase`, `PrintLabelsUseCase`, `IProductLookupService`. |
| `ViewModels/CategoriesViewModel.cs` | Create | Props: `Categories`, `SelectedCategory`, `Name`, `Message`. Commands: `Load`, `Save`, `Clear`. Deps: `SearchCategoriesUseCase`, `CreateCategoryUseCase`, `UpdateCategoryUseCase`. |
| `ViewModels/ReportsViewModel.cs` | Modify | Add `TopSellers`, `PeriodComparison`, `PlatformBreakdown`, `ProductMargins` collections + `LoadAnalyticsCommand`; inject `IProfitQueryService` (or a new `GetProfitReportUseCase`). |
| `ViewModels/CashierViewModel.cs` | Modify | Add optional customer select + `LoyaltyDiscount`, `ApplyRedemptionCommand`; pass `BankOrPlatform` and customer to `CreateSaleUseCase`. |
| `ViewModels/ProductsViewModel.cs` | Modify | Bind `CategoryId` to category list (populate from `SearchCategoriesUseCase`). |
| `ViewModels/MainShellViewModel.cs` | Modify | Add `Loyalty`, `Barcode` nullable `init` props. |
| `Views/LoyaltyView.xaml(.cs)` | Create | DataGrid (transactions), customer ComboBox, balance label, Redeem button. |
| `Views/BarcodeView.xaml(.cs)` | Create | TextBox (value), Image (preview), product DataGrid, Copies spinner, Generate/Print buttons. |
| `Views/ProductsView.xaml` | Modify | Add Categories sub-tab (DataGrid + form) and category ComboBox in product form. |
| `Views/ReportsView.xaml` | Modify | Add top-sellers DataGrid, comparison cards, platform-breakdown table, margin column. |
| `App.xaml.cs` | Modify | Resolve and wire new ViewModels with the same try/catch nullable pattern. |

## Interfaces / Contracts

```csharp
public interface ILoyaltyRepository
{
    Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default);
    Task AddTransactionAsync(LoyaltyTransaction tx, CancellationToken ct = default);
    Task<int> GetBalanceAsync(Guid accountId, CancellationToken ct = default); // SUM(Points)
    Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default);
}

public interface IProfitQueryService
{
    Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(ReportRangeRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(ReportRangeRequest r, int top = 10, CancellationToken ct = default);
    Task<PeriodComparisonResult> GetPeriodComparisonAsync(ReportRangeRequest current, ReportRangeRequest prior, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(ReportRangeRequest r, CancellationToken ct = default);
}

public interface IBarcodeGenerationService
{
    byte[] Generate(string value, BarcodeSymbology symbology);
    byte[] GenerateAuto(string value); // EAN-13 if 12–13 numeric digits, else Code128
}
```

ZXing implementation sketch:
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

## Testing Strategy

| Layer | Test class | Asserts |
|-------|-----------|---------|
| Domain | `LoyaltyTests` | rule `PointsFor` rounds correctly; redeem transaction stores negative points; account requires customer. |
| Domain | `SaleReturnTests` | `AddItem` accumulates; `TotalRefund` sums; qty > 0 guard. |
| Domain | `SaleItemTests` | `UnitCost` captured; `Profit()` = (price−cost)×qty−discount. |
| Application | `AccumulateLoyaltyPointsUseCaseTests` | points earned per rule on sale close; no double-earn. |
| Application | `RedeemLoyaltyPointsUseCaseTests` | rejects below-minimum; rejects over-balance; discount computed. |
| Application | `ProcessReturnUseCaseTests` (update) | partial return restores only returned qty; one-return-per-sale still enforced. |
| Application | `ProfitQueryServiceTests` | margin per product/period; top-10 ordering; platform breakdown; uses SQLite in-memory/test db. |
| Application | `CategoryUseCaseTests` | create/update/search against `CATEGORIES`. |
| Application | `PaymentPlatformValidationTests` | invalid platform rejected at sale. |
| Application | `BarcodeGenerationServiceTests` | EAN-13 chosen for 12–13 digits; Code128 fallback; non-empty PNG. |
| Presentation | `LoyaltyViewModelTests` | load balance, redeem command updates message. |
| Presentation | `BarcodeViewModelTests` | generate sets preview; bulk print invokes use case. |
| Presentation | `ReportsViewModelTests` (update) | analytics collections populate. |

Update existing: `CreateSaleUseCaseTests` (UnitCost snapshot + platform validation), `InvoiceUseCaseTests` (partial return shape), `ReportingUseCaseTests` (new aggregation), `SalePersistenceTests` (SaleItem column). No mocks for DB — use in-memory/test SQLite per convention.

## Migration / Rollout

Code-first, applied by `DatabaseSchemaRollout.MigrateAsync` at startup. Three migrations in order:

1. `RenameCategoriesTable` — rename `Categories` → `CATEGORIES` (reversible `RenameTable` in `Down`).
2. `AddLoyaltyTables` — create `LOYALTY_ACCOUNTS` (unique `CustomerId`), `LOYALTY_RULES`, `LOYALTY_TRANSACTIONS` (FK + index).
3. `AddSaleReturnItemsAndUnitCost` — create `SALE_RETURN_ITEMS` (FK to `SALE_RETURNS`); add `UnitCost` column to `SALE_ITEMS` (default 0 for legacy rows).

All additive/reversible; per-feature rollback = revert commit + reverse migration.

### DI registrations (`DependencyInjection.cs`)
```csharp
services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
services.AddScoped<ICategoryRepository, CategoryRepository>();
services.AddScoped<IProfitQueryService, ProfitQueryService>();
services.AddScoped<IBarcodeGenerationService, ZXingBarcodeGenerationService>();
services.AddScoped<ILabelPrintService, LabelPrintService>();
services.AddScoped<AccumulateLoyaltyPointsUseCase>();
services.AddScoped<RedeemLoyaltyPointsUseCase>();
services.AddScoped<GetLoyaltyBalanceUseCase>();
services.AddScoped<CreateCategoryUseCase>();
services.AddScoped<UpdateCategoryUseCase>();
services.AddScoped<SearchCategoriesUseCase>();
services.AddScoped<GenerateBarcodeUseCase>();
services.AddScoped<PrintLabelsUseCase>();
// ViewModels are composed manually in App.xaml.cs (no DI registration), per existing pattern.
```

## Open Questions

- [ ] **Loyalty rule seeding**: who creates the first `LoyaltyRule`? Recommend a Settings UI field or a seed row in `AddLoyaltyTables`. Needs product confirmation.
- [ ] **Redemption + earning interaction**: do redeemed (discounted) lines still earn points? Assumed points earned on net total after redemption. Confirm.
- [ ] **ZXing.Net package id/version**: `ZXing.Net` 0.16.x with a SkiaSharp binding must be build-verified on .NET 9 WPF before apply (risk flagged in proposal).
- [ ] **Legacy margin accuracy**: pre-migration `SALE_ITEMS` have `UnitCost = 0`; report falls back to current `Product.CostPrice`, which is approximate for historical rows.
