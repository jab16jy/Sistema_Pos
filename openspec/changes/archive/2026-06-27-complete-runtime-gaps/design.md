# Design: Complete Runtime Gaps

## Technical Approach

Extend the existing Clean Architecture stack by adding missing use cases, ViewModels, and Views for Products, Inventory, Invoices, and Settings modules. The Domain layer already has Product, InventoryMovement entities and IProductRepository. We leverage existing patterns from Customers, Purchases, and Quotes modules.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Product CRUD pattern | Follow CreateProductUseCase pattern | CQRS with separate query handlers | Existing codebase uses simple use cases, not full CQRS |
| Inventory queries | Direct repository queries with LINQ | Separate InventoryService | Keeps logic in use cases, consistent with other modules |
| Invoice returns | New SaleReturn entity + use case | Modify Sale status directly | Preserves audit trail, allows partial returns later |
| Settings storage | Single BusinessSettings entity | Multiple config tables | Simpler, all settings loaded together |
| ViewModel pattern | One VM per module with injected use cases | Shared shell VM with all logic | Follows existing CustomersViewModel, PurchasesViewModel pattern |
| View pattern | UserControl with DataContext binding | Code-behind logic | Consistent with existing XAML views |

## Data Flow

```
User Action → View → ViewModel → UseCase → Repository → AppDbContext → SQLite
                ↑                                              ↓
                └────────── DTO/Entity mapping ←───────────────┘
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/POS.Domain/Entities/SaleReturn.cs` | Create | Return/refund entity with original sale reference |
| `src/POS.Domain/Entities/BusinessSettings.cs` | Create | Single entity for business config, printer, backup, currency |
| `src/POS.Domain/Interfaces/IInventoryRepository.cs` | Create | Query movements, stock levels |
| `src/POS.Domain/Interfaces/ISaleReturnRepository.cs` | Create | Persist returns |
| `src/POS.Domain/Interfaces/ISettingsRepository.cs` | Create | Load/save business settings |
| `src/POS.Application/UseCases/Products/UpdateProductUseCase.cs` | Create | Edit product fields |
| `src/POS.Application/UseCases/Products/DeleteProductUseCase.cs` | Create | Soft-delete product |
| `src/POS.Application/UseCases/Products/SearchProductsUseCase.cs` | Create | Search by name/barcode/category |
| `src/POS.Application/UseCases/Inventory/QueryStockUseCase.cs` | Create | Get current stock for all products |
| `src/POS.Application/UseCases/Inventory/QueryLowStockUseCase.cs` | Create | Get products at/below minimum |
| `src/POS.Application/UseCases/Inventory/QueryMovementHistoryUseCase.cs` | Create | Get movements for product |
| `src/POS.Application/UseCases/Inventory/AdjustStockUseCase.cs` | Create | Manual stock adjustment |
| `src/POS.Application/UseCases/Invoices/QuerySalesHistoryUseCase.cs` | Create | Search sales by date/customer/invoice |
| `src/POS.Application/UseCases/Invoices/ReprintReceiptUseCase.cs` | Create | Generate receipt for sale |
| `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs` | Create | Full return with stock restore |
| `src/POS.Application/UseCases/Settings/LoadSettingsUseCase.cs` | Create | Load business settings |
| `src/POS.Application/UseCases/Settings/SaveSettingsUseCase.cs` | Create | Save business settings |
| `src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs` | Create | EF Core entity config |
| `src/POS.Infrastructure/Persistence/Configurations/BusinessSettingsConfiguration.cs` | Create | EF Core entity config |
| `src/POS.Infrastructure/Persistence/Repositories/InventoryRepository.cs` | Create | EF Core implementation |
| `src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs` | Create | EF Core implementation |
| `src/POS.Infrastructure/Persistence/Repositories/SettingsRepository.cs` | Create | EF Core implementation |
| `src/POS.Infrastructure/Persistence/AppDbContext.cs` | Modify | Add DbSets for SaleReturn, BusinessSettings |
| `src/POS.Infrastructure/DependencyInjection.cs` | Modify | Register new repositories and use cases |
| `src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs` | Create | CRUD operations, search |
| `src/POS.Presentation.Shared/ViewModels/InventoryViewModel.cs` | Create | Stock queries, low-stock alerts |
| `src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs` | Create | Sales history, reprint, returns |
| `src/POS.Presentation.Shared/ViewModels/SettingsViewModel.cs` | Create | Load/save settings |
| `src/POS.Presentation/Views/ProductsView.xaml` | Create | Product list, create/edit form |
| `src/POS.Presentation/Views/ProductsView.xaml.cs` | Create | Code-behind (minimal) |
| `src/POS.Presentation/Views/InventoryView.xaml` | Create | Stock table, low-stock panel, movement history |
| `src/POS.Presentation/Views/InventoryView.xaml.cs` | Create | Code-behind (minimal) |
| `src/POS.Presentation/Views/InvoicesView.xaml` | Create | Sales list, receipt preview, return button |
| `src/POS.Presentation/Views/InvoicesView.xaml.cs` | Create | Code-behind (minimal) |
| `src/POS.Presentation/Views/SettingsView.xaml` | Create | Settings form with tabs |
| `src/POS.Presentation/Views/SettingsView.xaml.cs` | Create | Code-behind (minimal) |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Modify | Mark 4 modules as implemented, wire ViewModels |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Modify | Add content areas for 4 modules |
| `src/POS.Presentation/App.xaml.cs` | Modify | Register new use cases and ViewModels in DI |

## Interfaces / Contracts

```csharp
// New repository interfaces
public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken ct = default);
}

public interface ISaleReturnRepository
{
    Task AddAsync(SaleReturn return, CancellationToken ct = default);
    Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<BusinessSettings?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(BusinessSettings settings, CancellationToken ct = default);
}

// New entities
public sealed class SaleReturn
{
    public Guid Id { get; private set; }
    public Guid OriginalSaleId { get; private set; }
    public Guid ProcessedByUserId { get; private set; }
    public DateTimeOffset ReturnedAt { get; private set; }
    public string Reason { get; private set; }
}

public sealed class BusinessSettings
{
    public Guid Id { get; private set; }
    public string BusinessName { get; private set; }
    public string? BusinessAddress { get; private set; }
    public string? TaxId { get; private set; }
    public string? PrinterCommand { get; private set; }
    public string? PrinterWorkingDirectory { get; private set; }
    public string? BackupDirectory { get; private set; }
    public string CurrencyCode { get; private set; } // "COP", "USD", etc.
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Use cases (Products, Inventory, Invoices, Settings) | xUnit v3 in POS.Application.Tests, mock repositories |
| Unit | Domain entities (SaleReturn, BusinessSettings validation) | xUnit v3 in POS.Domain.Tests |
| Integration | Repository implementations with SQLite | Optional, defer to future change |
| E2E | None | Defer to future change |

## Migration / Rollout

No migration required. New tables (SaleReturn, BusinessSettings) created via EF Core migrations. Existing tables unchanged. Fresh install assumed.

## Open Questions

- None
