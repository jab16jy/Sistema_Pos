# Apply Progress: wireframe-to-wpf-xaml

## Work Units

- Phase 1: `shell-theme-foundation`
  - Delivery: PR 1, stacked-to-main, `size:exception` pre-approved
- Phase 2: `caja-view`
  - Delivery: PR 2, stacked-to-main, no size exception
- Mode: Standard Mode (`strict_tdd: false`)
- Date: 2026-07-01

## Completed Phase 1

- 1.1 MaterialDesign packages added.
- 1.2 Theme dictionaries created and `App.xaml` converted to merged dictionaries.
- 1.3 Shared WPF controls created: `KpiCard`, `SubtabBar`, `QuickLink`; icon map published in `IconMap.cs`.
- 1.4 `NavigationShellView` rebuilt as topbar + funcbar shell; visual live clock added in code-behind.
- 1.5 `MainShellViewModel` restructured into `FuncbarModules` and `AdminHubModules`; Receivables shell state removed.
- 1.6 Receivables module UI removed from Presentation/Shared composition.
- 1.7 Dead `MainShellView.xaml` / `.xaml.cs` removed.
- 1.8 Presentation.Shared shell contract tests updated.
- 1.9 Automated build and test verification completed.

## Completed Phase 2

- 2.1 `CashierView.xaml` search dropdown now shows thumbnail, name, code, stock text, stock-status chip (`En stock` / `Stock bajo` / `Sin stock`), and formatted unit price.
- 2.2 `CashierViewModel` now exposes presentation search result view models with computed stock-status fields; `ProductLookupResult` carries `MinimumStock` from the existing product lookup query so the presentation does not guess low-stock status.
- 2.3 Cart rows now show product name/code, editable quantity input, unit price, subtotal, and hover-revealed edit/delete icon buttons.
- 2.4 Editable row quantity changes recompute item subtotal, cart summary, total-to-pay, payment balance, and command state through existing ViewModel totals.
- 2.5 `AssociateCustomerCommand` and `HoldSaleCommand` were added as presentation-only commands. `HoldSaleCommand` reports that hold/resume sale requires a new Application use case and is out of scope for this slice.
- 2.6 The `F6 · Cliente` and `F7 · En espera` payment-panel buttons are wired to the new commands.
- 2.7 The payment panel layout was polished against the wireframe: total, payment method, received amount, numpad, change/shortfall indicator, and `F5 · Finalizar venta` CTA.
- 2.8 Shortfall display now shows `Falta` with error styling, and empty-cart confirmation shows `El carrito está vacío.`
- 2.9 Automated build verification completed.

## Package Versions

- `MaterialDesignThemes`: 5.3.2
- `MaterialDesignColors`: 5.3.2

NuGet latest stable check on 2026-07-01 returned 5.3.2 for both packages. Prerelease 5.3.3-ci1401 was excluded.

## Verification

- Phase 1: `dotnet restore POS.sln` passed.
- Phase 1: `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- Phase 1: `dotnet test POS.sln --no-restore` passed:
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 54 passed
  - `POS.Application.Tests`: 112 passed
- Phase 2: `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore` passed: 58 passed.
- Phase 2: `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- Phase 2: `dotnet test POS.sln --no-restore` passed:
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 58 passed
  - `POS.Application.Tests`: 112 passed

## Deviations / Open Items

- Task 1.6.4 remains unchecked. `rg "Receivable" src\POS.Presentation src\POS.Presentation.Shared` still finds reporting settlement fields in `ReportsView.xaml` and `ReportsViewModel.cs`. These are retained because the proposal/design explicitly keep sales/reporting receivable accounting intact while removing only the Receivables module UI.
- Task 1.9.3 remains unchecked. Manual visual QA requires running the WPF app on the user's Windows desktop session.
- Task 2.5 deviation: `HoldSaleCommand` is wired but cannot persist held sales because no existing hold/resume sale use case was found; it reports this as out of scope instead of inventing backend behavior.
- Task 2.10 remains unchecked. Manual visual QA against `Wireframes/wireframe-caja.html` and F1 focus-vs-shell shortcut behavior requires the user's Windows desktop session.
- Engram apply-progress creation/update could not be completed via `mem_save` because the available Engram tool surface in this session did not expose a callable `mem_save` schema. The existing Engram tasks memory was updated where possible.

## Remaining Phase 1 Checklist Items

- [ ] 1.6.4 Decide whether to rename or hide reporting settlement terminology from Presentation/Shared, or accept the documented grep exception.
- [ ] 1.9.3 Manual visual QA on Windows.

## Remaining Phase 2 Checklist Items

- [ ] 2.10 Manual visual QA against `Wireframes/wireframe-caja.html`; confirm shell `F1` shortcut does not collide with in-module search focus.

## Next Recommended

Run `sdd-verify` for the implemented Phase 2 slice, with visual QA as the manual verification item. Continue to Phase 3 only after PR 2 review/merge.
