# Tasks: Complete Runtime Gaps

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1800 lines |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain+Application) → PR 2 (Infrastructure) → PR 3 (Presentation) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain entities, interfaces, use cases | PR 1 | Foundation layer, no dependencies |
| 2 | Repositories, DbContext, DI registration | PR 2 | Depends on PR 1 |
| 3 | ViewModels, Views, shell integration | PR 3 | Depends on PR 2 |

## Phase 1: Domain & Application Layer

- [x] 1.1 Create `src/POS.Domain/Entities/SaleReturn.cs` with OriginalSaleId, ProcessedByUserId, ReturnedAt, Reason
- [x] 1.2 Create `src/POS.Domain/Entities/BusinessSettings.cs` with BusinessName, BusinessAddress, TaxId, PrinterCommand, PrinterWorkingDirectory, BackupDirectory, CurrencyCode
- [x] 1.3 Create `src/POS.Domain/Interfaces/IInventoryRepository.cs` with GetMovementsByProductAsync, GetLowStockProductsAsync
- [x] 1.4 Create `src/POS.Domain/Interfaces/ISaleReturnRepository.cs` with AddAsync, GetBySaleIdAsync
- [x] 1.5 Create `src/POS.Domain/Interfaces/ISettingsRepository.cs` with GetAsync, SaveAsync
- [x] 1.6 Create `src/POS.Application/UseCases/Products/UpdateProductUseCase.cs`
- [x] 1.7 Create `src/POS.Application/UseCases/Products/DeleteProductUseCase.cs`
- [x] 1.8 Create `src/POS.Application/UseCases/Products/SearchProductsUseCase.cs`
- [x] 1.9 Create `src/POS.Application/UseCases/Inventory/QueryStockUseCase.cs`
- [x] 1.10 Create `src/POS.Application/UseCases/Inventory/QueryLowStockUseCase.cs`
- [x] 1.11 Create `src/POS.Application/UseCases/Inventory/QueryMovementHistoryUseCase.cs`
- [x] 1.12 Create `src/POS.Application/UseCases/Inventory/AdjustStockUseCase.cs`
- [x] 1.13 Create `src/POS.Application/UseCases/Invoices/QuerySalesHistoryUseCase.cs`
- [x] 1.14 Create `src/POS.Application/UseCases/Invoices/ReprintReceiptUseCase.cs`
- [x] 1.15 Create `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
- [x] 1.16 Create `src/POS.Application/UseCases/Settings/LoadSettingsUseCase.cs`
- [x] 1.17 Create `src/POS.Application/UseCases/Settings/SaveSettingsUseCase.cs`
- [x] 1.18 Write unit tests for Products use cases in `tests/POS.Application.Tests/Products/`
- [x] 1.19 Write unit tests for Inventory use cases in `tests/POS.Application.Tests/Inventory/`
- [x] 1.20 Write unit tests for Invoices use cases in `tests/POS.Application.Tests/Invoices/`
- [x] 1.21 Write unit tests for Settings use cases in `tests/POS.Application.Tests/Settings/`

## Phase 2: Infrastructure Layer

- [x] 2.1 Create `src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs` with EF Core entity mapping
- [x] 2.2 Create `src/POS.Infrastructure/Persistence/Configurations/BusinessSettingsConfiguration.cs` with EF Core entity mapping
- [x] 2.3 Create `src/POS.Infrastructure/Persistence/Repositories/InventoryRepository.cs` implementing IInventoryRepository
- [x] 2.4 Create `src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs` implementing ISaleReturnRepository
- [x] 2.5 Modify `src/POS.Infrastructure/Persistence/Repositories/SaleRepository.cs` to explicitly implement ISaleRepository.GetByIdAsync with items/payments loaded for invoice reprint and returns
- [x] 2.6 Create `src/POS.Infrastructure/Persistence/Repositories/SettingsRepository.cs` implementing ISettingsRepository
- [x] 2.7 Implement Application contracts for product category search, printer command validation warnings, and manual database backup
- [x] 2.8 Modify `src/POS.Infrastructure/Persistence/AppDbContext.cs` to add DbSet<SaleReturn> and DbSet<BusinessSettings>
- [x] 2.9 Modify `src/POS.Infrastructure/DependencyInjection.cs` to register new repositories (IInventoryRepository, ISaleReturnRepository, ISettingsRepository)
- [x] 2.10 Modify `src/POS.Infrastructure/DependencyInjection.cs` to register new services/use cases (Products: Update, Delete, Search; Inventory: Query, Adjust; Invoices: Query, Reprint, Return; Settings: Load, Save, Manual Backup; printer command validator; backup service)
- [x] 2.11 Generate EF Core migration for new tables
- [x] 2.12 Verify `dotnet build POS.sln` succeeds

## Phase 3: Presentation Layer

- [x] 3.1 Create `src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs` with CRUD commands, search, product list
- [x] 3.2 Create `src/POS.Presentation.Shared/ViewModels/InventoryViewModel.cs` with stock queries, low-stock alerts, movement history
- [x] 3.3 Create `src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs` with sales history, reprint, return commands
- [x] 3.4 Create `src/POS.Presentation.Shared/ViewModels/SettingsViewModel.cs` with load/save settings, tabs for business/printer/backup/currency
- [x] 3.5 Create `src/POS.Presentation/Views/ProductsView.xaml` with product list DataGrid and create/edit form
- [x] 3.6 Create `src/POS.Presentation/Views/ProductsView.xaml.cs` with minimal code-behind
- [x] 3.7 Create `src/POS.Presentation/Views/InventoryView.xaml` with stock table, low-stock panel, movement list
- [x] 3.8 Create `src/POS.Presentation/Views/InventoryView.xaml.cs` with minimal code-behind
- [x] 3.9 Create `src/POS.Presentation/Views/InvoicesView.xaml` with sales list, receipt preview, return button
- [x] 3.10 Create `src/POS.Presentation/Views/InvoicesView.xaml.cs` with minimal code-behind
- [x] 3.11 Create `src/POS.Presentation/Views/SettingsView.xaml` with TabControl for business/printer/backup/currency
- [x] 3.12 Create `src/POS.Presentation/Views/SettingsView.xaml.cs` with minimal code-behind
- [x] 3.13 Modify `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` to mark products, inventory, invoices, settings as IsImplemented: true
- [x] 3.14 Modify `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` to add Products, Inventory, Invoices, Settings ViewModel properties
- [x] 3.15 Modify `src/POS.Presentation/Views/NavigationShellView.xaml` to add content areas for 4 new modules
- [x] 3.16 Modify `src/POS.Presentation/App.xaml.cs` to register new ViewModels in DI
- [x] 3.17 Verify `dotnet build POS.sln` succeeds
- [x] 3.18 Verify `dotnet test POS.sln --no-restore` passes all tests
