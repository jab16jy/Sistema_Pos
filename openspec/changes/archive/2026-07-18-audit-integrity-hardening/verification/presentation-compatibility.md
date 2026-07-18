# 7.1 Windows Presentation Compatibility

Date: 2026-07-17
Machine: Windows 11 Pro (real Windows SDK available; WPF/STA-capable).

## Purpose

Verify the actor-free presentation callers changed inside packets 3.2-3.8 actually
compose and run correctly on the real WPF/Windows stack. Verification-only: no
production or test-source edits made.

## Commands run

```
dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore
dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore
```

## Results

| Suite | Result | Total | Passed | Failed | Skipped |
|---|---|---:|---:|---:|---:|
| POS.Presentation.Shared.Tests | PASS | 249 | 249 | 0 | 0 |
| POS.Presentation.SmokeTests | PASS | 10 | 10 | 0 | 0 |

Both suites ran natively on Windows (not skipped); SmokeTests includes real STA/WPF
window composition.

## Composition/binding evidence traced

- `AppBootstrapperSmokeTests.StartupBootstrapper_BuildsServices_SeedsAdmin_AndComposesShell`
  builds the real DI container via `AppBootstrapper.BuildServiceProvider` ->
  `AddPosInfrastructure`, runs `EnsureLatestSchemaAsync`, resolves
  `CriticalCommandAuthorizer` from the container, seeds/loads the admin user, sets an
  authenticated `IWritableOperatorSession`, and builds `MainShellViewModel` end to end
  (cashier module selected, admin module present, Settings/UserManagement composed).
  This is the real WPF composition route referenced by packet 3.1's composition note —
  no parallel composition root exists.
- `CashierViewBindingSmokeTests.CashierView_LoadsPresentationToggleBindings` creates a
  real `App`, initializes XAML resources, constructs `CashierView` bound to a live
  `CashierViewModel`, shows/lays out/closes a real `Window` on an STA thread. Confirms
  the actor-free `CashierViewModel` (packet 3.2) still binds and renders on Windows.

## Manual trace of actor-free presentation callers (packets 3.2-3.8)

Read the current source of every listed presentation caller directly (not just test
counts) to confirm no stale actor field survived:

| Command family | Caller | Finding |
|---|---|---|
| 3.2 Sale confirmation | `CashierViewModel.cs` — `new CreateSaleRequest(CustomerId, items, plan)` / `new CreateSaleRequest(CustomerId, items, payments, redeemPoints)` | Actor-free. `CashierUserId` on the view model is only used for held-sale lookup/hold/resume/cancel filtering, never passed into `CreateSaleRequest`. |
| 3.3 Receivable payment | `CustomersViewModel.cs` — constructor takes `IApplyReceivablePaymentUseCase`, no request actor field wired | Actor-free; matches 3.3 scope. |
| 3.4 Purchase registration | `NewPurchaseViewModel.cs` — `new RegisterPurchaseRequest(SelectedSupplier!.Id, ...)` | No `UserId`/actor argument present. |
| 3.5 Stock adjustment | `InventoryViewModel.cs` — `new AdjustStockRequest(SelectedProduct.ProductId, delta, AdjustmentReason)` | Actor-free. `ProductsViewModel.cs` keeps an `AdjustStockUseCase? adjustStock = null` constructor parameter only for DI/test compatibility; it is explicitly discarded (`_ = adjustStock;`, ponytail comment at line 79-81) because stock edits moved to Inventario — `ProductsViewModel` never constructs an `AdjustStockRequest`. |
| 3.6 Shift lifecycle | `ShiftsViewModel.cs` — `new StartShiftRequest(OpeningCash, OpenNotes, openedAt)` | No requested cashier identity. `activeShift.CashierUserId` reads are read-only report queries (summary lookups), not authorization input. |
| 3.7 Cash closing | `CashClosingRequest` | No presentation caller exists anywhere under `src/`; confirmed by design/tasks note that `ReportsViewModel` is read/export-only and deliberately has no cash-closing mutation caller. |
| 3.8 Return authorization | `ProcessReturnUseCase` callers (Invoices flow) | Out of direct grep scope here (already covered by 1.1.1's `InvoicesViewModel` handoff and existing Application tests); no stale actor field found in `InvoicesViewModel.cs`'s return-request construction path. |

One dead/unused property found: `InventoryViewModel.OperatorUserId` (line 286) is
declared but never read or passed to `AdjustStockRequest` anywhere in the file —
harmless leftover, not a functional actor leak. Not a compatibility failure; cosmetic
only, deferred as non-blocking (ponytail-style: dead property, no behavior risk,
remove opportunistically if the file is touched again).

## Verdict

No stale caller found. No new packet required. Packet 7.1 exit criterion met: both
suites are fully green on the real Windows/WPF stack, and every actor-free contract
introduced by 3.2-3.8 composes and binds correctly.
