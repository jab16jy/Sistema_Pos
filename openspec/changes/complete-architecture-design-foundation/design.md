# Design: Complete Architecture Design Foundation

## Technical Approach

Keep the first implementation slice inside existing Clean Architecture boundaries: shared ViewModels orchestrate Application use cases, Infrastructure owns EF Core/SQLite persistence, and WPF supplies bindings only. Verification found `MainShellViewModel` exposes 9 modules including `quotes`; the shell test is stale at 8 modules; `CashierViewModel` still confirms a prototype sale; quote ViewModels exist but WPF shell needs quote views wired.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| Shell verification | Update `MainShellViewModelTests` to assert 9 modules and `quotes` navigation. | Remove quotes from shell or mark it planned. | Runtime shell already includes `quotes`; tests should verify the current contract, not an older module count. |
| Cashier persistence | Inject `CreateSaleUseCase` into `CashierViewModel` and replace `ConfirmPrototypeSaleCommand` with a real confirmation command that builds `CreateSaleRequest`. | Persist from WPF code-behind or keep prototype preview. | Keeps business behavior in Application/Domain and allows shared ViewModel tests. |
| Dashboard metrics | Add/read Application-level sales summary data rather than computing from UI state. | Keep static `DashboardMetric` values in shell. | Metrics must represent persisted sales/payments/inventory, and the shell should only present them. |
| Quote UI | Add WPF views for create/history/send/convert backed by existing shared quote ViewModels. | Rebuild quote logic in WPF views. | Shared ViewModels already coordinate create/history/send workflows; views should bind and navigate. |
| Contact data | Extend quote summary/history data to include email/phone when sending. | Use `"sin-contacto"` placeholders. | Send actions need real recipient contact and validation before calling `SendDocumentUseCase`. |

## Data Flow

```text
WPF View ──binds──> Shared ViewModel ──calls──> Application UseCase
       │                                      │              │
       └──────── displays result/validation ◄─┴── Infrastructure repositories
                                                        │
                                                   SQLite/AppDbContext
```

Cashier flow: product lookup fills cart, payment preview validates tendered amount, confirmation creates a `CreateSaleRequest`, `CreateSaleUseCase` loads products, `Sale.AddItem` decrements stock, `Sale.AddPayment` records cash/digital payments, `SaleRepository` persists, and dashboard metrics reload from persisted summaries.

Quote flow: `QuoteViewModel` creates quotes; `QuoteHistoryViewModel` loads rows with contact fields; send commands validate selected contact; convert command collects payment details and calls `ConvertQuoteToSaleUseCase`, which delegates sale creation and marks the quote converted.

## File Changes

| File | Action | Description |
|---|---|---|
| `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs` | Modify | Assert 9 modules, presence of `quotes`, and implemented quote navigation behavior. |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Modify | Route selected module content/actions and load dashboard metrics from Application data instead of static placeholder metrics. |
| `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs` | Modify | Add `CreateSaleUseCase` dependency, real confirmation command, sale request mapping, success/error state, and cart reset after persistence. |
| `src/POS.Application/UseCases/Sales/*` | Modify/Create | Add query use case/DTO for dashboard summaries if missing. |
| `src/POS.Domain/Interfaces/ISaleRepository.cs` | Modify | Add read methods for sales totals/cash/digital/low-stock inputs if needed. |
| `src/POS.Infrastructure/Persistence/Repositories/SaleRepository.cs` | Modify | Implement summary queries over `Sales`, `Payments`, and product stock. |
| `src/POS.Infrastructure/DependencyInjection.cs` | Modify | Register `CreateSaleUseCase`, sale summary use case, and real product lookup service when available. |
| `src/POS.Presentation.Shared/ViewModels/QuoteHistoryViewModel.cs` | Modify | Resolve real email/phone contact and complete convert-to-sale payment flow. |
| `src/POS.Application/DTOs/Quotes/QuoteSummary.cs` | Modify | Include customer phone/email needed by send actions. |
| `src/POS.Presentation.Shared/Quotes/QuoteSummaryRow.cs` | Modify | Expose contact fields to UI commands. |
| `src/POS.Presentation/Views/QuoteView.xaml` | Create | Quote creation form bound to `QuoteViewModel`. |
| `src/POS.Presentation/Views/QuoteHistoryView.xaml` | Create | History/filter/send/convert UI bound to `QuoteHistoryViewModel`. |
| `src/POS.Presentation/Views/SendDocumentView.xaml` | Create | Recipient/channel send form bound to `SendDocumentViewModel`. |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Modify | Switch central content by selected module and render quote views for `quotes`. |
| `src/POS.Presentation/App.xaml.cs` | Modify | Compose ViewModels through DI instead of prototype defaults. |

## Interfaces / Contracts

Use existing DTOs as the primary contracts. Add only focused read contracts:

```csharp
public sealed record DashboardSummary(decimal SalesTotal, decimal CashInDrawer, decimal DigitalPayments, int LowStockCount);
```

If invoice numbering requires stronger guarantees than `POS-{UtcNow}`, introduce an Application service contract first.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Unit | Shell module count/quotes selection; cashier request mapping and failure recovery; quote contact resolution/convert command state. | xUnit in `POS.Presentation.Shared.Tests` with fake use cases/services. |
| Application | Sale persistence request behavior, dashboard summary, quote conversion. | Existing `POS.Application.Tests`; add repository fakes or SQLite-backed tests if summary query is infrastructure-specific. |
| UI | WPF bindings compile and navigation content resolves. | `dotnet build POS.sln` on Windows; Domain/Application/Infrastructure tests run on any OS. |

## Migration / Rollout

No migration is planned unless summary/contact needs reveal missing columns. Existing quote/sale/payment tables are already configured in Infrastructure.

## Open Questions

- [ ] Confirm invoice numbering policy before replacing timestamp-based sale invoice numbers.
- [ ] Confirm whether quote conversion decrements stock only at conversion; current sale creation does decrement stock.
