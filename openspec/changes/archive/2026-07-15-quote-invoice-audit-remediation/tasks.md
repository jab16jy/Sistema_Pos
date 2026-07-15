</sdd-tasks>
<Tasks: Quote and Invoice Audit Remediation>
## Review Workload Forecast

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Medium

| Field | Value |
|-------|-------|
| Estimated changed lines | 200-300 lines |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Implement quote & invoice audit remedies | PR 1 | Base branch; tests/docs included |

## Phase 1: Foundation / Infrastructure
- [x] 1.1 Update [SaleReturnConfiguration.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs) to set the index on OriginalSaleId to non-unique (`.IsUnique(false)`).
- [x] 1.2 Define `GetAllBySaleIdAsync` in [ISaleReturnRepository.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Domain/Interfaces/ISaleReturnRepository.cs).
- [x] 1.3 Implement `GetAllBySaleIdAsync` using EF Core in [SaleReturnRepository.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs).

## Phase 2: Core Implementation
- [x] 2.1 Add `IsQuoteConversion` boolean property to [CreateSaleRequest.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/DTOs/Sales/CreateSaleRequest.cs).
- [x] 2.2 Update [CreateSaleUseCase.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs) to skip `CashierDiscountPolicy` validation if `IsQuoteConversion` is true.
- [x] 2.3 Modify [ConvertQuoteToSaleUseCase.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/UseCases/Quotes/ConvertQuoteToSaleUseCase.cs) to check quote expiration in Colombian local time (UTC-5) and pass `IsQuoteConversion = true` in `CreateSaleRequest`.
- [x] 2.4 Update [GetCustomerPayableQuotesUseCase.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/UseCases/Quotes/GetCustomerPayableQuotesUseCase.cs) to compare `ExpiresAt` using Colombian local time (UTC-5).
- [x] 2.5 Modify [ProcessReturnUseCase.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs) to fetch all past return items using `ISaleReturnRepository.GetAllBySaleIdAsync`, calculate remaining returnable quantity per item, validate return lines, reject zero-item returns, and update the sale status to `Returned` only on 100% returns.
- [x] 2.6 Update [ReprintReceiptUseCase.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/ReprintReceiptUseCase.cs) to allow receipt reprinting for Completed, Cancelled, and Returned sales.

## Phase 3: Integration / Wiring
- [x] 3.1 Update [InvoicesViewModel.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs) to enable the reprint/return command states for Completed, Cancelled, and Returned sales.
- [x] 3.2 Modify [InvoiceSummaryRow.cs](file:///C:/Users/PC RYZEN/Sistema_Pos/src/POS.Presentation.Shared/Invoices/InvoiceSummaryRow.cs) to convert `CreatedAt` to Colombian local timezone (UTC-5) before formatting it as `"yyyy-MM-dd HH:mm:ss"`.

## Phase 4: Testing
- [x] 4.1 Write domain unit tests to assert the non-unique index configuration in EF Core.
- [x] 4.2 Write application unit tests for UTC-5 expiration comparisons in `GetCustomerPayableQuotesUseCase` and `ConvertQuoteToSaleUseCase`.
- [x] 4.3 Write application unit tests for `CreateSaleUseCase` to check discount limit bypass.
- [x] 4.4 Write application unit tests for `ProcessReturnUseCase` validating partial returns quantity logic and status transitions.
- [x] 4.5 Write presentation unit tests for `InvoicesViewModel` reprint/return command states.
</Tasks: Quote and Invoice Audit Remediation>
