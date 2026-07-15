# Design: Quote and Invoice Audit Remediation

## Technical Approach

To satisfy the requirements, the system will support:
1. **Multiple Partial Returns**: `ProcessReturnUseCase` will fetch all previous return records via `ISaleReturnRepository.GetAllBySaleIdAsync` to calculate the remaining returnable quantity for each item (`OriginalQty - Sum(PastReturnedQty)`). It will reject requests exceeding these quantities or with empty items. The `SaleStatus` will only change to `Returned` when 100% of the original items are returned.
2. **Colombian Timezone (es-CO, UTC-5)**: Quote expiration logic in `GetCustomerPayableQuotesUseCase` and `ConvertQuoteToSaleUseCase` will compare quote expiration dates using the Colombian local timezone offset. Invoice creation timestamps (`InvoiceSummaryRow.CreatedAt`) will be converted to UTC-5 before formatting as `"yyyy-MM-dd HH:mm:ss"`.
3. **Cashier Discount Policy Bypass**: A new boolean `IsQuoteConversion` flag on `CreateSaleRequest` will allow `CreateSaleUseCase` to skip discount validations when converting pre-approved quotes.
4. **Receipt Reprinting**: `ReprintReceiptUseCase` and `InvoicesViewModel` will allow reprinting receipts for `Completed`, `Cancelled`, and `Returned` sales.

## Architecture Decisions

### Decision: Non-Unique Index on OriginalSaleId
**Choice**: Change the index on `SaleReturn.OriginalSaleId` to non-unique.
**Alternatives considered**: Create a separate junction table.
**Rationale**: Storing multiple `SaleReturn` records linked to the same `OriginalSaleId` is the simplest way to track history of returns without unnecessary entities.

### Decision: In-Memory Remaining Quantity Computation
**Choice**: Retrieve all past returns for a sale and calculate remaining stock per-item in memory.
**Alternatives considered**: Adding a `RemainingQuantity` column to `SaleItem`.
**Rationale**: Avoids schema migrations on `SaleItem` and ensures data consistency by computing from the source of truth (`SaleReturnItem` history).

## Data Flow

```
Quote Conversion:
[ConvertQuoteToSaleUseCase]
  ├──> Check Expiry (es-CO UTC-5): q.ExpiresAt.AddHours(-5).Date >= Now.AddHours(-5).Date
  └──> Create CreateSaleRequest { IsQuoteConversion = true }
         └──> [CreateSaleUseCase] ──(Bypass CashierDiscountPolicy.Validate)──> Persist Sale

Sale Return:
[ProcessReturnUseCase]
  ├──> Fetch past returns: GetAllBySaleIdAsync(saleId)
  ├──> Calculate & validate remaining quantity: QtyReturned <= (OriginalQty - PastReturnedQty)
  ├──> Check if 100% items returned:
  │      ├──> Yes: sale.MarkReturned() (Status = Returned)
  │      └──> No: Keep Completed status
  └──> Persist SaleReturn
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| [CreateSaleRequest.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/DTOs/Sales/CreateSaleRequest.cs) | Modify | Add `IsQuoteConversion` property to bypass discount checks. |
| [CreateSaleUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs) | Modify | Skip calling `CashierDiscountPolicy.Validate` if `IsQuoteConversion` is true. |
| [ConvertQuoteToSaleUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Quotes/ConvertQuoteToSaleUseCase.cs) | Modify | Compare expiration using UTC-5 dates. Instantiate `CreateSaleRequest` with `IsQuoteConversion = true`. |
| [GetCustomerPayableQuotesUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Quotes/GetCustomerPayableQuotesUseCase.cs) | Modify | Compare `ExpiresAt` using UTC-5 dates today or in the future. |
| [ISaleReturnRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Domain/Interfaces/ISaleReturnRepository.cs) | Modify | Add `GetAllBySaleIdAsync` method. |
| [SaleReturnRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs) | Modify | Implement `GetAllBySaleIdAsync` using EF Core. |
| [ProcessReturnUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs) | Modify | Calculate remaining quantity, validate return lines, reject zero-item returns, and update sale status to `Returned` only on 100% returns. |
| [ReprintReceiptUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Application/UseCases/Invoices/ReprintReceiptUseCase.cs) | Modify | Allow reprinting for `Completed`, `Cancelled`, and `Returned` statuses. |
| [SaleReturnConfiguration.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs) | Modify | Configure index on `OriginalSaleId` as non-unique (`.IsUnique(false)`). |
| [InvoiceSummaryRow.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Presentation.Shared/Invoices/InvoiceSummaryRow.cs) | Modify | Convert `CreatedAt` to UTC-5 and format to `"yyyy-MM-dd HH:mm:ss"`. |
| [InvoicesViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos/src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs) | Modify | Enable `ReprintCommand` for `Completed`, `Cancelled`, and `Returned` sales. |

## Interfaces / Contracts

### CreateSaleRequest
```csharp
public bool IsQuoteConversion { get; init; } = false;
```

### ISaleReturnRepository
```csharp
Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| **Domain** | Non-unique index configuration | Entity framework configuration assertion tests. |
| **Application** | Expiration comparison logic (UTC-5) | Unit test mock quotes expiring "today" versus "yesterday" in Colombia time. |
| **Application** | Bypassing discount checks | Unit test `CreateSaleUseCase` with `IsQuoteConversion = true` and a 25% discount. |
| **Application** | Multiple/partial returns validation | Unit tests verifying partial returns subtraction and 100% returned transition. |
| **Presentation** | Receipt reprinting enabled states | Unit tests verifying `InvoicesViewModel` commands enabled for Cancelled/Returned. |

## Migration / Rollout
Modify database index schema for `SALE_RETURNS.OriginalSaleId` to be non-unique via EF migration or direct SQLite schema update.

## Open Questions
- None
