# Tasks: Customer Suppliers Reporting Foundation

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 900-1300 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | WU1 customers -> WU2 suppliers/purchases -> WU3 receivables+cashier -> WU4 reporting/cash closing |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1 | Customer management foundation | PR 1 | sequential start |
| 2 | Supplier + purchases atomicity | PR 2 | parallel-ready after 1.3 |
| 3 | Receivables + cashier + quote collateral | PR 3 | depends on WU1 |
| 4 | Reporting + cash closing + exports | PR 4 | depends on WU3 |

## Phase 1: Shared settlement foundation (Sequential)

- [ ] 1.1 [CS1][CR1][RC2][Seq] RED: extend `tests/POS.Domain.Tests/SaleTests.cs`, `tests/POS.Application.Tests/{CreateSaleUseCaseTests.cs,PreviewPaymentUseCaseTests.cs,SalePersistenceTests.cs}`, and `tests/POS.Presentation.Shared.Tests/{CashierViewModelTests.cs,QuoteHistoryViewModelTests.cs}` for settlement-plan, orphan-credit, quote conversion, and separated closing totals.
- [ ] 1.2 [CS1][CR1][RC2][Seq] Add `src/POS.Domain/Entities/{Receivable.cs,ReceivableSettlement.cs}`; update `Sale.cs`, `CashClosing.cs`, `Payment.cs`, `src/POS.Domain/Interfaces/{ISaleRepository.cs,IReceivableRepository.cs}`, and `src/POS.Application/Interfaces/{IReceivableQueryService.cs,IOperationalReportQueryService.cs}`.
- [ ] 1.3 [CS1][CR1][Seq] Update `src/POS.Application/DTOs/Sales/{CreateSaleRequest.cs,PaymentPreviewRequest.cs,PaymentPreviewResult.cs}` and `UseCases/Sales/{PreviewPaymentUseCase.cs,CreateSaleUseCase.cs}`; include collateral `UseCases/Quotes/ConvertQuoteToSaleUseCase.cs` and `ViewModels/QuoteHistoryViewModel.cs`.

## Phase 2: Customer and supplier slices (Parallel after 1.3)

- [ ] 2.1 [CM1][CM2][Par] Add `src/POS.Application/DTOs/Customers/*`, `UseCases/Customers/{SaveCustomerUseCase.cs,GetCustomerHistoryUseCase.cs}`, `src/POS.Domain/Interfaces/ICustomerRepository.cs`, `src/POS.Infrastructure/Persistence/Repositories/CustomerRepository.cs`, and `Persistence/Queries/CustomerHistoryQueryService.cs`; update `Customer.cs` for update/deactivate rules.
- [ ] 2.2 [SP1][SP2][Par] RED/GREEN: add `DTOs/Purchases/*`, `UseCases/Purchases/RegisterPurchaseUseCase.cs`, `src/POS.Domain/Interfaces/{ISupplierRepository.cs,IPurchaseRepository.cs}`, `Persistence/Repositories/{SupplierRepository.cs,PurchaseRepository.cs}`, and update `Supplier.cs`, `Purchase.cs`, `InventoryMovement.cs` for atomic stock traceability.

## Phase 3: Persistence and shared UI wiring (Sequential after 2.1/2.2)

- [ ] 3.1 [CM1][SP2][CR2][Seq] Add EF files `Persistence/Configurations/{CustomerConfiguration.cs,SupplierConfiguration.cs,PurchaseConfiguration.cs,InventoryMovementConfiguration.cs,ReceivableConfiguration.cs,ReceivableSettlementConfiguration.cs}`; update `AppDbContext.cs`, `CashClosingConfiguration.cs`, `DependencyInjection.cs`, and add one migration under `src/POS.Infrastructure/Migrations/`.
- [ ] 3.2 [CM2][CR2][SP1][RC1][Seq] Add shared VMs `src/POS.Presentation.Shared/ViewModels/{CustomersViewModel.cs,ReceivablesViewModel.cs,PurchasesViewModel.cs,ReportsViewModel.cs}` plus focused tests in `tests/POS.Presentation.Shared.Tests/`; keep business logic in Application.

## Phase 4: Cashier and reporting delivery (Sequential after 3.1)

- [ ] 4.1 [CR1][CR2][CS1][Seq] Wire `src/POS.Presentation.Shared/ViewModels/{CashierViewModel.cs,MainShellViewModel.cs}` and Avalonia `Views/{CustomersView.axaml,ReceivablesView.axaml,PurchasesView.axaml,ReportsView.axaml}` / `App.axaml.cs` for settlement input, customer-required deferred balance, and module navigation.
- [ ] 4.2 [RC1][RC2][Seq] Add `src/POS.Application/DTOs/Reporting/*`, `UseCases/Reporting/{GetOperationalReportUseCase.cs,CloseCashRegisterUseCase.cs}`, update `src/POS.Application/Interfaces/IReportService.cs`, `src/POS.Infrastructure/Services/ReportService.cs`, and add SQLite totals tests that separate billed, cash, digital, settlements, counted, and difference.
