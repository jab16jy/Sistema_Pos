# Proposal: Quote and Invoice Audit Remediation

## Intent

Resolve issues with partial returns, quote expiration timezones, pre-approved discounts, and receipt reprinting to align POS operations with local rules.

## Scope

### In Scope
- Support multiple partial returns, tracking remaining quantities via `SaleReturn` records.
- Set `SaleStatus` to `Returned` only on 100% full return; otherwise keep as `Completed`.
- Standardize quote expiration comparisons using Colombian local time (es-CO, UTC-5).
- Format and display `InvoiceSummaryRow.CreatedAt` using es-CO offset (-5h).
- Bypass cashier discount policy validation during quote-to-sale conversions.
- Allow reprint of receipts for cancelled and returned sales.

### Out of Scope
- Modifying return approval permissions (retains Admin requirement).
- Changing database schemas beyond index constraints.

## Capabilities

### Modified Capabilities
- **Invoice Return**: Allows multiple partial returns for Completed invoices until fully returned.
- **Quote Conversion**: Permits converting active quotes with pre-approved discounts and checks validity in local time.
- **Receipt Reprint**: Allows operators to reprint receipts for Completed, Cancelled, and Returned sales.

## Approach

1. **Partial Returns**: Modify `ProcessReturnUseCase` to fetch all existing `SaleReturn` items to check remaining quantities. Do not change `SaleStatus` to `Returned` unless all quantities are returned. Enable `ProcessReturnCommand` in `InvoicesViewModel` for `Completed` sales. Make the database index on `SaleReturn.OriginalSaleId` non-unique to support multiple return records.
2. **Timezones**: Standardize quote expiration checks in `GetCustomerPayableQuotesUseCase` and `ConvertQuoteToSaleUseCase` by comparing dates adjusted to Colombian local time (UTC-5). Convert `InvoiceSummaryRow.CreatedAt` to UTC-5 before formatting.
3. **Cashier Discounts**: Add `IsQuoteConversion` flag to `CreateSaleRequest`. Bypass `CashierDiscountPolicy.Validate` in `CreateSaleUseCase` if `IsQuoteConversion` is true.
4. **Reprint Receipts**: Update `ReprintReceiptUseCase` and `InvoicesViewModel.ReprintCommand` to allow printing for `Completed`, `Cancelled`, and `Returned` sales.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `POS.Domain` | Modified | Add `IsQuoteConversion` to `CreateSaleRequest`; make `OriginalSaleId` index non-unique on `SaleReturn`. |
| `POS.Application` | Modified | Update `ProcessReturnUseCase`, `ReprintReceiptUseCase`, `ConvertQuoteToSaleUseCase`, `GetCustomerPayableQuotesUseCase`, and `CreateSaleUseCase`. |
| `POS.Presentation.Shared` | Modified | Enable reprint/return actions and adjust timezone offsets in `InvoicesViewModel` and `InvoiceSummaryRow`. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Over-returning quantity | Low | Validate remaining quantities against total original quantities minus sum of past returns. |

## Rollback Plan

Revert git changes on affected areas and restore the unique database index on `SaleReturn.OriginalSaleId`.

## Success Criteria

- [ ] Multiple partial returns can be processed up to the original sale quantity.
- [ ] Sale status becomes `Returned` only when 100% of quantities are returned.
- [ ] Quotes expire using Colombian local date (UTC-5) boundaries.
- [ ] Cancelled or Returned sales can reprint receipts successfully.
