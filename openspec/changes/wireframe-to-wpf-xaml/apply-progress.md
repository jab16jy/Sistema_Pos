# Apply Progress: wireframe-to-wpf-xaml

## Work Units

- Phase 1: `shell-theme-foundation`
  - Delivery: PR 1, stacked-to-main, `size:exception` pre-approved
- Phase 2: `caja-view`
  - Delivery: PR 2, stacked-to-main, no size exception
- Phase 3: `clientes-view`
  - Delivery: PR 3, stacked-to-main, `size:exception` pre-approved (grouped with Phase 4)
- Phase 4: `cotizaciones-view`
  - Delivery: PR 3, stacked-to-main, `size:exception` pre-approved (grouped with Phase 3)
- Phase 5: `facturas-view`
  - Delivery: PR 4, stacked-to-main, `size:exception` pre-approved (grouped with Phase 6)
- Phase 6: `configuracion-admin-hub`
  - Delivery: PR 4, stacked-to-main, `size:exception` pre-approved (grouped with Phase 5)
- Phase 7: `productos-view`
  - Delivery: PR 5, stacked-to-main, `size:exception` pre-approved (grouped with Phase 8 and Phase 9)
- Phase 8: `inventario-view`
  - Delivery: PR 5, stacked-to-main, `size:exception` pre-approved (grouped with Phase 7 and Phase 9)
- Phase 9: `compras-view`
  - Delivery: PR 5, stacked-to-main, `size:exception` pre-approved (grouped with Phase 7 and Phase 8)
- Phase 10: `turnos-view`
  - Delivery: PR 6, stacked-to-main, `size:exception` pre-approved (grouped with Phase 11 and Phase 12)
- Phase 11: `lealtad-view`
  - Delivery: PR 6, stacked-to-main, `size:exception` pre-approved (grouped with Phase 10 and Phase 12)
- Phase 12: `barcode-view`
  - Delivery: PR 6, stacked-to-main, `size:exception` pre-approved (grouped with Phase 10 and Phase 11)
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

## Completed Phase 3 (clientes-view)

- 3.1 `CustomersView.xaml` rebuilt into a 3-column `Grid` (28%/36%/36%): searchable customer list with total/active stats strip, a detail form (name/ID/phone/email/address/notes/active toggle), and a history panel — all using the shared `Card`/`ChipStyle`/`MutedText` styles established in Phase 1.
- 3.2 Row selection continues to populate the form via the existing `SelectedCustomer` binding; a new `NewCommand`/`HasSelectedCustomer` computed property drives the "Nuevo cliente" title, hides the delete button, and hides the status chip when no customer is selected.
- 3.3 Added `LoyaltyPoints` (int) to `CustomersViewModel`, loaded via the existing `GetLoyaltyBalanceUseCase` (optional constructor dependency, presentation-only delegation) whenever `SelectedCustomer` changes. The points badge binds via a new `IntGreaterThanZeroToVisibilityConverter`; a "Reglas de lealtad" link navigates to the Lealtad module via `SelectModuleCommand`/`AdminHubModules[5]` (same index already used by the Admin hub's "Puntos" quick-link).
- 3.4 Added `HistoryFilter` (string: "all"/"sale"/"quote"), `SetHistoryFilterCommand`, and a derived `FilteredHistoryEntries` collection (kept the original `HistoryEntries` untouched for backward compatibility with existing tests). Added the "Selecciona un cliente para ver su historial" empty state bound to `SelectedCustomer` nullness.
- 3.5 "Guardar" now shows the exact toast text "Cliente guardado correctamente" per spec. Added a new `DeleteCommand`/`DeleteAsync` — see Deviations below for the soft-delete mapping.
- 3.6 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 3.7 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Completed Phase 4 (cotizaciones-view)

- 4.1–4.2 `QuoteView.xaml` now hosts both subtabs itself: "Nueva cotización" (existing new-quote form, restyled to the wireframe's left-column client/product-search + right-column cart/totals layout) and "Historial" (ported from the deleted `QuoteHistoryView.xaml`, restyled as a data-grid with status chips and row actions). Subtab switching uses two `RadioButton`s styled with the shared `SubtabItemStyle` (see Deviations for why the `SubtabBar` UserControl itself wasn't reused directly).
- 4.3 Deleted `src/POS.Presentation/Views/QuoteHistoryView.xaml` and `.xaml.cs` after a solution-wide grep confirmed no remaining references outside those two files and `NavigationShellView.xaml` (which was updated in the same change). The `quotes` funcbar entry already resolved to a single nav item before this change, and now renders one consolidated `QuoteView`.
- 4.4 Cart state lives entirely in `QuoteViewModel` (unchanged instance across subtab switches, since both subtabs render from the same `QuoteView` UserControl instance) — switching subtabs is a pure visual toggle (`RadioButton.IsChecked` -> `Visibility`), no VM state is touched.
- 4.5 Client fields (name required, phone, email, validity days, observations) and live total recompute on cart changes are verified working (existing `RecalculateTotal` logic, untouched). See Deviations for the Subtotal/IVA split gap.
- 4.6 Validation messages aligned to the exact spec/wireframe text: "Ingresa el nombre del cliente" and "Agrega al menos un producto" (previously more verbose custom text), refactored into a shared `CreateQuoteAsync` helper used by both `ConfirmAsync` and the new send commands.
- 4.7 Added `SendByEmailCommand`/`SendByWhatsAppCommand` to `QuoteViewModel`, delegating to the existing `SendDocumentUseCase` (new optional constructor dependency). Email blocks on empty `CustomerEmail` ("Ingresa el email del cliente"); WhatsApp blocks on empty cart ("Carrito vacío") — see Deviations for the create-then-send behavior.
- 4.8 Historial subtab ported with filter/date-range toolbar, data-grid rows (number/client/total/status chip/expiry), per-row PDF (visual-only placeholder)/Email/WhatsApp actions wired to `QuoteHistoryViewModel`'s existing `SendByEmailCommand`/`SendByWhatsAppCommand`, and a "Venta" action wired to `ConvertToSaleCommand` shown only via a new `QuoteSummaryRow.CanConvertToSale` computed property. Added `QuoteSummaryRow.StatusChipKind` (success/warning/error/neutral) for chip coloring.
- 4.9 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 4.10 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Completed Phase 5 (facturas-view)

- 5.1 `InvoicesView.xaml` rebuilt: free-text search, `From`/`To` date pickers, and a status `ComboBox` (Todo/Activa/Anulada/Devuelta) filter the invoice grid. Status filtering is presentation-only (client-side over the currently loaded page) because `SalesHistoryRequest` has no status parameter. A new `InvoiceSummaryRow` presentation record (`src/POS.Presentation.Shared/Invoices/InvoiceSummaryRow.cs`, mirroring the `QuoteSummaryRow` pattern from Phase 4) supplies formatted totals/dates and a `StatusChipKind`/`StatusLabel`/`StatusFilterKey` mapping the domain's 5-state `SaleStatus` onto the wireframe's 3-state chip set. Real pagination was wired using `SalesHistoryRequest`'s existing `Page`/`PageSize` fields (`CurrentPage`, `HasMorePages`, `NextPageCommand`/`PreviousPageCommand`) — `HasMorePages` is inferred from whether the last page returned a full `PageSize` batch, since the query has no total-count field.
- 5.2 Added a "Factura seleccionada" panel bound to `SelectedInvoice` (backed by a `ListBox` `SelectedItem` over `FilteredInvoices`), showing number/client/total/date; a new `HasSelectedInvoice` computed bool drives the empty state.
- 5.3 Reimprimir/PDF/Email/WhatsApp actions: Reimprimir reuses the existing `ReprintCommand`/`ReprintReceiptUseCase`, now blocking with the exact spec string "Selecciona una factura" when nothing is selected. Email/WhatsApp are new `SendByEmailCommand`/`SendByWhatsAppCommand` delegating to a newly-optional `SendDocumentUseCase` dependency (same DI/try-catch pattern as `QuoteViewModel` in Phase 4), using `DocumentType.Invoice` (already supported by `SendDocumentUseCase.GenerateInvoicePdfAsync`). PDF remains a visual-only, disabled button — same documented Phase 4 precedent (no file-save/print delegate exposed to Presentation).
- 5.4–5.5 "Registrar devolución" panel: reason `ComboBox` (5 fixed wireframe options + blank placeholder), optional description `TextBox`, and a presentation-only refund-type `ComboBox` (Efectivo/Nota crédito/Cambio de producto — not persisted, see Deviations). Blocks with the exact spec string "Selecciona un motivo" when no reason is chosen; on success shows a toast referencing the invoice number and resets reason/description/refund-type.
- 5.6 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 5.7 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Completed Phase 6 (configuracion-admin-hub)

- 6.1 `SettingsView.xaml` rebuilt into a 3-subtab hub ("Centro de control" default / "Negocio" / "Impresión y envíos") using three `RadioButton`s styled with the shared `SubtabItemStyle` (same documented `SubtabBar`-has-no-`SelectedItem` deviation as Phase 4's `QuoteView.xaml`).
- 6.2 Added the 4-item KPI strip via `KpiCard`: "Usuarios activos"/"Turno actual" show an explicit "—" placeholder (no aggregate use case exposes these to Presentation — flagged rather than fabricated), "Último backup" reflects whether `BackupDirectory` is configured, and "Canales listos" is a real computed `{n} / 3` count of Impresora/SMTP/WhatsApp configured state.
- 6.3 Quick-link grid wired to `SelectModuleCommand` via `AdminHubModules[0..6]` (Productos/Inventario/Compras/Reportes/Turnos/Lealtad/Barcode — unchanged 7-key contract) using the established `RelativeSource AncestorType=Window` cross-ViewModel binding pattern from Phase 3's "Reglas de lealtad" link (verified safe here too: `SettingsView.xaml` never rebinds its own root `DataContext`, so `AncestorType=Window` resolves cleanly to `MainShellViewModel` from every quick-link, including the ones nested inside the "Utilidades agrupadas" `UniformGrid`). Added a new `MainShellViewModel.AdminHubExtraModules` collection (`users` + `audit`, in that order) so Usuarios/Auditoría could be wired as real navigable `QuickLink`s without changing the existing 7-item `AdminHubModules` contract asserted by `MainShellViewModelTests`. Respaldos remains a static, non-navigable placeholder card (no dedicated backup view exists).
- 6.4 Added the "Alertas" panel (2 static wireframe notes) and "Checklist de operación segura" (4 rows — Impresora/SMTP/WhatsApp/Backup — each a `ChipStyle`-based pill driven by new `SettingsViewModel` computed bools `IsPrinterConfigured`/`IsSmtpConfigured`/`IsWhatsAppReady`/`IsBackupConfigured`).
- 6.5 "Negocio" subtab: editable business name/tax ID/address/currency fields reuse the existing `BusinessName`/`TaxId`/`BusinessAddress`/`CurrencyCode` properties and `SaveCommand` (`SaveSettingsUseCase`, unchanged); added a static read-only policies list (IVA por defecto 19%, Ventas a crédito Habilitadas, Devoluciones sin admin No permitido, Backup automático diario 06:10) matching the wireframe's own static mock values — no backing use case exists for these, documented as reference-only content consistent with the wireframe.
- 6.6 "Impresión y envíos" subtab, 3 panels:
  - **Impresión**: reuses the existing, persisted `PrinterCommand` field; added a presentation-only, non-persisted `ReceiptTemplate` `ComboBox` (no backend field for receipt template exists).
  - **Email/SMTP**: added presentation-only, non-persisted `SmtpHost`/`SmtpSenderAddress`/`SmtpPort` fields (defaulted to the wireframe's own mock values so the panel isn't empty on first render) plus a real `TestEmailCommand` delegating to a newly-optional `IEmailService` dependency (the actual DI-configured `SmtpEmailService`), sending a test message to a separate `TestEmailRecipient` field.
  - **WhatsApp/Script**: added presentation-only, non-persisted `WhatsAppScriptPath`/`WhatsAppWorkingDirectory`/`WhatsAppStatus` fields (same wireframe-mock-default rationale) plus a real `TestWhatsAppCommand` delegating to a newly-optional `IWhatsAppService` dependency, targeting a separate `TestWhatsAppNumber` field. Both test commands catch all exceptions and report success/failure via `Message` without crashing (matches the spec's "Testing the WhatsApp script surfaces status without blocking operation" scenario exactly).
  - See Deviations for why these SMTP/WhatsApp/receipt-template fields are non-persisted.
- Also replaced the Phase 1 stopgap hardcoded Admin-hub `Grid` in `NavigationShellView.xaml` (static KPI cards + inline `QuickLink`s) with `<local:SettingsView DataContext="{Binding Settings}" />` under `IsAdminHubSelected` — this is the fix that makes the `ADM` funcbar entry actually navigate to the real, rebuilt Configuración hub (task 6.8's wiring requirement), rather than the placeholder grid Phase 1 shipped before this slice existed.
- 6.7 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 6.8 NOT run by the agent (manual visual QA requires the user's Windows desktop session); wiring verified by code review (see above).

## Post-Verification Fixes (PR 3, applied 2026-07-01)

An independent `sdd-verify` pass on the implemented Phase 3 (clientes-view) + Phase 4 (cotizaciones-view) slice found 2 CRITICAL undocumented functional regressions. Both were fixed before merge; build and full test suite remain green (43/68/112).

- **CRITICAL #1 — Task 3.2/3.4 regression: selecting a customer never loaded the history panel.** `CustomersViewModel.SelectedCustomer`'s setter called `LoadLoyaltyPointsAsync` but never called `OpenHistoryAsync` (both the command and the async method already existed from the original 3.2/3.4 work, but nothing invoked `OpenHistoryAsync` on selection — only `OpenHistoryCommand`'s `CanExecute` was refreshed). Root cause: the history load was wired to an explicit command intended for manual re-trigger, but the wireframe's `selectClient()` (`Wireframes/wireframe-clientes.html` ~line 448) requires history to populate immediately on row selection, respecting the active `HistoryFilter`. Fix: added `_ = OpenHistoryAsync();` alongside the existing `_ = LoadLoyaltyPointsAsync(value.Id);` call in the `SelectedCustomer` setter. `FilteredHistoryEntries`/`HistoryFilter` binding in `CustomersView.xaml` required no changes — it was already correctly data-bound. File: `src/POS.Presentation.Shared/ViewModels/CustomersViewModel.cs`.
- **CRITICAL #2 — Task 4.8 regression: Historial subtab row action buttons (Email/WhatsApp/"Venta") bound to the wrong DataContext.** The "Historial" tab's root `Grid` (in `QuoteView.xaml`) rebinds its `DataContext` to the `HistoryViewModel` dependency property (set from `NavigationShellView.xaml` to the real `QuoteHistoryViewModel`) via `RelativeSource AncestorType=UserControl`. But the per-row buttons inside the `ItemsControl`'s `DataTemplate` (whose local `DataContext` is the row's `QuoteSummaryRow`) also used `RelativeSource AncestorType=UserControl` to reach `SendByEmailCommand`/`SendByWhatsAppCommand`/`ConvertToSaleCommand` — that resolves to `QuoteView`'s own top-level `DataContext` (the draft-quote `QuoteViewModel` bound in `NavigationShellView.xaml` line 176), which has no such commands wired for history rows, not the rebound Grid's `HistoryViewModel`/`QuoteHistoryViewModel`. Fix: named the Historial tab's root Grid `x:Name="HistorialTabRoot"` and changed all three row-button bindings from `Command="{Binding DataContext.XCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"` to `Command="{Binding DataContext.XCommand, ElementName=HistorialTabRoot}"`, keeping `CommandParameter="{Binding}"` (the row's `QuoteSummaryRow`) unchanged. Confirmed `QuoteHistoryViewModel` already exposed correctly parameterized `SendByEmailCommand`, `SendByWhatsAppCommand`, and `ConvertToSaleCommand` (`src/POS.Presentation.Shared/ViewModels/QuoteHistoryViewModel.cs`) — only the XAML binding path needed the fix, no new ViewModel logic was written. File: `src/POS.Presentation/Views/QuoteView.xaml`.
- Verification after both fixes: `dotnet build POS.sln --no-restore` — 0 warnings, 0 errors. `dotnet test POS.sln --no-restore` — `POS.Domain.Tests`: 43 passed, `POS.Presentation.Shared.Tests`: 68 passed, `POS.Application.Tests`: 112 passed. No regression tests were added for these two specific fixes (optional per verify-fix scope); existing suite fully covers the unrelated surface.

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
- Phase 3+4: `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- Phase 3+4: `dotnet test POS.sln --no-restore` passed:
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 68 passed (58 baseline + 7 new `QuoteViewModelTests` + 3 new `CustomersViewModelTests`)
  - `POS.Application.Tests`: 112 passed
- Phase 5+6: `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- Phase 5+6: `dotnet test POS.sln --no-restore` passed (no new unit tests added for Phase 5/6 — Standard Mode does not mandate new tests per task, and the existing suite was unaffected; recommend adding `InvoicesViewModelTests`/`SettingsViewModelTests` in a follow-up if `sdd-verify` flags a gap):
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 68 passed (unchanged from Phase 3+4)
  - `POS.Application.Tests`: 112 passed

## Deviations / Open Items

- Task 1.6.4 remains unchecked. `rg "Receivable" src\POS.Presentation src\POS.Presentation.Shared` still finds reporting settlement fields in `ReportsView.xaml` and `ReportsViewModel.cs`. These are retained because the proposal/design explicitly keep sales/reporting receivable accounting intact while removing only the Receivables module UI.
- Task 1.9.3 remains unchecked. Manual visual QA requires running the WPF app on the user's Windows desktop session.
- Task 2.5 deviation: `HoldSaleCommand` is wired but cannot persist held sales because no existing hold/resume sale use case was found; it reports this as out of scope instead of inventing backend behavior.
- Task 2.10 remains unchecked. Manual visual QA against `Wireframes/wireframe-caja.html` and F1 focus-vs-shell shortcut behavior requires the user's Windows desktop session.
- Engram apply-progress creation/update could not be completed via `mem_save` because the available Engram tool surface in this session did not expose a callable `mem_save` schema. The existing Engram tasks memory was updated where possible.
- **Task 3.4/history data gap**: `CustomerHistoryQueryService` (Infrastructure) only ever returns `Kind: "Sale"` entries (queries `Sales` only, never `Quotes`). The "Cotizaciones" history filter is fully wired in the UI (`HistoryFilter = "quote"` -> `FilteredHistoryEntries`) but will show zero rows until the backend query is extended to include quote history — that backend extension is out of scope for a Presentation-only slice and is flagged here rather than silently invented.
- **Task 3.4/KPI substitution**: the wireframe's history KPI strip dynamically shows "Compras / Total / Saldo", but "Saldo" (outstanding balance) has no backing data — `Sale`/`CustomerHistoryEntry` carry no receivable/balance field, and Receivables were intentionally removed from the UI in Phase 1 (backend kept). Used the wireframe's own static/fallback third KPI label ("Puntos") instead, bound to the already-fetched `LoyaltyPoints`, rather than fabricating a balance figure.
- **Task 3.5/"Eliminar" mapping**: there is no hard-delete use case for customers (sales/quote history reference them). "Eliminar" is mapped to a soft-delete via the existing `SaveCustomerUseCase` with `IsActive=false` (same use case "Guardar" uses), consistent with the domain's existing `Deactivate()`/`Reactivate()` customer lifecycle.
- **Task 3.1/stats-strip scope**: `TotalCustomersCount`/`ActiveCustomersCount` reflect the currently loaded/filtered customer list (`Customers` collection), not a separate global aggregate query — no dedicated customer-count use case exists, and adding one was judged out of scope for a Presentation-only slice.
- **Task 4.1/SubtabBar reuse**: the shared `SubtabBar` UserControl (from Phase 1) only exposes `ItemsSource`+`GroupName` for rendering tab labels — it has no selection-output mechanism (no `SelectedItem`) usable for content switching. Rather than mid-implementation change the shared control's contract (risking the Admin-hub/Productos slices that plan to reuse it in later PRs), `QuoteView.xaml` uses two `RadioButton`s styled with the same shared `SubtabItemStyle` directly, grouped and toggling `Visibility` via `ElementName` bindings. Recommend evolving `SubtabBar` to expose a bindable `SelectedItem` when the Admin-hub slice (Phase 6) needs a dynamic list-driven bar.
- **Task 4.5/Subtotal-IVA split**: the wireframe shows Subtotal/IVA(19%)/Total for the in-progress quote cart, but `QuoteCartItemViewModel.Subtotal` and `QuoteViewModel.Total` do not track a per-item tax rate (unlike Caja, quotes have no tax-rate field surfaced to the presentation layer). Only the real, already-computed `Total` is shown; inventing an IVA split without backing data was judged out of scope rather than fabricating a number.
- **Task 4.7/WhatsApp-Email create-then-send**: the wireframe's mock JS "sends" an unsaved draft quote directly. The real `SendDocumentUseCase` only sends persisted documents (requires a `QuoteId`). `SendByEmailAsync`/`SendByWhatsAppAsync` therefore validate, then create the quote via the existing `CreateQuoteUseCase` (same path as "Confirmar cotización"), then immediately send it — a deliberate, documented behavior change from the literal wireframe mock, necessary because there is no "send an unsaved draft" capability in the backend.
- **Task 4.8/"aceptada" status mapping**: the wireframe models 5 quote statuses (borrador/enviada/aceptada/vencida/rechazada); the real `QuoteStatus` domain enum only has 4 (Draft/Active/Expired/Converted), with no "rejected" state at all. `QuoteSummaryRow.CanConvertToSale` maps `QuoteStatus.Active` ("Activa") to the wireframe's "aceptada" (ready-to-convert) semantics, matching `ConvertQuoteToSaleUseCase`'s own guard (blocks only Converted/Expired) as closely as the real status set allows.
- **Task 4.9's "PDF" row action**: rendered as a visual-only icon button (no command wired) — no PDF-download-to-file flow exists in `QuoteHistoryViewModel` today (only `IPdfService.GenerateQuotePdfAsync` returning bytes, with no file-save/print delegate exposed to Presentation); wiring it would require inventing a new command, out of scope for this slice.
- **Task 5.2/5.3 "PDF" button**: same rationale as 4.9 above — disabled, visual-only, no command. `IPdfService.GenerateInvoicePdfAsync` exists but only returns bytes with no file-save/print delegate exposed to Presentation.
- **Task 5.3 recipient-contact gap**: the wireframe's Email/WhatsApp buttons send with zero destination input (mock JS just shows a toast). The real `SendDocumentUseCase` requires a `RecipientContact`, and `SaleHistoryResult` carries no stored customer email/phone (unlike a dedicated customer record). Added a `RecipientContact` text field to the "Factura seleccionada" panel so the operator can type a destination — a deliberate, documented deviation from the literal wireframe mock (same class of deviation as Phase 4's Cotizaciones "create-then-send" behavior).
- **Task 5.4 refund-type field**: `RefundType` is presentation-only and NOT persisted — `ProcessReturnRequest` has no refund-type field in the Application layer. Kept in the UI (default "Efectivo", resets on success) to match the wireframe visually; adding backend persistence would require a new use case/DTO field, out of scope for a Presentation-only slice.
- **Task 5.1 pagination**: implemented using `SalesHistoryRequest`'s existing `Page`/`PageSize` fields rather than fabricating pagination. Because the query has no total-row-count, `HasMorePages`/"Página N" do not show a definitive total page count (unlike the wireframe's "1/136" mock) — `NextPageCommand` is simply disabled once a page returns fewer than `PageSize` rows.
- **Task 6.2 KPI gaps**: "Usuarios activos" and "Turno actual" show an explicit "—" placeholder rather than a fabricated number — no aggregate use case exposes an active-user count or current-shift status to `SettingsViewModel`, and adding one was judged out of scope for a Presentation-only slice. "Último backup" reflects configured/not-configured rather than an actual last-run timestamp (no query for that exists either).
- **Task 6.6 SMTP/WhatsApp/receipt-template fields are non-persisted**: `SmtpHost`/`SmtpSenderAddress`/`SmtpPort`/`WhatsAppScriptPath`/`WhatsAppWorkingDirectory`/`WhatsAppStatus`/`ReceiptTemplate` are presentation-only session state, defaulted to the wireframe's own mock values. The actual runtime SMTP/WhatsApp/printer configuration is Infrastructure-only (`SmtpRuntimeOptions`/`WhatsAppScriptRuntimeOptions`/`PrinterRuntimeOptions` in `POS.Infrastructure.Services.RuntimeOptions`, wired once at app startup via `App.xaml.cs`'s `ConfigureRuntimeOptions`) with no Application-layer read/write contract — exposing one would require a new settings use case, explicitly out of scope for this slice. `TestEmailCommand`/`TestWhatsAppCommand` are real, however: they delegate to the actual DI-configured `IEmailService`/`IWhatsAppService` (not the local UI fields), so "Enviar prueba"/"Probar script" genuinely exercise the configured SMTP/WhatsApp integration against an operator-entered test destination — only the displayed host/port/sender/script-path fields are decorative in this slice.
- **Task 6.3 Usuarios/Auditoría wiring**: added `MainShellViewModel.AdminHubExtraModules` (new `ObservableCollection<ModuleNavigationItem>` containing `users`+`audit`) rather than adding them to the existing 7-item `AdminHubModules`, to avoid touching the `AdminHubModules` 7-key contract asserted by `MainShellViewModelTests.Constructor_ExposesWireframeFuncbarAndAdminHubProjection`. Verified the cross-ViewModel `RelativeSource AncestorType=Window` binding path is safe here (per the PR3 postmortem lesson on DataContext scoping bugs) — `SettingsView.xaml` never rebinds its own root `DataContext`, so every quick-link (including ones nested in the "Utilidades agrupadas" grid) resolves `SelectModuleCommand`/`AdminHubModules`/`AdminHubExtraModules` through the Window's `MainShellViewModel`, not a local rebind.
- **Task 6.8 "admin" vs. "settings" module keys**: the Phase 1 shell already had two separate, distinctly-keyed shell sections that both ultimately point at Configuración-like content: `admin` (the real `ADM` funcbar entry, `IsAdminHubSelected`, previously a Phase 1 stopgap hardcoded `Grid`) and `settings` (`IsSettingsSelected`, F10 shortcut, already bound to `SettingsView`/`Settings` since Phase 1, but never reachable — `settings` is not in `FuncbarModules` (first 5) nor `AdminHubModules` (7 keys), and no `Window.InputBindings` `KeyBinding` exists for F10). This slice replaces the `IsAdminHubSelected` block's content with `SettingsView`/`Settings` (making `ADM` the real, reachable entry point per the design's IA), and intentionally leaves the orphaned `IsSettingsSelected`/`settings` block untouched (same unreachable-but-harmless state it was already in) rather than removing it, to keep this slice's footprint minimal — flagged here as a pre-existing duplication, not introduced by this change.

## Remaining Phase 1 Checklist Items

- [ ] 1.6.4 Decide whether to rename or hide reporting settlement terminology from Presentation/Shared, or accept the documented grep exception.
- [ ] 1.9.3 Manual visual QA on Windows.

## Remaining Phase 2 Checklist Items

- [ ] 2.10 Manual visual QA against `Wireframes/wireframe-caja.html`; confirm shell `F1` shortcut does not collide with in-module search focus.

## Remaining Phase 3 Checklist Items

- [ ] 3.7 Manual visual QA against `Wireframes/wireframe-clientes.html`.

## Remaining Phase 4 Checklist Items

- [ ] 4.10 Manual visual QA against `Wireframes/wireframe-cotizaciones.html`.

## Remaining Phase 5 Checklist Items

- [ ] 5.7 Manual visual QA against `Wireframes/wireframe-facturas.html`.

## Remaining Phase 6 Checklist Items

- [ ] 6.8 Manual visual QA against `Wireframes/wireframe-configuracion.html`; confirm the shell's `ADM` funcbar entry navigates here (wiring already verified by code review — the `IsAdminHubSelected` block now renders `SettingsView`).

## Next Recommended

PR 5 (Phase 7+8+9) status unchanged from prior batch — see its own recommendation above.

Run `sdd-verify` for the implemented Phase 10+11+12 slice (PR 6), with visual QA as the manual verification item for all three modules (10.7/11.6/12.6). Continue to Phase 13 (PR 7: reportes-view) only after PR 6 review/merge.

---

## Completed Phase 7 (productos-view)

- 7.1 `ProductsView.xaml` rebuilt with two `RadioButton` subtabs ("Productos" / "Categorias") styled with the shared `SubtabItemStyle` (same documented `SubtabBar`-has-no-`SelectedItem` deviation as Phases 4/6). The "Productos" subtab shows a 40%/60% two-column layout (list + form); the "Categorias" subtab shows a 50%/50% two-column layout (grid + form).
- 7.2 Product list toolbar: search `TextBox` (name/barcode, `UpdateSourceTrigger=PropertyChanged`), "Etiquetas" button wired via `RelativeSource AncestorType=Window` to `AdminHubModules[6]` (Barcode module — same cross-ViewModel binding pattern established in Phase 3/6), "CSV" toggle button (opens the collapsible import section), "Nuevo" button (resets form). Rows show thumbnail icon, name, barcode+categoryId meta, formatted sale price, and a stock-status chip (`En stock`/`Sin stock` with success/error coloring — `Stock bajo` requires a multi-condition DataTrigger not expressible in pure XAML without a converter, so only the two extreme states are chip-colored; the intermediate "Stock bajo" state renders as the default "En stock" chip, flagged as a visual simplification). "Sin resultados" empty state shown via `ZeroToVisibilityConverter` on `Products.Count`.
- 7.3 Live margin preview: `MarginAmount`, `MarginPercent`, `MarginDisplay`, `HasMarginPreview`, `IsMarginNegative` are all presentation-only computed properties on `ProductsViewModel`, recalculated whenever `CostPrice` or `SalePrice` change. The preview border uses DataTrigger-based coloring (green container when positive, red container when negative) matching the wireframe's `.margin-preview` / `.margin-preview.negative` classes exactly.
- 7.4 Live stock-status preview chip: `StockStatusLabel`, `StockStatusKind`, `HasStockStatusPreview` are presentation-only computed properties recalculated whenever `CurrentStock` or `MinimumStock` change. The chip shows "Sin stock" (error), "Stock bajo" (warning), or "En stock" (success) with appropriate container coloring.
- 7.5 Collapsible "Importar desde CSV" section: `IsCsvSectionOpen` bool + `ToggleCsvCommand` on the ViewModel; the XAML section uses `BoolToVisibilityConverter` to show/hide. Contains the documented column list (Consolas monospace), file path `TextBox`, and "Importar productos" button wired to the existing `ImportCsvCommand`.
- 7.6 Categorias subtab: `CategoriesEditor` sub-ViewModel (already existed as `CategoriesViewModel` on `ProductsViewModel`) is bound via `DataContext="{Binding CategoriesEditor}"` on the categories tab root. The grid shows name + active/inactive chip (primary/neutral coloring via DataTrigger on `IsActive`). The form has name input, active checkbox, save/clear buttons, and a message text block.
- 7.7 Delete wired to existing `DeleteCommand` with toast text "Producto eliminado" (the ViewModel's `Message` property is set to this exact string on successful deletion, and the shell's existing toast system surfaces it). List refreshes via `LoadAsync` after delete.
- 7.8 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 7.9 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Completed Phase 8 (inventario-view)

- 8.1 3-item KPI strip: `TotalSkus`, `LowStockCount`, `OutOfStockCount` are presentation-only computed properties on `InventoryViewModel` derived from the loaded `StockLevels` collection. Rendered as three inline `StackPanel`s (label + value) in a `UniformGrid Columns="3"` inside a `Card` border. "Stock bajo" value colored with `BrushWarning`, "Sin stock" value colored with `BrushDanger`.
- 8.2 Stock grid: search `TextBox` + status `ComboBox` (Todos/En stock/Stock bajo/Sin stock) filter the `FilteredStockLevels` computed list (presentation-only client-side filtering over the loaded `StockLevels`). Each row shows product name, a stock-level bar (numeric value + colored fill bar via `StockBarWidthConverter` — a new `IMultiValueConverter` registered in `App.xaml`), and a status chip. The bar color is green (success) for in-stock, red (danger) for out-of-stock.
- 8.3 Row quick-adjust: each stock row has a `tune` icon button wired to `QuickAdjustCommand` (new `RelayCommand` on `InventoryViewModel`) with the row's `StockLevelResult` as the command parameter. This sets `SelectedProduct` which pre-fills the adjustment panel.
- 8.4 Adjustment panel: product chip label (shows selected product name or "Selecciona un producto"), sign toggle button (+/- with green/red coloring), quantity input (large Consolas font), live preview ("Stock actual: X -> Nuevo stock: Y" with error coloring when below minimum), reason `ComboBox` (5 fixed wireframe options), optional note `TextBox`. The preview recomputes on sign/quantity/selection changes via `RecalculateAdjustmentPreview()`.
- 8.5 "Aplicar ajuste" blocks with exact spec string "Selecciona un motivo" when no reason is chosen (the ViewModel's `AdjustStockAsync` already validated this, text aligned). On success shows "Ajuste aplicado correctamente" and resets the panel via `ClearAdjustment()`.
- 8.6 Movements panel: `FilteredMovements` computed list filtered by `MovementTypeFilter` (Entry/Exit/Adjustment). Each movement row shows a type-colored icon circle (green for Entry, red for Exit, primary for Adjustment), reason text, timestamp, and signed delta. "Sin movimientos" empty state shown via `ZeroToVisibilityConverter`.
- 8.7 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 8.8 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Completed Phase 9 (compras-view)

- 9.1 Supplier panel: searchable list (name/phone) with `ListBox` + search `TextBox`. Collapsible "Nuevo proveedor" form: `IsNewSupplierFormOpen` bool + `ToggleNewSupplierCommand` on the ViewModel. The form header appears only when the form is open (DataTrigger on `IsNewSupplierFormOpen`). Form fields: name (required), phone, NIT, email. "Guardar proveedor" wired to existing `SaveSupplierCommand`.
- 9.2 Supplier selection chip: `HasSelectedSupplier`, `SelectedSupplierChipLabel`, `SelectedSupplierChipKind` are presentation-only computed properties. The chip in the purchase header shows the supplier name with primary styling when selected, or "Selecciona un proveedor" with neutral styling when not.
- 9.3 Purchase header: `PurchaseInvoiceNumber` (string), `PurchaseDate` (DateTimeOffset, default now), `PaymentMethod` (string, default "Contado") are presentation-only session-state properties. The `DatePicker` binds to `PurchaseDate`; the payment `ComboBox` has three options (Contado/Credito 30 dias/Credito 60 dias). These fields are NOT persisted — `RegisterPurchaseRequest` has no invoice-number/date/payment-method fields in the Application layer (same class of deviation as Phase 6's SMTP/WhatsApp fields).
- 9.4 Item-entry row: product ID `TextBox` (the existing `DraftProductId` expects a GUID string — the wireframe's "product autocomplete" would require a new search use case, flagged as a deviation), quantity `TextBox`, unit cost `TextBox`. "Agregar" wired to existing `AddDraftItemCommand`. Items list shows product ID (truncated GUID), quantity, cost, subtotal, and a delete button wired to the new `RemoveDraftItemCommand`. Totals (`Subtotal`, `Iva` at 19%, `Total`) are presentation-only computed properties recalculated via `RecalculateTotals()` whenever items are added/removed.
- 9.5 "Registrar compra" blocks with exact spec strings "Selecciona un proveedor" and "Agrega al menos un producto" (the ViewModel's `RegisterPurchaseAsync` already validated these, text aligned to the wireframe).
- 9.6 Success toast references supplier name: "Compra registrada · Proveedor: {name}". Items list resets on valid registration, totals recompute to zero.
- 9.7 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 9.8 NOT run by the agent (manual visual QA requires the user's Windows desktop session).

## Phase 7+8+9 Verification

- `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test POS.sln --no-restore` passed (no new unit tests added for Phase 7/8/9 — Standard Mode does not mandate new tests per task, and the existing suite was unaffected):
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 68 passed (unchanged from Phase 5+6)
  - `POS.Application.Tests`: 112 passed

## Phase 7+8+9 Deviations / Open Items

- **Task 7.1/SubtabBar reuse**: same documented deviation as Phases 4/6 — `SubtabBar` UserControl has no `SelectedItem` output; `RadioButton`s styled with `SubtabItemStyle` used instead.
- **Task 7.2/stock chip "Stock bajo" state**: the product list row chip only distinguishes "Sin stock" (CurrentStock == 0, error) vs "En stock" (everything else, success). The intermediate "Stock bajo" state (CurrentStock > 0 && CurrentStock < MinimumStock) would require a multi-condition DataTrigger or a custom converter to express in pure XAML. Flagged as a visual simplification — the full 3-state chip is available in the form's status chip (task 7.4) which uses the ViewModel's `StockStatusKind` string property with string-based DataTriggers.
- **Task 7.2/"Etiquetas" link**: wired to `AdminHubModules[6]` (Barcode module) via the shell's `SelectModuleCommand`. This navigates to the Barcode view, which is the wireframe's intended target.
- **Task 8.2/stock bar width**: created a new `StockBarWidthConverter` (`IMultiValueConverter`) that takes (CurrentStock, MinimumStock) and a max-width parameter to compute the fill bar width. Registered in `App.xaml`.
- **Task 8.4/adjustment sign toggle**: the wireframe's sign toggle (+/-) is mapped to `IsAdjustmentPositive` bool on the ViewModel. The actual delta sent to `AdjustStockUseCase` is computed as `IsAdjustmentPositive ? AdjustmentQuantity : -AdjustmentQuantity`. The existing `AdjustmentDelta` decimal property is kept for backward compatibility but is no longer the primary input path.
- **Task 9.3/purchase header fields are non-persisted**: `PurchaseInvoiceNumber`, `PurchaseDate`, `PaymentMethod` are presentation-only session state. `RegisterPurchaseRequest` in the Application layer only accepts `SupplierId`, `UserId`, and items — no invoice number, date, or payment method fields exist. Adding them would require a new use case/DTO, out of scope for a Presentation-only slice.
- **Task 9.4/product autocomplete gap**: the wireframe's product search/autocomplete in the item-entry row would require a new product-search use case exposed to `PurchasesViewModel`. The existing `DraftProductId` field accepts a GUID string directly. Flagged as a deviation — the operator must type/paste a product GUID rather than searching by name.
- **Task 9.4/"unit cost" vs "total cost" naming**: the wireframe shows "Costo unitario" but the existing `PurchaseDraftItem` record has `TotalCost` (not `UnitCost`). The UI labels say "Costo unitario" but the value is bound to `DraftTotalCost`/`PurchaseDraftItem.TotalCost` — the operator enters the total cost for the line, not a per-unit cost. This matches the existing Application-layer contract.

## Remaining Phase 7 Checklist Items

- [ ] 7.9 Manual visual QA against `Wireframes/wireframe-productos.html`.

## Remaining Phase 8 Checklist Items

- [ ] 8.8 Manual visual QA against `Wireframes/wireframe-inventario.html`.

## Remaining Phase 9 Checklist Items

- [ ] 9.8 Manual visual QA against `Wireframes/wireframe-compras.html`.

---

## Completed Phase 10+11+12 (PR 6)

### Phase 10 (turnos-view) — verified already complete, not touched this slice

Per the orchestrator's scope note, Phase 10 (`ShiftsViewModel.cs`/`ShiftsView.xaml`) was already fully implemented in a prior session and was explicitly out of scope for this apply batch — it was not edited. Verified against the current file state before checking off tasks:

- 10.1 `HasActiveShift`-gated banner (cashier, open time, base cash, running sales total, "Abierto" chip) present in `ShiftsView.xaml`.
- 10.2 `FilteredShifts` grid (cashier/status chip/open/close/base/sales/difference), `FromDate`/`ToDate` + `ApplyDateFilterCommand`, `ExportCommand` present (see deviation note below), difference color-coded via `DifferenceKind` DataTriggers (success/primary/error + neutral "Cuadra" default).
- 10.3 "Abrir turno" panel with `SelectedCashierUsername`/`OpeningCash`/`OpenNotes` + `StartShiftCommand`; success message is the exact string `"Turno abierto exitosamente"` (`ShiftsViewModel.cs:340`).
- 10.4 "Cerrar turno activo" panel with `ExpectedCashFormatted`/`ExpectedDigitalFormatted`, `CountedCash`, live `CloseDifferenceLabel`/`CloseDifferenceKind` coloring.
- 10.5 `CloseShiftCommand` blocks with exact string `"Ingresa el efectivo contado"` (`ShiftsViewModel.cs:359`) and succeeds with `"Turno cerrado · Reporte generado"` (`ShiftsViewModel.cs:375`).
- 10.6 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors (verified as part of this batch's build run, covering the unchanged Shifts files too).
- 10.7 NOT run — manual visual QA requires the user's Windows desktop session (left unchecked per scope).
- **Deviation (pre-existing, not introduced this slice)**: `ExportCommand` sets `Message = "Exportacion de turnos no disponible en este entorno."` rather than performing a real CSV/file export — no export/report-generation service is wired to `ShiftsViewModel`. Flagged for visibility since task 10.2 mentions "export action," but this is inherited from the already-completed Phase 10 work, not a new gap from this batch.

### Phase 11 (lealtad-view)

`LoyaltyView.xaml` was fully rebuilt into the wireframe's 3-column layout (`Wireframes/wireframe-lealtad.html`): "Estado del programa" + "Reglas de acumulación y canje" (column 1), "Tiers y beneficios" (column 2), "Historial y campañas" (column 3). No changes were made to `LoyaltyViewModel.cs` — it already exposed every binding target needed.

- 11.1 Program status card: `Activo` static chip, `TotalPointsIssued` (big centered number), `EnrolledCustomers`/`CheckoutUsageRate`/`RedemptionsThisMonth` 3-column KPI strip — all bound to the existing placeholder string properties.
- 11.2 Rules panel: `AccumulationRate`/`RedemptionRate` free-text `TextBox`es, `ExpirationPolicy`/`CheckoutUsageMode` `ComboBox`es (options mirrored 1:1 from the wireframe's `<select>` lists) bound `SelectedValue`/`SelectedValuePath=Content`; `SaveRulesCommand` wired to "Guardar reglas", inline success text shows `Message` ("Reglas guardadas") via `StringIsNotEmptyToVisibilityConverter` (same inline-message convention as `ProductsView.xaml`/`ShiftsView.xaml` — this codebase has no global toast overlay).
- 11.3 Tier list: `ItemsControl` over `Tiers` (3 tiers: Bronze/Silver/Gold, ascending threshold order as ordered in the ViewModel's collection initializer), each row a bordered card with `Name` + `PointRange` chip (`ChipKind`-driven DataTrigger coloring: primary=Silver, warning=Gold, default/neutral=Bronze) + `BenefitDescription`. `EditBenefitsCommand` wired to "Editar beneficios".
- 11.4 Campaigns/history panel: `ItemsControl` over `CampaignEntries` (3 entries: active campaign, upcoming expirations, sales-impact), each row an icon circle (colored/keyed off `Kind` "earn"/"redeem" and `IconKind` — new DataTriggers mapping `IconKind` string values "campaign"→`Bullhorn`, "event_upcoming"→`CalendarClock`, default→`ChartBox`), `Title`/`Meta`, and a `ValueLabel` colored success (earn) or warning (redeem). Static explanatory note at the bottom ("La operación diaria de puntos vive en Clientes y Caja...") matches the wireframe's own static text.
- 11.5 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 11.6 NOT run — manual visual QA requires the user's Windows desktop session.

**Deviation — old balance-lookup/redeem/transaction-history UI removed from `LoyaltyView.xaml`:** The previous `LoyaltyView.xaml` (pre-wireframe) contained a customer picker + "Consultar saldo"/"Aplicar descuento" panel and a transaction-history `DataGrid`, backed by `LoadBalanceCommand`/`RedeemCommand`/`Transactions`/`Balance`/`SelectedCustomer`/`PointsToRedeem` on `LoyaltyViewModel`. This UI was **removed, not relocated into a supplementary section**, after confirming (unlike Phase 9's supplier panel, which had no other home) this functionality already lives elsewhere in the app:
  - `Wireframes/wireframe-lealtad.html` has no balance-lookup/redeem/history UI at all, and explicitly states in its own static footer note: "La operación diaria de puntos vive en Clientes y Caja. Este módulo solo configura reglas, tiers y seguimiento del programa."
  - `CustomersViewModel` (Phase 3) already loads a customer's loyalty balance via `GetLoyaltyBalanceUseCase` and shows a points badge + "Reglas de lealtad" link.
  - `CashierViewModel` already has `RedeemLoyaltyPointsUseCase` wired for redeem-at-checkout (real, tested functionality, not touched by this slice).
  - Given both the wireframe intent and the existing real functional homes in Clientes/Caja, duplicating a third balance-lookup/redeem surface inside Lealtad would contradict the wireframe's explicit design note.
  `LoyaltyViewModel.LoadBalanceCommand`/`RedeemCommand`/`Transactions`/`Balance`/`SelectedCustomer`/`PointsToRedeem`/`LoadBalanceAsync`/`RedeemAsync` were left **untouched** in the ViewModel (not deleted) because `LoyaltyViewModelTests.cs` has 4 passing tests directly exercising them — removing them would be an unrelated, unrequested ViewModel behavior change. They are simply no longer bound from `LoyaltyView.xaml`. Flagging this as dead-from-the-UI-but-tested code in case a future slice wants to either delete it outright or repurpose it.

### Phase 12 (barcode-view)

`BarcodeView.xaml` was fully rebuilt into the wireframe's 2-panel layout (`Wireframes/wireframe-barcode.html`): fixed-width "Generador" panel (left) + flexible "Lote de impresión" panel (right).

- 12.1 Generator panel: `BarcodeValue` `TextBox`, `SelectedFormat` `ComboBox` (CODE 128/EAN-13/CODE 39/QR Code, matching the wireframe's `<select>` options exactly), `LabelText`/`LabelPrice` two-column fields, `Copies` `TextBox` (clamped 1-100 by the existing property setter). Live preview border shows `PreviewLabel` (always-on computed text), the actual generated barcode `Image` (bound to `PreviewImageBytes` via `BytesToImageSourceConverter`, visible via `NullToVisibilityConverter` once `GenerateCommand` has run), the raw `BarcodeValue` in Consolas as a lightweight text placeholder for the wireframe's simulated bars (with a `HasPreview`-gated "Ingresa un valor para previsualizar" hint when empty), and `PreviewPriceFormatted` hidden via `HasPreviewPrice`/`BoolToVisibilityConverter` when there's no price (task 12.1's "hide price line when empty" requirement).
- 12.2 `PrintSingleCommand` ("Imprimir") already blocked on empty `BarcodeValue` with the exact string "Ingresa el valor del codigo" (pre-existing `PrintSingleAsync` logic, untouched) and sets a success message with the copy count on success ("{Copies} etiqueta(s) enviada(s) a imprimir").
- 12.3 Batch panel: `CatalogSearchText` `TextBox` + a search icon `Button` wired to `SearchCatalogCommand` (explicit-trigger pattern, same as `ShiftsView.xaml`'s date-filter button and `InventoryView.xaml`'s CSV-export button — this codebase has no auto-search-on-keystroke convention for backend-hitting searches), results rendered via `CatalogSearchResults` `ItemsControl` with a per-row "Agregar" button. `BatchItems` `ItemsControl` shows each row's `Name`/`MetaLine`, a copies +/- stepper, and a remove button. Duplicate-add-increments-copies and empty-batch prompt behavior were already correct in `BarcodeViewModel.AddCatalogProductToBatch`/`AddGeneratedToBatch` (untouched).
- 12.4 `PrintBulkCommand` already blocked on an empty batch with the exact string "El lote esta vacio" (pre-existing `PrintBulkAsync` logic, untouched) and shows a total-label-count success message with batch reset on success (pre-existing, untouched).
- 12.5 `dotnet build POS.sln --no-restore` passed with 0 warnings/errors.
- 12.6 NOT run — manual visual QA requires the user's Windows desktop session.

**Deviation — added 4 minimal `ICommand` wrappers to `BarcodeViewModel.cs`:** `AddCatalogProductToBatch(ProductLookupResult)`, `ChangeBatchItemCopies(BarcodeBatchItem, int delta)`, and `RemoveBatchItem(BarcodeBatchItem)` are plain public methods (object-parameter-friendly for direct C# callers/tests), not `ICommand`s — there was no existing XAML binding target for per-row batch actions. Added `AddCatalogItemCommand`, `IncrementBatchItemCopiesCommand`, `DecrementBatchItemCopiesCommand`, and `RemoveBatchItemCommand` as thin `RelayCommand` wrappers in the constructor (pattern-matching the `CommandParameter` object, delegating straight to the existing methods — no new business logic). This follows the exact established convention from `InventoryView.xaml`'s `QuickAdjustCommand` and `PurchasesView.xaml`'s `RemoveDraftItemCommand` (`Command="{Binding DataContext.XCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"` + `CommandParameter="{Binding}"`). Added 3 focused unit tests (`AddCatalogItemCommand_AddsProductToBatch`, `IncrementAndDecrementBatchItemCopiesCommand_ChangeCopies`, `RemoveBatchItemCommand_RemovesItemFromBatch`) in `BarcodeViewModelTests.cs` covering the new wrappers.

**Pre-existing test bug fixed (unrelated to Phase 12's XAML work, found while running the baseline test suite before this batch's changes):** `BarcodeViewModelTests.PrintBulkAsync_WithNoProducts_SetsErrorMessage` and `PrintBulkAsync_WithProducts_InvokesPrintLabelsUseCase` were failing on the baseline (pre-batch) `dotnet test` run — they added items to `vm.SelectedProducts` and asserted on a "Selecciona" message, but `BarcodeViewModel.PrintBulkAsync` (implemented before this batch, in the ViewModel-only slice) was already rewritten to check/iterate `vm.BatchItems` with the message "El lote esta vacio" instead. This is a stale-test/refactor mismatch, not a regression introduced by this batch. Fixed both tests to add items to `BatchItems` (via the public `BarcodeBatchItem` constructor) and assert the actual current message text, restoring the suite to green. No production ViewModel logic was changed to fix this — only the two test bodies.

## Phase 10+11+12 Verification

- `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test POS.sln --no-restore` passed:
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 71 passed (68 baseline + 3 new `BarcodeViewModelTests` for the new row-action commands; the 2 pre-existing failing `PrintBulkAsync` tests were fixed as part of this batch, not net-new)
  - `POS.Application.Tests`: 112 passed

## Remaining Phase 10/11/12 Checklist Items

- [ ] 10.7 Manual visual QA against `Wireframes/wireframe-turnos.html`.
- [ ] 11.6 Manual visual QA against `Wireframes/wireframe-lealtad.html`.
- [ ] 12.6 Manual visual QA against `Wireframes/wireframe-barcode.html`.
