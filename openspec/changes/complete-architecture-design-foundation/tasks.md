# Tasks: Complete Architecture Design Foundation

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900-1,400 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 shell test → PR 2 cashier persistence/metrics → PR 3 quotation UI/workflow |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Align shell verification with 9 modules | PR 1 | Independent; validates `quotes` navigation. |
| 2 | Persist cashier sales and real dashboard metrics | PR 2 | Depends on PR 1 only for shell confidence; keep tests with behavior. |
| 3 | Complete quotation Avalonia workflow | PR 3 | Depends on quote contact DTOs and conversion path; retarget per chosen chain strategy. |

## Phase 1: Shell Verification Slice

- [x] 1.1 RED: Update `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs` to expect 9 modules and `quotes`. [Req: Nine-module shell contract]
- [x] 1.2 GREEN: Adjust only shell/navigation test setup if needed; do not remove `quotes`. [Req: Shell exposes required modules]
- [x] 1.3 REFACTOR: Keep shell assertions named around the active module contract. [Req: Verification rejects stale expectations]

## Phase 2: Cashier Sales and Dashboard Slice

- [x] 2.1 RED: Add shared ViewModel tests for valid/invalid sale confirmation in `CashierViewModel`. [Req: Persisted cashier sale confirmation]
- [x] 2.2 GREEN: Inject/use `CreateSaleUseCase` in `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs`, map cart/payment/customer to `CreateSaleRequest`, and reset only after persistence. [Req: Confirm valid sale]
- [x] 2.3 RED: Add Application/Infrastructure tests for persisted sale totals, payments, invoice number, and stock effect. [Req: Confirm valid sale]
- [x] 2.4 GREEN: Complete sale repository/DI wiring in `src/POS.Infrastructure/...` and `src/POS.Infrastructure/DependencyInjection.cs`. [Req: Confirm valid sale]
- [x] 2.5 RED: Add dashboard summary tests for sales-present and zero-sales periods. [Req: Real dashboard sales metrics]
- [x] 2.6 GREEN: Add `DashboardSummary` query/use case and bind `MainShellViewModel` metrics to persisted sales. [Req: Metrics reflect persisted sales]

## Phase 3: Quotation UI and Workflow Slice

- [ ] 3.1 RED: Add tests for quote history contact fields, blocked send without contact, and eligible conversion. [Req: Quote contact, send, and convert workflow]
- [ ] 3.2 GREEN: Extend `QuoteSummary`, `QuoteSummaryRow`, and `QuoteHistoryViewModel` with real email/phone send and convert state. [Req: Send quote to customer contact]
- [ ] 3.3 GREEN: Create `QuoteView.axaml`, `QuoteHistoryView.axaml`, and `SendDocumentView.axaml` bound to shared ViewModels. [Req: Quotations navigation and history]
- [ ] 3.4 GREEN: Update `MainWindow.axaml` and `App.axaml.cs` to resolve `quotes` content through DI. [Req: Navigate to quotations]
- [ ] 3.5 VERIFY: Run focused shared/application tests plus Avalonia/shared build; note WPF verification is Windows-only. [Req: all scenarios]
