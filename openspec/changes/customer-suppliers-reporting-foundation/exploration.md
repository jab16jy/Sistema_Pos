## Exploration: customer-suppliers-reporting-foundation

### Current State
The current implementation provides only partial foundations for the requested umbrella scope.

**Customers (RF-04)**
- `POS.Domain.Entities.Customer` exists with name, identification, phone, email, address, and active flag.
- `AppDbContext` and the existing EF migration create a `Customers` table.
- Sales and quotes can reference a `CustomerId`, but there is no customer repository, no customer use case, no customer-specific query model, no customer UI flow, and no tests for customer management.
- Quote flows currently store customer contact data inline (`CustomerName`, `CustomerPhone`, `CustomerEmail`) and do not require a persisted customer master record.
- Purchase history by customer and customer search/filter are not implemented.
- Credit balance, debt payments, and receivables are not implemented.

**Suppliers and Purchases (RF-05)**
- `Supplier`, `Purchase`, and `PurchaseItem` entities exist.
- `AppDbContext`, `PurchaseConfiguration`, `PurchaseItemConfiguration`, and the migration create persistence tables.
- There is no supplier repository, no purchase repository, no application use cases, no DI registration, no supplier/purchase ViewModel, no Avalonia screen, and no tests.
- `Purchase.AddItem(...)` only appends items; it does not increase product stock or record inventory movements.
- Supplier-product relationships, purchase history by supplier, and purchase entry workflows are not implemented.

**Reporting and Cash Closing (RF-06)**
- A minimal reporting foundation exists only for dashboard metrics:
  - `GetDashboardSummaryUseCase`
  - `ISaleRepository.GetDashboardSummaryAsync(...)`
  - `SaleRepository.GetDashboardSummaryAsync(...)`
  - `MainShellViewModel.LoadDashboardMetricsAsync()`
- This existing dashboard already separates physical cash from digital payments through `Sale.CashInDrawer()` and payment-method aggregation.
- `CashClosing` entity, `CashClosingConfiguration`, and `AppDbContext.CashClosings` exist, but there is no cash-closing repository, use case, UI flow, or tests.
- `IReportService` exposes only `GenerateDailySalesReportAsync(...)`, and `ReportService` currently throws `NotImplementedException`.
- Export to PDF/Excel, sales reports by range/category/user/product, inventory reports, receivables reports, and charts are not implemented.

**Cross-cutting foundations already in place**
- Clean Architecture boundaries are mostly respected in current implemented flows.
- Avalonia is the runnable Linux prototype UI, while shared ViewModels live in `POS.Presentation.Shared`.
- OpenSpec currently contains only shell/cashier/quotation specs; nothing yet covers RF-04 to RF-06.

### Affected Areas
- `src/POS.Domain/Entities/Customer.cs` — base customer aggregate exists but lacks behaviors for history/receivables.
- `src/POS.Domain/Entities/Supplier.cs` — supplier master exists without contracts or workflows.
- `src/POS.Domain/Entities/Purchase.cs` — purchase aggregate exists but does not apply stock/movement side effects.
- `src/POS.Domain/Entities/CashClosing.cs` — cash-closing entity exists without use-case orchestration.
- `src/POS.Domain/Entities/Sale.cs` and `src/POS.Domain/Entities/Payment.cs` — existing payment model already supports cash-vs-digital separation needed for reporting/cash closing.
- `src/POS.Domain/Interfaces/` — missing repository contracts for customers, suppliers, purchases, cash closings, and likely reporting read models.
- `src/POS.Application/Interfaces/IReportService.cs` — too narrow for the requested reporting scope.
- `src/POS.Application/UseCases/Sales/GetDashboardSummaryUseCase.cs` — useful seed for reporting queries, but insufficient for RF-06.
- `src/POS.Infrastructure/Persistence/AppDbContext.cs` — already exposes DbSets for needed aggregates.
- `src/POS.Infrastructure/Persistence/Configurations/PurchaseConfiguration.cs` and `PurchaseItemConfiguration.cs` — purchase persistence exists, but relational/detail constraints are still thin.
- `src/POS.Infrastructure/Persistence/Repositories/SaleRepository.cs` — current reporting query loads sales into memory before filtering; this is a scalability risk and a pattern to avoid extending.
- `src/POS.Infrastructure/Services/ReportService.cs` — reporting/export implementation is still absent.
- `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` — shell exposes planned modules for customers, suppliers, and reports but they are placeholders.
- `src/POS.Presentation.Avalonia/Views/MainWindow.axaml` and `App.axaml.cs` — Avalonia currently wires cashier and quotes only; new reusable logic should stay in `POS.Presentation.Shared`.
- `tests/POS.Application.Tests/SalePersistenceTests.cs` — proves current persisted sales/dashboard foundation only.
- `tests/` overall — no coverage for customers, suppliers/purchases, cash closing, receivables, or report exports.
- `openspec/specs/` — existing merged specs cover cashier sales, quotations, and shell only; new delta specs will be required.

### Approaches
| Approach | Pros | Cons | Complexity |
|----------|------|------|------------|
| Extend current entities with direct CRUD-style screens and a single broad report service | Fastest path to visible screens; less upfront design | High risk of leaking logic into UI/services, weak testability, and hard-to-evolve receivables/reporting rules | Medium |
| Build module foundations through explicit Application contracts, repositories, query DTOs, and shared ViewModels, then slice UI per work unit | Best fit for Clean Architecture; supports hybrid SDD artifacts, testable use cases, and phased delivery for customers, purchases, and reporting | More upfront design work and more interfaces/DTOs | Medium-High |
| Treat receivables as just a few fields on Customer and defer separate modeling | Simplifies first customer CRUD slice | Breaks quickly once credit sales, debt payments, and receivables reports are needed; mixes master data with accounting behavior | Medium now, High later |

### Recommendation
Recommend **Approach 2**.

Define this umbrella change as one planning change with at least four downstream work units:
1. **Customer base management** — customer master data, search/filter, customer-linked sale history.
2. **Suppliers and purchases** — supplier master, purchase registration, stock increase, inventory movement traceability.
3. **Reporting and cash closing** — persisted report query layer, cash-closing workflow, export contracts, and report UI foundations.
4. **Receivables (conditional but likely needed)** — separate capability for credit balances, debt payments, and receivables reporting.

Concrete design direction for proposal/spec/design:
- Keep write workflows in Application use cases with repository/unit-of-work orchestration.
- Add dedicated repository/query contracts instead of overloading `ISaleRepository` with every reporting need.
- Model reporting as persisted query use cases fed from sales, payments, inventory movements, purchases, and receivables data.
- Put reusable module ViewModels in `POS.Presentation.Shared`; keep Avalonia views/platform wiring in `POS.Presentation.Avalonia`.
- Preserve the non-negotiable report split between physical cash and digital payments in every cash-closing/reporting contract.
- Add inventory-movement generation as part of purchase/reporting foundations, because RF-06 inventory and audit reports depend on it.

### Risks
- **Missing repository contracts**: there are no customer/supplier/purchase/cash-closing repositories yet, so proposal/spec must define contract boundaries before design can stay clean.
- **Inventory movement gap**: current sale persistence reduces stock directly but does not persist inventory movements, which weakens audit/report accuracy for RF-06.
- **Reporting query drift**: extending the current in-memory filtering pattern in `SaleRepository` would create avoidable performance debt.
- **Receivables ambiguity**: RF-04.3 and RF-06.5 imply accounting behavior, not just customer CRUD; if this is not split explicitly, scope will blur.
- **UI placeholder reality**: customers, suppliers, and reports are shell placeholders today, so proposal/design must account for both shared ViewModels and Avalonia views.
- **Data integrity gaps**: current persistence has thin constraints/configuration for customer identification, supplier tax id, and purchase relationships.

### Ready for Proposal
Yes.

The proposal should treat **customer receivables as a separate capability from base customer management**, even if both remain inside the same umbrella change. Base customer management covers registration, search, and purchase history. Receivables introduces credit-sale balances, debt payments, and pending-balance reporting, which cross-cut sales, customers, reporting, and cash-closing behavior.
