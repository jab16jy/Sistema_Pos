# Progress: Quote and Invoice Audit Remediation

## Phase 1: Foundation / Infrastructure
- [x] 1.1 Update `SaleReturnConfiguration.cs` index on `OriginalSaleId` to non-unique.
- [x] 1.2 Define `GetAllBySaleIdAsync` in `ISaleReturnRepository.cs`.
- [x] 1.3 Implement `GetAllBySaleIdAsync` using EF Core in `SaleReturnRepository.cs`.

## Phase 2: Core Implementation
- [x] 2.1 Add `IsQuoteConversion` to `CreateSaleRequest.cs`.
- [x] 2.2 Update `CreateSaleUseCase.cs` to skip `CashierDiscountPolicy` validation if `IsQuoteConversion` is true.
- [x] 2.3 Modify `ConvertQuoteToSaleUseCase.cs` to use Colombian local time (UTC-5) and pass `IsQuoteConversion = true`.
- [x] 2.4 Update `GetCustomerPayableQuotesUseCase.cs` to compare `ExpiresAt` using Colombian local time (UTC-5).
- [x] 2.5 Modify `ProcessReturnUseCase.cs` to support multiple partial returns and adjust `SaleStatus` correctly.
- [x] 2.6 Update `ReprintReceiptUseCase.cs` to allow reprint for Completed, Cancelled, and Returned sales.

## Phase 3: Integration / Wiring
- [x] 3.1 Update `InvoicesViewModel.cs` command states.
- [x] 3.2 Modify `InvoiceSummaryRow.cs` timezone formatting.

## Phase 4: Testing
- [x] 4.1 Domain unit tests for EF Core index configuration.
- [x] 4.2 Application unit tests for UTC-5 quote expiration.
- [x] 4.3 Application unit tests for bypassing cashier discount policy.
- [x] 4.4 Application unit tests for `ProcessReturnUseCase`.
- [x] 4.5 Presentation unit tests for `InvoicesViewModel`.

## Files Changed
- `src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs`
- `src/POS.Domain/Interfaces/ISaleReturnRepository.cs`
- `src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs`
- `src/POS.Application/DTOs/Sales/CreateSaleRequest.cs`
- `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs`
- `src/POS.Application/UseCases/Quotes/ConvertQuoteToSaleUseCase.cs`
- `src/POS.Application/UseCases/Quotes/GetCustomerPayableQuotesUseCase.cs`
- `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
- `src/POS.Application/UseCases/Invoices/ReprintReceiptUseCase.cs`
- `src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs`
- `src/POS.Presentation.Shared/Invoices/InvoiceSummaryRow.cs`
- `src/POS.Infrastructure/Migrations/*` (EF Core migration: MakeSaleReturnIndexNonUnique)
- `tests/POS.Application.Tests/SaleReturnConfigurationTests.cs` (new test file)
- `tests/POS.Application.Tests/QuoteExpirationTests.cs` (new test file)
- `tests/POS.Application.Tests/CreateSaleUseCaseTests.cs` (updated)
- `tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs` (updated)
- `tests/POS.Application.Tests/CommercialPresentationFlowTests.cs` (updated)
- `tests/POS.Presentation.Shared.Tests/InvoicesViewModelTests.cs` (updated)
- `tests/POS.Presentation.Shared.Tests/InvoicesViewModelExceptionTests.cs` (updated)
