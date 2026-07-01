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

Run `sdd-verify` for the implemented Phase 5+6 slice (PR 4), with visual QA as the manual verification item for both modules. Continue to Phase 7 (PR 5: productos-view + inventario-view + compras-view) only after PR 4 review/merge.
