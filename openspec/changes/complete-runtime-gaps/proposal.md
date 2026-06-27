# Proposal: Complete Runtime Gaps

## Intent

Four navigation modules are marked as "planned" in the WPF shell but represent core POS functionality: Products, Inventory, Invoices, and Settings. Users cannot manage the product catalog, check stock levels, reprint receipts, or configure the system. This change completes the full stack (Domain → Application → Infrastructure → Presentation) for these four modules to make the POS usable end-to-end.

## Scope

### In Scope
- **Products module**: CRUD for product catalog (name, price, barcode, min stock, category)
- **Inventory module**: Stock queries, low-stock alerts, stock movement history
- **Invoices module**: Receipt reprint, sales history lookup, return/refund workflow
- **Settings module**: Business info, printer config, backup, currency settings
- Full Clean Architecture stack per module: entities, use cases, repositories, ViewModels, Views
- Navigation shell integration (mark modules as `IsImplemented: true`)

### Out of Scope
- Barcode scanner hardware integration (deferred to separate change)
- Multi-warehouse inventory (single-location only for now)
- Advanced reporting/analytics beyond existing Reports module
- Migration of existing data (fresh install assumed)
- Puntos (loyalty points) module — separate change

## Capabilities

### New Capabilities
- `product-catalog`: Product CRUD, search, barcode lookup, stock threshold management
- `inventory-management`: Stock queries, movement tracking, low-stock alerts
- `invoice-management`: Sales receipt reprint, return/refund processing, invoice history
- `system-settings`: Business configuration, printer setup, backup/restore, currency

### Modified Capabilities
- `shell-navigation-verification`: Add Products, Inventory, Invoices, Settings to implemented modules

## Approach

**Layered implementation per module** following existing Clean Architecture patterns:

1. **Domain**: Entities (Product, StockMovement, InvoiceReturn), value objects, repository interfaces
2. **Application**: Use cases (CreateProduct, UpdateStock, ReprintReceipt, SaveSettings), DTOs
3. **Infrastructure**: EF Core entities, repositories, AppDbContext migrations
4. **Presentation**: ViewModels (in POS.Presentation.Shared), Views (in POS.Presentation)
5. **Shell**: Update MainShellViewModel to mark modules implemented, wire ViewModels

Each module follows the pattern established by Customers, Purchases, and Quotes modules.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/POS.Domain/Entities/` | New | Product, StockMovement, InvoiceReturn, BusinessSettings entities |
| `src/POS.Domain/Interfaces/` | New | IProductRepository, IInventoryRepository, IInvoiceRepository, ISettingsRepository |
| `src/POS.Application/UseCases/` | New | Product, Inventory, Invoice, Settings use cases |
| `src/POS.Infrastructure/Persistence/` | Modified | AppDbContext adds DbSets, entity configurations, migrations |
| `src/POS.Infrastructure/Repositories/` | New | EF Core repository implementations |
| `src/POS.Presentation.Shared/ViewModels/` | New | ProductsViewModel, InventoryViewModel, InvoicesViewModel, SettingsViewModel |
| `src/POS.Presentation/Views/` | New | ProductsView.xaml, InventoryView.xaml, InvoicesView.xaml, SettingsView.xaml |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Modified | Mark 4 modules as implemented, wire new ViewModels |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Modified | Add content areas for 4 new modules |
| `src/POS.Presentation/App.xaml.cs` | Modified | Register new use cases and ViewModels in DI |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| DbContext migration conflicts with existing data | Low | Fresh install assumed; provide migration script |
| ViewModels too large (monolithic) | Medium | Follow existing pattern: one VM per module, delegate to use cases |
| Settings scope creep (too many options) | Medium | Limit to: business info, printer, backup path, currency |
| Invoice returns complexity | Medium | Start simple: mark sale as returned, restore stock, no partial returns |

## Rollback Plan

1. Revert commits in reverse order (UI → Infrastructure → Application → Domain)
2. Drop new tables from SQLite if migration ran
3. Restore MainShellViewModel to mark modules as `IsImplemented: false`
4. No data loss risk — new tables only, no modifications to existing tables

## Dependencies

- None (services already implemented: ReportService, PrintService, SmtpEmailService, WhatsAppScriptService)
- Existing patterns in Customers, Purchases, Quotes modules as reference

## Success Criteria

- [ ] Products module: can create, edit, delete, search products with barcode
- [ ] Inventory module: shows current stock, low-stock alerts, movement history
- [ ] Invoices module: can reprint receipts, view sales history, process returns
- [ ] Settings module: can configure business info, printer, backup path, currency
- [ ] All 4 modules marked `IsImplemented: true` in navigation shell
- [ ] 109 existing tests still pass
- [ ] New tests cover use cases for each module (unit tests in Application layer)
- [ ] `dotnet build POS.sln` succeeds with no warnings
- [ ] `dotnet test POS.sln` passes all tests
