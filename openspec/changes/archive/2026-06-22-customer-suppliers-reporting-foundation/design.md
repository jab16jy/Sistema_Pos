# Design: Customer Suppliers Reporting Foundation

## Technical Approach
Implement five aligned slices behind Clean Architecture ports: `customer-management`, `customer-receivables`, `supplier-purchases-flow`, `reporting-cash-closing`, and the narrow `cashier-sales-flow` delta required by the current specs. The design is authored against the current proposal plus all five spec deltas. Write behavior stays in Application use cases, persistence/query translation stays in Infrastructure, reusable state stays in `POS.Presentation.Shared`, and Avalonia wiring stays in `POS.Presentation.Avalonia`.

## Architecture Decisions
| Decision | Choice | Rejected alternative | Rationale |
|---|---|---|---|
| Reporting reads | Add dedicated reporting/receivable query ports and SQL-backed projections | Extend `ISaleRepository.GetDashboardSummaryAsync()` / current in-memory `SaleRepository` filtering | RF-06 requires persisted sales + receivable settlements + closings, and the current repository loads rows into memory before filtering. |
| Receivable creation seam | Create receivables inside sale confirmation transaction via a settlement contract | Separate post-sale receivable job or customer balance field | Specs require receivable creation at confirmation time and forbid orphan balances; customer master data stays separate from accounting. |
| Narrow cashier delta | Extend cashier sale request/preview contracts instead of redesigning cashier screens | Hidden cashier rewrite or credit logic in UI | Current `CashierViewModel` already owns cart state; only payment/confirmation contracts need expansion. |
| Purchase atomicity | One application transaction persists purchase, stock increase, and `InventoryMovement` rows | Persist purchase first and adjust stock later | RF-05 explicitly requires no partial purchase or stock increase to remain persisted. |
| Closing snapshot | Persist/report explicit billed, cash, digital, settlement, counted, and difference totals | Keep only current `CashClosing(ExpectedCash, CountedCash, DigitalPayments)` shape | RF-06 requires auditable separated totals, not only a drawer difference. |

## Data Flow
```text
Avalonia View -> Shared ViewModel -> Application Use Case
    -> Repository/Query Port -> EF Core/SQLite
        -> Domain entities + read models
```

Deferred-balance sale:
```text
CashierViewModel settlement input
-> PreviewPaymentUseCase(total, collected, method, deferRemainder)
-> CreateSaleUseCase(CreateSaleRequest + SettlementPlan)
-> Sale validates collected + deferred == total
-> if deferred > 0: create Receivable linked to Sale and Customer
-> commit Sale + Payments + Receivable + InventoryMovement once
```

Purchase:
```text
RegisterPurchaseUseCase
-> Purchase + PurchaseItems
-> Product.IncreaseStock(...)
-> InventoryMovement(Entry, referenceId=purchaseId)
-> commit once
```

## File Changes
| File | Action | Description |
|---|---|---|
| `src/POS.Domain/Entities/Sale.cs` | Modify | Replace the current `TotalPaid >= Total` completion-only rule with a settlement-aware completion method that records collected vs deferred balance without breaking cash/digital classification. |
| `src/POS.Domain/Entities/CashClosing.cs` | Modify | Persist auditable separated totals required by reporting/closing summaries. |
| `src/POS.Domain/Entities/Receivable*.cs` | Create | Receivable aggregate and settlement entries linked to customer and origin sale. |
| `src/POS.Domain/Entities/Purchase.cs`, `InventoryMovement.cs` | Modify/Reuse | Enforce auditable purchase totals and purchase-linked stock movements. |
| `src/POS.Application/DTOs/Sales/*` | Modify | `CreateSaleRequest` and preview DTOs gain settlement fields for collected amount and deferred balance. |
| `src/POS.Application/UseCases/Sales/PreviewPaymentUseCase.cs` | Modify | Preview against collected amount, not always full total; require customer when deferring balance. |
| `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs` | Modify | Persist sale, payments, receivable, and inventory effects in one unit of work. |
| `src/POS.Application/UseCases/Receivables|Customers|Purchases|Reporting/*` | Create | CRUD/search, settlement posting, purchase registration, and reporting/closing use cases. |
| `src/POS.Domain/Interfaces` + `src/POS.Application/Interfaces` | Create/Modify | Repositories/query ports for receivables, suppliers, purchases, and SQL-backed reports. |
| `src/POS.Infrastructure/Persistence/*` | Modify/Create | EF mappings, migrations, transaction-safe repositories, and reporting queries; do not extend the in-memory `SaleRepository` pattern. |
| `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs` | Modify | Keep product/cart flow intact; add explicit settlement state (`CollectedAmount`, deferred remainder, customer requirement, preview message). |
| `src/POS.Presentation.Shared/ViewModels/*`, `src/POS.Presentation.Avalonia/Views/*` | Create/Modify | Shared customers/receivables/purchases/reporting state plus Avalonia-only screens and DI wiring. |

## Interfaces / Contracts
```csharp
public sealed record SaleSettlementPlan(decimal CollectedAmount, PaymentMethod? CollectedMethod, decimal DeferredBalanceAmount);
public sealed record OperationalReportTotalsDto(decimal BilledSalesTotal, decimal SaleCashCollected, decimal SaleDigitalCollected, decimal ReceivableSettlementCash, decimal ReceivableSettlementDigital, decimal ReceivableSettlementTotal, decimal ExpectedDrawerCash);
```
`PreviewPaymentUseCase` currently accepts only `(total, paymentMethod, receivedAmount)`, and `CashierViewModel.BuildCreateSaleRequest()` currently sends one payment line for `TotalAmount`; both must move to the settlement plan above so immediate credit/deferred-balance sales work without a cashier rewrite.

## Testing Strategy
| Layer | What to Test | Approach |
|---|---|---|
| Unit | Sale settlement invariants, orphan-credit rejection, receivable overpayment rejection, purchase totals | Domain/Application tests |
| Integration | SQLite sale+receivable atomic commit, purchase+inventory atomic commit, SQL reporting totals including settlement totals | Infrastructure/Application tests with real `AppDbContext` |
| Presentation | Cashier deferred-balance state, shared reporting filters, customer/receivable workflows | `POS.Presentation.Shared.Tests` |

## Migration / Rollout
Add EF Core tables/mappings for receivables and settlement entries plus closing/reporting columns. No data backfill is required. Roll out by enabling new navigation/modules while keeping existing cashier search/cart behavior intact.

## Open Questions
- None blocking for design.
