# Proposal: wireframe-to-wpf-xaml

## Intent

Translate the 12 approved HTML wireframes (`Wireframes/wireframe-*.html`) into the real WPF application: adopt MaterialDesignInXamlToolkit, replace the current 300px left-sidebar shell with the wireframe's topbar+funcbar two-level IA, and remove UI that was never planned. Today's WPF shell is structurally the opposite of the approved design, and its flat 15-module list diverges from the agreed module inventory. Users get the operator experience the wireframes promised, on a consistent Material design system.

## Scope

### In Scope
- Add MaterialDesignThemes + MaterialDesignColors (new dependency) via `Directory.Packages.props` + `POS.Presentation/POS.Presentation.csproj`; wire `BundledTheme` + resource dictionaries mapped to wireframe CSS tokens (`--primary #0369A1`, `--secondary #0F766E`, surface/outline/on-surface), extending the already token-aligned `App.xaml` palette.
- Shell/theme foundation slice: rebuild `NavigationShellView` to topbar (48px) + funcbar (44px); restructure `MainShellViewModel`'s flat 15-item list into 5-item funcbar + Admin hub IA; delete dead `MainShellView.xaml`/`.xaml.cs`; unhook ReceivablesView from the UI.
- 12 module view slices, each WPF view rebound to its existing `POS.Presentation.Shared` ViewModel per the corresponding wireframe.

### Out of Scope
- New backend/business/domain logic beyond what unhooking the Receivables UI requires.
- `UserManagementView` / `AuditLogView` (no wireframe; admin-hub placeholders only) — keep current styling.
- Avalonia parity — no Avalonia project exists in `POS.sln` today; the config's "Avalonia prototype" note is aspirational. Tracked as a future risk only, plus a ViewModel-purity convention (no WPF types leaking into Shared).

### Receivables Removal Decision (resolved)
Grep confirms `Receivable` is domain-woven: `CreateSaleUseCase` (credit sales), `CashClosing`/`CloseCashRegisterUseCase`, reporting (`ReportsViewModel`, `OperationalReportDto`, `ReportExportService`, `OperationalReportQueryService`), and EF migrations all reference it. Decision: **remove UI only** — `ReceivablesView.xaml`/`.xaml.cs`, `ReceivablesViewModel`, nav entry, `F7` shortcut, `IsReceivablesSelected`. **Keep** Domain/Application/Infrastructure + DB schema intact; full domain deletion would break sales/reporting/migrations (high blast radius, out of scope). This satisfies the user ask ("a module we never planned shouldn't appear") — the module disappears from the operator UI while sales-created receivable data stays consistent.

### Dead-Code Decision (resolved)
`MainShellView.xaml`/`.xaml.cs` are referenced only by their own code-behind; `App.xaml.cs` uses `NavigationShellView` + the still-active `MainShellViewModel`. **Delete both** in the shell foundation slice. (`MainShellViewModel` stays — it is the live shell VM.)

### CashClosing / CloseCashRegisterUseCase Decision (resolved)
The `wireframe-reportes.html` wireframe's Cierres y arqueos subtab is explicitly read-only (no cash-closing action; a "Ir a Turnos para cerrar caja" link is the only action) — cash-closing now belongs exclusively to the Turnos module's per-shift `CloseShiftUseCase`/`CashierShift`. This leaves `CloseCashRegisterUseCase` (a distinct, period/date-range register reconciliation against `CashClosing`: separated billed/cash/digital/receivable-settlement totals vs. counted cash, not tied to a single cashier's shift) with no remaining UI caller anywhere in the app as of PR 7. Per the same rule applied to the Receivables removal above: **keep Domain/Application/Infrastructure + DB schema intact** (`CashClosing` entity, `ICashClosingRepository`, `CloseCashRegisterUseCase`, its DI registration, and its Application-layer tests) — deleting a working, tested Application-layer capability is out of scope for a UI-only wireframe migration and would destroy a period-level reconciliation capability that may be needed later (e.g. audit/compliance tooling) even though no current wireframe exposes it. The capability is simply unreachable from the UI today; this is a deliberate, documented gap, not an oversight.

## Capabilities

> Contract with sdd-spec. 13 capabilities → 13 stacked PRs (one per slice).

### New Capabilities
- `shell-theme-foundation`: MaterialDesign setup + BundledTheme/token dictionaries, topbar+funcbar shell rebuild, flat-list → 5-item funcbar + Admin hub IA restructure, Receivables UI removal, `MainShellView.xaml` dead-code cleanup. (prerequisite for all others)
- `caja-view`: CashierView to wireframe — stock chips (success/warning/error), row-hover action icons, search-dropdown polish, wire `F6`/`F7` buttons.
- `clientes-view`: CustomersView list+detail+loyalty/points history.
- `cotizaciones-view`: reconcile QuoteView+QuoteHistoryView into one page; SendDocument integration.
- `facturas-view`: InvoicesView detail/history + SendDocument integration.
- `configuracion-admin-hub`: SettingsView → 3-subtab admin hub (Centro de control / Negocio / Impresión y envíos) with KPI strip + quick-link grid to 7 modules.
- `productos-view`: ProductsView richer catalog (variants/categories/detail).
- `inventario-view`: InventoryView KPI grid + movements + adjustments.
- `compras-view`: PurchasesView order/reception flow.
- `turnos-view`: ShiftsView open/close/arqueo flow.
- `lealtad-view`: LoyaltyView rules/tiers/redemption.
- `barcode-view`: BarcodeView generator + preview/batch.
- `reportes-view`: ReportsView KPI/table + chip/subtab polish.

### Modified Capabilities
- None — spec-level behavior is unchanged; ViewModels and use cases already exist. This is UI-layer translation, not new requirements.

## Approach

MVVM per `pos-wpf-mvvm`: WPF views bind to existing `POS.Presentation.Shared` ViewModels; no business logic in XAML/code-behind (Clean Architecture inward rule, `pos-clean-architecture`). MaterialDesign dictionaries extend the current, already token-aligned `App.xaml` palette. Order (per exploration): shell foundation first (validates the library end-to-end), then Caja, then the funcbar modules (Clientes/Cotizaciones/Facturas), then the Configuración admin hub (umbrella for the rest), then catalog/ops (Productos/Inventario/Compras), then utility modules (Turnos/Lealtad/Barcode/Reportes). Delivery: chained PRs, stacked-to-main, one capability per PR, work-unit commits, each PR carrying a dependency diagram marking itself `📍`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Directory.Packages.props`, `POS.Presentation.csproj` | New | Add MaterialDesignThemes + MaterialDesignColors package refs |
| `src/POS.Presentation/App.xaml` (+ new `Themes/` dictionaries) | Modified | BundledTheme + token-mapped Material styles (chip/subtab/funcbar/topbar) |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Modified | Left sidebar → topbar+funcbar layout |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Modified | Flat 15-item list → 5-funcbar + Admin hub IA; drop `IsReceivablesSelected` |
| `src/POS.Presentation/Views/MainShellView.xaml(.cs)` | Removed | Dead code |
| `ReceivablesView.xaml(.cs)`, `ReceivablesViewModel.cs` | Removed | UI unhooked (backend kept) |
| `src/POS.Presentation/Views/*View.xaml` (12 modules) | Modified | Wireframe translation, one per slice |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| WPF-only Windows build — no XAML render verification in this dev loop | High | User's machine is Windows; per-module manual visual QA by user before each PR merges |
| 12-module chained PR discipline drift | Med | Stacked-to-main, one capability/PR, dependency diagram per PR, work-unit commits |
| Receivables UI removal breaks bindings/DI half-migrated | Med | Remove nav+VM properties together; keep domain/infra untouched; build+test after |
| L-effort slices exceed 400-line budget | High | Flagged below for further sub-splitting at sdd-tasks time |
| Configuración hub entangled with shell IA | Med | Sequence after shell foundation lands, not in parallel with it |
| Avalonia parity regressions (future) | Low | No active Avalonia consumer; keep Shared VMs WPF-type-free |

**Slices likely to need sub-splitting (>400 lines):** `shell-theme-foundation` (theme setup + shell rebuild + IA + Receivables removal + dead code — split into theme/deps vs. shell+IA), `configuracion-admin-hub`, `clientes-view`, `productos-view`, `inventario-view`; watch `compras-view` and `turnos-view` (M/L).

## Rollback Plan

Each capability is an independent stacked-to-main PR → revert the single offending merge commit without touching other modules. The shell-foundation PR is the only shared dependency; if it regresses, revert it and later module PRs (which target main after it) can be paused. MaterialDesign package additions are removable by reverting the props/csproj commit. Receivables backend is untouched, so its removal is UI-only and reversible by restoring the view/VM/nav files. Git history preserves all prior XAML.

## Dependencies

- MaterialDesignThemes + MaterialDesignColors NuGet packages (new; verified absent today).
- `shell-theme-foundation` slice must land before any module slice.
- Windows machine for manual visual QA (user-owned).

## Success Criteria

- [ ] MaterialDesign packages added; app builds on Windows with BundledTheme + token-mapped dictionaries.
- [ ] Shell renders topbar+funcbar; nav reflects 5-item funcbar + Admin hub IA.
- [ ] `MainShellView.xaml` deleted; Receivables absent from UI; sales/reporting still build and pass tests.
- [ ] Each of the 12 module views matches its wireframe and binds to its existing Shared ViewModel (no business logic in XAML/code-behind).
- [ ] Delivered as 13 stacked-to-main PRs, each within/declared against the 400-line review budget.
