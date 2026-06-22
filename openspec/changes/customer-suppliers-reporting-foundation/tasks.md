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

- [x] 1.1 [CS1][CR1][RC2][Seq] RED: extend `tests/POS.Domain.Tests/SaleTests.cs`, `tests/POS.Application.Tests/{CreateSaleUseCaseTests.cs,PreviewPaymentUseCaseTests.cs,SalePersistenceTests.cs}`, and `tests/POS.Presentation.Shared.Tests/{CashierViewModelTests.cs,QuoteHistoryViewModelTests.cs}` for settlement-plan, orphan-credit, quote conversion, and separated closing totals.
- [x] 1.2 [CS1][CR1][RC2][Seq] Add `src/POS.Domain/Entities/{Receivable.cs,ReceivableSettlement.cs}`; update `Sale.cs`, `CashClosing.cs`, `Payment.cs`, `src/POS.Domain/Interfaces/{ISaleRepository.cs,IReceivableRepository.cs}`, and `src/POS.Application/Interfaces/{IReceivableQueryService.cs,IOperationalReportQueryService.cs}`.
- [x] 1.3 [CS1][CR1][Seq] Update `src/POS.Application/DTOs/Sales/{CreateSaleRequest.cs,PaymentPreviewRequest.cs,PaymentPreviewResult.cs}` and `UseCases/Sales/{PreviewPaymentUseCase.cs,CreateSaleUseCase.cs}`; include collateral `UseCases/Quotes/ConvertQuoteToSaleUseCase.cs` and `ViewModels/QuoteHistoryViewModel.cs`.

## Phase 2: Customer and supplier slices (Parallel after 1.3)

- [x] 2.1 [CM1][CM2][Par] Add `src/POS.Application/DTOs/Customers/*`, `UseCases/Customers/{SaveCustomerUseCase.cs,GetCustomerHistoryUseCase.cs}`, `src/POS.Domain/Interfaces/ICustomerRepository.cs`, `src/POS.Infrastructure/Persistence/Repositories/CustomerRepository.cs`, and `Persistence/Queries/CustomerHistoryQueryService.cs`; update `Customer.cs` for update/deactivate rules.
- [x] 2.2 [SP1][SP2][Par] RED/GREEN: add `DTOs/Purchases/*`, `UseCases/Purchases/RegisterPurchaseUseCase.cs`, `src/POS.Domain/Interfaces/{ISupplierRepository.cs,IPurchaseRepository.cs}`, `Persistence/Repositories/{SupplierRepository.cs,PurchaseRepository.cs}`, and update `Supplier.cs`, `Purchase.cs`, `InventoryMovement.cs` for atomic stock traceability.

## Phase 3: Persistence and shared UI wiring (Sequential after 2.1/2.2)

- [x] 3.1 partial [CM1][SP2][CR2][Seq] WU3: added `ReceivableConfiguration.cs`, `ReceivableSettlementConfiguration.cs`; updated `AppDbContext.cs` (Receivables/ReceivableSettlements DbSets) and `DependencyInjection.cs`; added EF migration `AddReceivables`. WU1 customer config and WU2 supplier/purchase configs were done in earlier passes; ReportsViewModel EF work is WU4.
- [x] 3.2 partial [CM2][CR2][SP1][RC1][Seq] WU3: added `ReceivablesViewModel.cs` with focused tests in `ReceivablesViewModelTests.cs`. WU1 added CustomersViewModel; WU2 added PurchasesViewModel. ReportsViewModel remains WU4 scope.

## Phase 4: Cashier and reporting delivery (Sequential after 3.1)

- [x] 4.1 [CR1][CR2][CS1][Seq] WU4: added `ReportsView.axaml`; updated `MainShellViewModel.cs` (Reports property, IsReportsSelected, reports navigation item, UpdateOperatorMessage); updated `MainWindow.axaml` reports section; wired `ReportsViewModel` in `App.axaml.cs`. Previously WU3: CashierViewModel settlement, ReceivablesView, ReceivablesViewModel.
- [x] 4.2 [RC1][RC2][Seq] WU4: added `src/POS.Application/DTOs/Reporting/{OperationalReportDto.cs,ReportRangeRequest.cs,CashClosingRequest.cs,CashClosingSummary.cs,OperationalReportResult.cs,CashClosingResult.cs}`; added `UseCases/Reporting/{GetOperationalReportUseCase.cs,CloseCashRegisterUseCase.cs}`; updated `IOperationalReportQueryService.cs` with real contract; added `IGetOperationalReportUseCase.cs`; created `OperationalReportQueryService.cs`; updated `CashClosing.cs` with separated totals; updated `CashClosingConfiguration.cs`; added EF migration `AddCashClosingSeparatedTotals`; added `ICashClosingRepository.cs` + `CashClosingRepository.cs`; added `ReportsViewModel.cs`; updated `DependencyInjection.cs`; added SQLite tests (6 tests in ReportingUseCaseTests.cs + 4 in ReportsViewModelTests.cs).
