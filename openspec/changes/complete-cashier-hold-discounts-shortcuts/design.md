# Design: Complete Cashier Hold, Discounts, and Shortcuts

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Held sale storage | Reuse `SaleStatus.OnHold` and `ISaleRepository`. | The domain already models the state; a second entity would duplicate snapshots and persistence. |
| Stock safety | Reserve stock when the hold is created; resume and complete the same aggregate without re-adding items. | Matches current `Sale.AddItem` semantics and prevents double subtraction. |
| Discount policy | Add `MaximumCashierDiscountPercent` to `BusinessSettings`, default 20%, editable through existing `Settings.Edit`. | Reuses the existing administrator-gated settings boundary. Application validates 0–100 and each manual line discount against the persisted value. |
| Discount separation | Keep manual line discount in the request and compose it with tier discount as today; loyalty remains a payment/tender. | Avoids changing existing loyalty accounting or tier behavior. |
| Cancellation | Keep the confirmation dialog in WPF; expose a VM cancellation operation that only clears the unsaved cart. | Dialogs are presentation concerns and no inventory was persisted for the unsaved cart. |
| Keyboard actions | Handle F5/F6/F7/Esc at the `CashierView` root and delegate to VM commands/actions. | Avoids navigation/global-shell coupling while satisfying the Caja keyboard contract. |

## Data Flow

```text
CashierView -> CashierViewModel -> HoldSaleUseCase -> Sale(OnHold) -> ISaleRepository/SQLite
CashierView -> CashierViewModel -> GetHeldSalesUseCase -> held-sale summaries
CashierView -> CashierViewModel -> ResumeHeldSaleUseCase -> existing Sale(Draft)
CashierView -> CashierViewModel -> CompleteHeldSaleUseCase -> payments/inventory/receivable/loyalty -> save
SettingsView -> SettingsViewModel -> SaveSettingsUseCase -> BusinessSettings.MaximumCashierDiscountPercent
```

Held-sale resume is intentionally restricted to the persisted snapshot. The ViewModel marks resumed lines read-only so quantity, product, and discount edits cannot invalidate the stock reservation.

## Validation

- A hold requires at least one item and a valid cashier ID.
- A discount maximum must be finite in the decimal domain and between 0 and 100 inclusive; the default is 20% when existing databases have no value.
- Application rejects any manual discount above the configured maximum before persistence.
- Resume requires an `OnHold` sale belonging to the current cashier.
- Completion uses the existing payment/settlement and customer validation rules.

## Testing Strategy

- Domain: `Resume` state transition and held aggregate stock behavior.
- Application: discount bound, hold persistence, resume ownership, and completion movement.
- Shared presentation: discount property/UI command, hold/resume/cancel command behavior.
- WPF smoke: discount binding and root key handling compile and construct.
