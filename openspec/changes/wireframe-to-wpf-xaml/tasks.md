# Tasks: wireframe-to-wpf-xaml

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

| Field | Value |
|-------|-------|
| Estimated changed lines | ~4,800-6,500 total across 13 PRs (200-900 per slice, see per-slice table) |
| Delivery strategy | ask-on-risk (resolved by user: proceed, no re-ask) |
| Suggested split | PR 1 -> PR 2 -> ... -> PR 13, one capability per PR, stacked-to-main, in locked order |

### Per-Slice Forecast

| # | Slice (PR) | Est. lines | Risk | size:exception |
|---|------------|-----------|------|-----------------|
| 1 | shell-theme-foundation | 700-900 | High | Yes (pre-approved) |
| 2 | caja-view | 200-300 | Low | No |
| 3 | clientes-view | 450-550 | High | Yes (pre-approved) |
| 4 | cotizaciones-view | 350-450 | Medium | No |
| 5 | facturas-view | 300-400 | Medium | No |
| 6 | configuracion-admin-hub | 500-650 | High | Yes (pre-approved) |
| 7 | productos-view | 450-550 | High | Yes (pre-approved) |
| 8 | inventario-view | 450-550 | High | Yes (pre-approved) |
| 9 | compras-view | 350-450 | Medium (watch) | No |
| 10 | turnos-view | 300-400 | Medium (watch) | No |
| 11 | lealtad-view | 250-350 | Low-Medium | No |
| 12 | barcode-view | 250-350 | Low-Medium | No |
| 13 | reportes-view | 400-500 | Medium-High | No |

### Suggested Work Units

| Unit | Goal | PR | Notes |
|------|------|----|-------|
| 1 | Ship theme + shell + IA rework foundation | PR 1 | Base=main; size:exception; unblocks all others |
| 2 | Caja view to wireframe | PR 2 | Base=main (post PR1 merge); smallest gap, validates foundation |
| 3 | Clientes view to wireframe | PR 3 | Base=main; size:exception |
| 4 | Cotizaciones view (consolidate Quote+QuoteHistory) | PR 4 | Base=main |
| 5 | Facturas view to wireframe | PR 5 | Base=main |
| 6 | Configuración admin hub | PR 6 | Base=main; size:exception; must land after PR1 |
| 7 | Productos view to wireframe | PR 7 | Base=main; size:exception |
| 8 | Inventario view to wireframe | PR 8 | Base=main; size:exception |
| 9 | Compras view to wireframe | PR 9 | Base=main; watch line count |
| 10 | Turnos view to wireframe | PR 10 | Base=main; watch line count |
| 11 | Lealtad view to wireframe | PR 11 | Base=main |
| 12 | Barcode view to wireframe | PR 12 | Base=main |
| 13 | Reportes view to wireframe | PR 13 | Base=main |

Note on tasks-artifact size: per-slice file-level detail below intentionally exceeds the generic 530-word guard because the user explicitly requested concrete file/task granularity for all 13 capability slices of a large, sequenced program. This is a deliberate, requested deviation, not an oversight.

---

## Phase 1: shell-theme-foundation (PR 1 — prerequisite, size:exception)

### 1.1 MaterialDesign packages
- [x] 1.1.1 Add `MaterialDesignThemes` (5.2.1) + `MaterialDesignColors` (3.1.1) to `Directory.Packages.props`; verify latest stable net9.0-windows-compatible pair at apply time
- [x] 1.1.2 Add version-less `PackageReference` for both packages to `src/POS.Presentation/POS.Presentation.csproj`
- [x] 1.1.3 Restore; record resolved versions in apply-progress notes

### 1.2 Theme dictionaries
- [x] 1.2.1 Create `src/POS.Presentation/Themes/Palette.xaml`: token-mapped brushes (`ColorPrimaryContainer`, `BrushSecondaryContainer`, `BrushSurfaceContainer`, `BrushSuccess`/`BrushSuccessContainer`, `BrushWarning`/`BrushWarningContainer`) mapping Material `PrimaryHueMid`/`SecondaryHueMid` to existing `#0369A1`/`#0F766E`; keep existing `BrushPrimary`/`BrushAccent`/`BrushBorder`/`BrushMuted`/`BrushCard` names
- [x] 1.2.2 Create `src/POS.Presentation/Themes/Controls.xaml`: move existing `App.xaml` inline styles (Card, Button/PrimaryButton/AccentButton, TextBox, PasswordBox, ComboBox, DataGrid, MutedText, CardShadow) verbatim, no behavior change
- [x] 1.2.3 Add new styles to `Controls.xaml`: `ChipStyle` (success/warning/error/neutral), `SubtabItemStyle`, `QuickLinkStyle`, `FuncbarItem`, `TopbarChrome`, `MonoText`
- [x] 1.2.4 Modify `src/POS.Presentation/App.xaml`: convert `Application.Resources` to `ResourceDictionary`/`MergedDictionaries` (BundledTheme Light -> MaterialDesign3.Defaults.xaml -> `Palette.xaml` -> `Controls.xaml`); keep converters in root Resources
- [x] 1.2.5 Resolve implicit-style collisions (Button/TextBox/DataGrid) — decide `BasedOn` Material vs. keep app style; verify merge order; smoke-test app launch

### 1.3 Shared reusable components
- [x] 1.3.1 Create `src/POS.Presentation/Controls/KpiCard.xaml(.cs)` UserControl (label/value/hint), generalized from existing dashboard metric card
- [x] 1.3.2 Create `src/POS.Presentation/Controls/SubtabBar.xaml(.cs)` UserControl (40px strip, active underline)
- [x] 1.3.3 Create `src/POS.Presentation/Controls/QuickLink.xaml(.cs)` UserControl (icon+title+subtitle, primary hover)
- [x] 1.3.4 Publish a wireframe-icon -> `PackIconKind` mapping table (resource dictionary or documented comment) resolving the Material Symbols vs. MaterialDesignIcons gap; flag any missing glyph

### 1.4 Shell rebuild
- [x] 1.4.1 Rebuild `src/POS.Presentation/Views/NavigationShellView.xaml` root as `DockPanel`: topbar (48px, `Dock=Top`: icon+app name left, centered `SelectedModule.Title`, operator avatar + live clock + logout right), funcbar (44px, `Dock=Bottom`, `ItemsControl` of 5 `FuncbarModules` with dividers + active state), content region (`*`)
- [x] 1.4.2 Remove the 300px sidebar `ColumnDefinition`/`ListBox` markup
- [x] 1.4.3 Add `Window.InputBindings` `KeyBinding`s: `F1`->Caja, `F4`->Clientes, `F6`->Cotizaciones, `F8`->Facturas, chosen key (`F12`/`Ctrl+A`)->Admin hub, invoking `SelectModuleCommand`
- [x] 1.4.4 Remove the `IsReceivablesSelected` content `Grid` block from `NavigationShellView.xaml`
- [x] 1.4.5 Implement live topbar clock (`HH:mm:ss`, 1s tick) — visual-only concern per `pos-wpf-mvvm`

### 1.5 MainShellViewModel IA restructure
- [x] 1.5.1 Modify `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs`: add `FuncbarModules` projection (cashier F1, customers F4, quotes F6, invoices F8, admin-entry) and `AdminHubModules` projection (products, inventory, purchases, reports, shifts, loyalty, barcode)
- [x] 1.5.2 Remove `receivables` list entry, `Receivables` init property, `IsReceivablesSelected` + its `OnPropertyChanged` line, and the `receivables` arm of `UpdateOperatorMessage`
- [x] 1.5.3 Re-map `ModuleNavigationItem` shortcuts to wireframe F-keys (F1/F4/F6/F8); document the final key map, resolving the Caja in-module `F1` collision risk
- [x] 1.5.4 Keep `SelectedModule`, `SelectModuleCommand`, remaining `IsXxxSelected` flags, dashboard metrics, `LoadDashboardMetricsAsync` intact

### 1.6 Receivables UI removal
- [x] 1.6.1 Delete `src/POS.Presentation/Views/ReceivablesView.xaml` and `.xaml.cs`
- [x] 1.6.2 Delete `src/POS.Presentation.Shared/ViewModels/ReceivablesViewModel.cs`
- [x] 1.6.3 Modify `src/POS.Presentation/App.xaml.cs`: remove `ReceivablesViewModel` construction block + `Receivables = receivables` initializer
- [ ] 1.6.4 Verify `grep -r "Receivable" src/POS.Presentation src/POS.Presentation.Shared` returns zero matches; verify the same grep across `POS.Application`/`POS.Domain`/`POS.Infrastructure` still returns matches (backend intact)
  - Apply note: module UI references are removed, but this grep still finds reporting settlement fields in `ReportsView.xaml` / `ReportsViewModel.cs`; those are retained because the proposal/design keep sales/reporting receivable accounting intact.

### 1.7 Dead-code removal
- [x] 1.7.1 Grep-verify `MainShellView` referenced only by itself (`grep -rn "MainShellView" .`)
- [x] 1.7.2 Delete `src/POS.Presentation/Views/MainShellView.xaml` and `.xaml.cs`

### 1.8 Test-contract alignment
- [x] 1.8.1 Modify `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs`: align module-count assertion to the post-Receivables-removal count; state the interaction with `complete-architecture-design-foundation` in the PR description
- [x] 1.8.2 Add/update unit tests: funcbar set is exactly 5 items, admin-hub set contents, no `receivables` module, cashier default-selected, `SelectModuleCommand` behavior

### 1.9 Verification
- [x] 1.9.1 Run `dotnet build POS.sln --no-restore` — zero references to deleted files
- [x] 1.9.2 Run `dotnet test POS.sln --no-restore` — Shared/Domain/Application/Infrastructure suites pass
- [ ] 1.9.3 Manual visual QA on Windows: topbar+funcbar chrome matches wireframe baseline (cross-checked against `Wireframes/wireframe-caja.html` shell markup)
  - Apply note: not run by agent; requires user visual inspection on the Windows desktop app.

---

## Phase 2: caja-view (PR 2)

- [x] 2.1 Modify `CashierView.xaml` search dropdown row template: add stock-status chip (`ChipStyle` success/warning/error) next to thumbnail/name/code/price
- [x] 2.2 Verify/expose stock-status computed value on `CashierViewModel` search results (presentation-only, no new business logic)
- [x] 2.3 Modify cart row template: quantity input, unit price, subtotal, edit/delete icon buttons with `Visibility` bound via `IsMouseOver` style trigger
- [x] 2.4 Verify quantity-change recompute of row subtotal, cart summary, and total-to-pay (existing `CashierViewModel` logic)
- [x] 2.5 Confirm/add `AssociateCustomerCommand` (F6) and `HoldSaleCommand` (F7) on `CashierViewModel` — presentation-only delegation to existing use cases; flag if a new use case is required (out of scope)
- [x] 2.6 Wire "F6 · Cliente" / "F7 · En espera" buttons in the `pago-panel` to these commands with `IsEnabled` bindings
- [x] 2.7 Rebuild `pago-panel` layout to match wireframe: total, payment method selector, received-amount field, numpad, change/shortfall indicator, `F5 · Finalizar venta` CTA using the shared component library
- [x] 2.8 Verify shortfall message ("Falta $X", error color) and empty-cart block ("El carrito está vacío") on F5
- [x] 2.9 Run `dotnet build POS.sln --no-restore`
- [ ] 2.10 Manual visual QA against `Wireframes/wireframe-caja.html`; confirm shell `F1` shortcut does not collide with in-module search focus

---

## Phase 3: clientes-view (PR 3, size:exception)

- [ ] 3.1 Rebuild `CustomersView.xaml` into a 3-column `Grid`: customer list (search + total/active stats), detail form (name/ID/phone/email/address/notes/active toggle), history panel
- [ ] 3.2 Verify row selection populates the form via existing `SelectedCustomer` binding; "Nuevo" resets form to "Nuevo cliente" and hides delete + status chip
- [ ] 3.3 Add loyalty points badge to the form, bound to `LoyaltyPoints > 0` ("{points} puntos de lealtad disponibles"), with a link navigating to the Lealtad module
- [ ] 3.4 Add history filter buttons (Todo/Ventas/Cotizaciones) bound to existing filter command/property; add "Selecciona un cliente para ver su historial" empty state
- [ ] 3.5 Wire "Guardar"/"Eliminar" to existing commands with toast confirmation ("Cliente guardado correctamente") and list refresh
- [ ] 3.6 Run `dotnet build POS.sln --no-restore`
- [ ] 3.7 Manual visual QA against `Wireframes/wireframe-clientes.html`

---

## Phase 4: cotizaciones-view (PR 4)

- [ ] 4.1 Modify `QuoteView.xaml`: wrap existing new-quote form under a "Nueva cotización" subtab (shared `SubtabBar`), add a "Historial" subtab
- [ ] 4.2 Port `QuoteHistoryView.xaml` grid/filter markup into the "Historial" subtab content inside `QuoteView.xaml`
- [ ] 4.3 Delete `src/POS.Presentation/Views/QuoteHistoryView.xaml` and `.xaml.cs`; confirm the Cotizaciones nav entry resolves solely to the consolidated view
- [ ] 4.4 Verify subtab switching preserves in-progress quote cart state (state lives in `QuoteViewModel`)
- [ ] 4.5 Verify client fields (name required, phone, email, validity days, observations) and live subtotal/IVA/total recompute on cart changes
- [ ] 4.6 Add validation blocking "Confirmar cotización" without client name ("Ingresa el nombre del cliente") or empty cart ("Agrega al menos un producto")
- [ ] 4.7 Add WhatsApp/Email footer send actions delegating to `SendDocumentViewModel`/existing use case; block Email without email address, block WhatsApp with empty cart
- [ ] 4.8 Verify Historial subtab: filterable/paginated grid (number, client, total, status chip, expiry), per-row PDF/email/WhatsApp actions, "Venta" action shown only when status is `aceptada`
- [ ] 4.9 Run `dotnet build POS.sln --no-restore`
- [ ] 4.10 Manual visual QA against `Wireframes/wireframe-cotizaciones.html`

---

## Phase 5: facturas-view (PR 5)

- [ ] 5.1 Modify `InvoicesView.xaml`: add free-text search, date-range inputs, status selector (Todo/Activa/Anulada/Devuelta) filtering the invoice grid (number, client, total, status chip, date)
- [ ] 5.2 Add "Factura seleccionada" panel (number/client/total/date) populated on row click
- [ ] 5.3 Wire Reimprimir/PDF/Email/WhatsApp actions to existing commands with confirmation toasts; block Reimprimir with no selection ("Selecciona una factura")
- [ ] 5.4 Add "Registrar devolución" panel: required reason dropdown, optional description, refund type (Efectivo/Nota crédito/Cambio de producto); require a selected invoice
- [ ] 5.5 Block "Procesar devolución" without a reason ("Selecciona un motivo"); success toast referencing invoice number + reset reason/description on success
- [ ] 5.6 Run `dotnet build POS.sln --no-restore`
- [ ] 5.7 Manual visual QA against `Wireframes/wireframe-facturas.html`

---

## Phase 6: configuracion-admin-hub (PR 6, size:exception)

- [ ] 6.1 Rebuild `SettingsView.xaml` with shared `SubtabBar`: "Centro de control" (default), "Negocio", "Impresión y envíos"
- [ ] 6.2 Add 4-item KPI strip (usuarios activos, turno actual, último backup, canales listos) using `KpiCard`
- [ ] 6.3 Add quick-link grid (`QuickLink`) to Productos/Inventario/Compras/Reportes/Turnos/Lealtad/Barcode wired to `SelectModuleCommand`. Also add Usuarios ("users") and Auditoría ("audit") as real navigable `QuickLink` entries wired to `SelectModuleCommand` — both are already `IsImplemented: true` in `MainShellViewModel` with working views (`UserManagementView`, `AuditLogView`) and were left unreachable from any shell entry point after the Phase 1 shell rebuild (funcbar has 5 items, AdminHubModules has 7, neither includes users/audit); this closes that navigation gap. Render Respaldos as a static non-navigable placeholder only (no dedicated backup view exists yet)
- [ ] 6.4 Add Alertas panel + Checklist de operación segura (pill-style status: printer/SMTP/WhatsApp script/backup)
- [ ] 6.5 Add Negocio subtab: editable business name/tax ID/address/currency fields + "Guardar" wired to the existing `system-settings` business-information use case; add read-only policies list
- [ ] 6.6 Add Impresión y envíos subtab: Impresión panel (command/port, receipt template), Email/SMTP panel (host/sender/port/test-send), WhatsApp/Script panel (script path/working dir/status/test-script) wired to existing `SettingsViewModel` commands
- [ ] 6.7 Run `dotnet build POS.sln --no-restore`
- [ ] 6.8 Manual visual QA against `Wireframes/wireframe-configuracion.html`; confirm the shell's `ADM` funcbar entry navigates here

---

## Phase 7: productos-view (PR 7, size:exception)

- [ ] 7.1 Rebuild `ProductsView.xaml` with `SubtabBar`: "Productos" (list+form) and "Categorías" (grid+form)
- [ ] 7.2 Add product list toolbar: search (name/barcode), "Etiquetas" link to Barcode module, "CSV" import trigger, "Nuevo" action; rows show name/code+category/price/stock-status chip; add "Sin resultados" empty state
- [ ] 7.3 Add live margin preview (amount + %, negative styling) bound to cost/price changes
- [ ] 7.4 Add live stock-status preview chip bound to stock/minimum-stock changes
- [ ] 7.5 Add collapsible "Importar desde CSV" section (documented columns: name, categoryId, costPrice, salePrice, currentStock, minimumStock, description, barcode, taxRate) + file path field + "Importar productos" action wired to the existing import use case
- [ ] 7.6 Add Categorías subtab: category grid (name, active/inactive) + edit form, wired to `CategoriesViewModel`
- [ ] 7.7 Wire product/category delete to confirmation toast ("Producto eliminado") + list refresh
- [ ] 7.8 Run `dotnet build POS.sln --no-restore`
- [ ] 7.9 Manual visual QA against `Wireframes/wireframe-productos.html`

---

## Phase 8: inventario-view (PR 8, size:exception)

- [ ] 8.1 Add 3-item KPI strip (total SKUs, stock bajo, sin stock) via `KpiCard`, warning/error coloring
- [ ] 8.2 Add stock grid: search + status filter (Todos/En stock/Stock bajo/Sin stock), stock-level bar per row colored by status
- [ ] 8.3 Add row quick-adjust action opening the adjustment panel pre-selecting the product
- [ ] 8.4 Add adjustment panel: product selector, sign toggle, quantity, required reason, optional note; live "Stock actual -> Nuevo stock" preview recomputing on sign/quantity change, error-color when below minimum
- [ ] 8.5 Block "Aplicar ajuste" without a reason ("Selecciona un motivo"); success toast ("Ajuste aplicado correctamente") + panel reset on valid apply
- [ ] 8.6 Add movements panel: entrada/salida/ajuste list (product/reason/user/time/resulting stock/signed delta) with type filter dropdown; "Sin movimientos" empty state
- [ ] 8.7 Run `dotnet build POS.sln --no-restore`
- [ ] 8.8 Manual visual QA against `Wireframes/wireframe-inventario.html`

---

## Phase 9: compras-view (PR 9 — watch line count)

- [ ] 9.1 Add supplier panel: searchable list (name/phone/product count) + collapsible "Nuevo proveedor" form (name required/phone/NIT/email) hidden until requested
- [ ] 9.2 Wire supplier selection to purchase header chip (primary styling)
- [ ] 9.3 Add purchase header: supplier invoice number, purchase date, payment method (default Contado; Crédito 30/60 días)
- [ ] 9.4 Add item-entry row (product autocomplete, quantity, unit cost); "Agregar" appends to items list computing subtotal/IVA(19%)/total; support removal with recompute
- [ ] 9.5 Block "Registrar compra" without a supplier ("Selecciona un proveedor") or without items ("Agrega al menos un producto")
- [ ] 9.6 Success toast referencing supplier name + items list reset on valid registration
- [ ] 9.7 Run `dotnet build POS.sln --no-restore`
- [ ] 9.8 Manual visual QA against `Wireframes/wireframe-compras.html`

---

## Phase 10: turnos-view (PR 10 — watch line count)

- [ ] 10.1 Add persistent active-shift banner (cashier, open time, base cash, running sales total, "Abierto" chip)
- [ ] 10.2 Add shift history grid (cashier/status/open/close/base/sales/difference) filterable by date range with export action; color-code difference (Cuadra=neutral, positive=primary, negative=error)
- [ ] 10.3 Add "Abrir turno" panel: required cashier selector + base cash + optional notes; success toast ("Turno abierto exitosamente")
- [ ] 10.4 Add "Cerrar turno activo" panel: expected cash/digital totals, counted-cash input, live difference calculation (success/primary/error coloring)
- [ ] 10.5 Block "Cerrar turno" without counted cash ("Ingresa el efectivo contado"); success toast ("Turno cerrado · Reporte generado") on close
- [ ] 10.6 Run `dotnet build POS.sln --no-restore`
- [ ] 10.7 Manual visual QA against `Wireframes/wireframe-turnos.html`

---

## Phase 11: lealtad-view (PR 11)

- [ ] 11.1 Add program status card (Activo chip, total points issued, enrolled customers, checkout usage rate, redemptions this month)
- [ ] 11.2 Add rules panel: accumulation rate, redemption rate, expiration policy, checkout usage mode; "Guardar reglas" success toast ("Reglas guardadas")
- [ ] 11.3 Add tier list (>=3 tiers, ascending threshold order) with point-range chip + benefit description; "Editar beneficios" action
- [ ] 11.4 Add campaigns/history panel: active campaigns, upcoming expirations count, sales-impact metrics, explanatory note (daily operations live in Clientes/Caja)
- [ ] 11.5 Run `dotnet build POS.sln --no-restore`
- [ ] 11.6 Manual visual QA against `Wireframes/wireframe-lealtad.html`

---

## Phase 12: barcode-view (PR 12)

- [ ] 12.1 Add generator panel: value, format (CODE 128/EAN-13/CODE 39/QR Code), label, optional price, copies (1-100), live preview; hide price line when empty
- [ ] 12.2 Block "Imprimir" without a value ("Ingresa el valor del código"); success toast confirming copy count on valid print
- [ ] 12.3 Add batch panel: catalog search (name/barcode) to add items; "Agregar" adds the generated code; duplicate catalog add increments copies (no duplicate row); copies stepper + remove per row; empty-batch prompt
- [ ] 12.4 Block "Imprimir lote" on empty batch ("El lote está vacío"); success toast with total label count + batch reset on valid print
- [ ] 12.5 Run `dotnet build POS.sln --no-restore`
- [ ] 12.6 Manual visual QA against `Wireframes/wireframe-barcode.html`

---

## Phase 13: reportes-view (PR 13)

- [ ] 13.1 Rebuild `ReportsView.xaml` with `SubtabBar`: Resumen operacional / Rentabilidad / Cierres y arqueos
- [ ] 13.2 Add preset filters (Hoy/Semana/Mes) + explicit date-range inputs + PDF/Excel export actions on the operational subtab
- [ ] 13.3 Add 5 KPI cards (total facturado, efectivo esperado, efectivo ventas, pagos digitales, transferencias) with `chip-success` "Cuadra" when esperado equals ventas
- [ ] 13.4 Add sales-by-hour bar chart panel and payment-method breakdown bar chart panel with percentage labels
- [ ] 13.5 Add Rentabilidad subtab: top-10 best-selling products (proportional bars, descending units), per-category gross-margin breakdown, two-period comparison (percentage difference styling), POS-vs-converted-quotations platform breakdown
- [ ] 13.6 Add Cierres y arqueos subtab (read-only): last shift closing summary + closings query/export panel, "Ir a Turnos para cerrar caja" navigation button (no cash-closing action here), explanatory scope note
- [ ] 13.7 Run `dotnet build POS.sln --no-restore`
- [ ] 13.8 Manual visual QA against `Wireframes/wireframe-reportes.html`
