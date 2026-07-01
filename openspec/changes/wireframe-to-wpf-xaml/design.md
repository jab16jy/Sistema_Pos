# Design: wireframe-to-wpf-xaml

## Technical Approach

Translate the 12 approved HTML wireframes into the real WPF app **without moving a single business rule out of Application/Domain**. Every module View is pure XAML that binds to a ViewModel that **already exists** in `POS.Presentation.Shared`; the ViewModels already coordinate Application use cases (see `App.xaml.cs` `BuildMainShellViewModel`, which composes 15 module ViewModels through DI). This change is a Presentation-layer re-skin plus one information-architecture (IA) restructure, delivered as a shared **foundation slice** followed by 12 per-module view slices.

The wireframe design system is the north star. The good news confirmed in exploration: the wireframe CSS tokens are nearly identical to the palette already inlined in `App.xaml` (`--primary #0369A1` == `ColorPrimary`, `--secondary #0F766E` == `ColorAccent`, `--surface #FFFFFF`, `--outline #D4D4D8` == `ColorBorder`, `--on-surface #18181B` == `ColorText`). So we **extend** the existing theme with MaterialDesignInXamlToolkit and a token-mapped palette rather than re-theming from scratch. The two structural gaps that require real work are (1) the shell: current 300px left sidebar vs. wireframe topbar (48px) + funcbar (44px), and (2) `MainShellViewModel`'s flat 15-item module list vs. the wireframe's two-level IA (5-item funcbar + Admin hub).

Clean Architecture inward-dependency rule (`pos-clean-architecture`) and MVVM boundaries (`pos-wpf-mvvm`) are hard constraints: WPF sends actions to ViewModels, ViewModels call use cases, no SQLite/EF/business calculation in XAML or code-behind. `POS.Presentation.Shared` stays WPF-type-free to keep the theoretical Avalonia door open.

## Architecture Decisions (ADRs)

| # | Decision | Choice | Alternatives rejected | Rationale |
|---|----------|--------|-----------------------|-----------|
| 1 | Theming library | Adopt **MaterialDesignInXamlToolkit** (`MaterialDesignThemes` + `MaterialDesignColors`) 5.x line, .NET 9 compatible. | (a) Keep hand-rolled styles in `App.xaml` only; (b) ModernWpf/HandyControl. | Wireframes assume a Material system (Material Symbols icons, elevation, ripple). MaterialDesign is the de-facto WPF Material toolkit, actively maintained on the 5.x/.NET 9 line, and composes via `ResourceDictionary` merge so it layers on top of the existing token-aligned palette with minimal churn. |
| 2 | Theme composition | Merge `BundledTheme` (Light) + a **custom palette dictionary** that maps Material `PrimaryColor`/`SecondaryColor` to the existing tokens, then keep the app's own component styles (Card/Button/TextBox/chip/subtab/funcbar) in a dedicated `Themes/` dictionary set. Do **not** delete the existing custom styles wholesale. | Replace all inline `App.xaml` styles with pure MaterialDesign defaults. | The existing styles already match the wireframe tokens and are consumed by every current View; ripping them out would break all 12 views at once and blow past the slice budget. Additive layering keeps each module slice independently shippable. |
| 3 | Palette source of truth | Wireframe CSS tokens become named `Color`/`SolidColorBrush` resources; MaterialDesign primary/secondary point at those same tokens. | Let MaterialDesign generate a palette from a seed color. | Single source of truth for color prevents drift between Material-styled controls and the hand-rolled chip/funcbar styles. |
| 4 | Shell layout | Rebuild `NavigationShellView` as `DockPanel`: topbar `DockPanel.Dock=Top` (48px) + funcbar `DockPanel.Dock=Bottom` (44px) + central content region that swaps by selected primary section. Remove the 300px sidebar `ColumnDefinition`. | Keep sidebar; add a topbar on top of it. | The wireframe IA is fundamentally two-bar (qutebrowser-style); a hybrid would satisfy neither design and confuse operators. |
| 5 | IA model | Restructure `MainShellViewModel` into **funcbar primary items** (Caja/F1, Clientes/F4, Cotizaciones/F6, Facturas/F8, Admin/ADM) + an **Admin hub** that fans out to the remaining modules (Productos, Inventario, Compras, Reportes, Turnos, Lealtad, Barcode) via quick-links. Introduce a lightweight grouping over `ModuleNavigationItem` rather than changing the record. | Rewrite `ModuleNavigationItem` shape; or keep flat 15-item list and only restyle. | The record is consumed by tests and DI wiring; keeping its shape (`Key/Title/Description/Shortcut/IsImplemented`) minimizes blast radius. Grouping is expressed as two `ObservableCollection`s (funcbar primary + admin-hub) derived from the same source list. |
| 6 | Receivables | **Remove UI only** (see removal boundary below). | Full cross-layer deletion. | `Receivable` is domain-woven (CreateSale credit path, CloseCashRegister, reporting, EF migrations). Full deletion breaks sales/reporting/migrations — out of scope, high blast radius. |
| 7 | Dead code | **Delete** `MainShellView.xaml`/`.xaml.cs`. | Leave as-is. | Unreferenced legacy prototype; `App.xaml.cs` launches `NavigationShellView`. Verify-before-delete via solution-wide grep. |
| 8 | Per-module pattern | One reusable View/ViewModel checklist + a shared reusable XAML component set (KPI card, chip, subtab bar, quick-link, topbar/funcbar chrome) authored **in the foundation slice**, consumed by all 12 module slices. | Let each of 12 slices invent its own chip/card/subtab. | Prevents 12 divergent reimplementations, keeps each module slice within the 400-line budget, guarantees visual consistency. |
| 9 | Test contract collision | Do **not** silently rewrite `MainShellViewModelTests`. Coordinate the module-count assertion change with the open `complete-architecture-design-foundation` change (see Coordination Risk). | Bump the count freely. | Two in-flight changes touch the same test file with different module contracts; uncoordinated edits cause merge conflicts and a broken contract. |

## MaterialDesign Integration (detail)

### Packages (`Directory.Packages.props`)

Add to the existing `<ItemGroup>` (central package management is already in force — versions live here, `PackageReference` in the csproj carries no `Version`):

```xml
<PackageVersion Include="MaterialDesignThemes" Version="5.2.1" />
<PackageVersion Include="MaterialDesignColors" Version="3.1.1" />
```

`POS.Presentation.csproj` gains two version-less references:

```xml
<PackageReference Include="MaterialDesignThemes" />
<PackageReference Include="MaterialDesignColors" />
```

> **Apply-time verification (required):** confirm the latest stable 5.x `MaterialDesignThemes` + matching `MaterialDesignColors` that target `net9.0-windows` at the moment of implementation (`dotnet add package` / NuGet). `MaterialDesignThemes` 5.x is the .NET 8/9 line; 5.2.1 / colors 3.1.1 is the design target — pin the verified pair in the foundation slice PR and record the exact resolved versions in apply-progress. `MaterialDesignColors` must be the version the chosen `MaterialDesignThemes` depends on (they are released as a pair).

### `App.xaml` composition

Convert `Application.Resources` to a `ResourceDictionary` with `MergedDictionaries`. Order matters (later dictionaries win):

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <!-- 1. MaterialDesign bundled Light theme -->
      <materialDesign:BundledTheme BaseTheme="Light"
                                   PrimaryColor="Cyan" SecondaryColor="Teal"
                                   xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes" />
      <!-- 2. MaterialDesign default control templates -->
      <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" />
      <!-- 3. App palette: wireframe tokens (overrides Material primary/secondary swatches) -->
      <ResourceDictionary Source="Themes/Palette.xaml" />
      <!-- 4. App component styles: Card, chip, subtab, funcbar, topbar, buttons, inputs, DataGrid -->
      <ResourceDictionary Source="Themes/Controls.xaml" />
    </ResourceDictionary.MergedDictionaries>
    <!-- converters stay here (already registered) -->
  </ResourceDictionary>
</Application.Resources>
```

`Palette.xaml` re-declares Material's primary/secondary brushes against the wireframe tokens so Material-styled controls and hand-rolled controls share one color source:

- `PrimaryHueMidBrush` -> `#0369A1` (`--primary`), `PrimaryHueDarkBrush` -> `#075985`.
- `SecondaryHueMidBrush` -> `#0F766E` (`--secondary`).
- Keep the existing named brushes (`BrushPrimary`, `BrushAccent`, `BrushBorder`, `BrushMuted`, `BrushCard`) so no existing View binding breaks.

`Controls.xaml` carries the current `App.xaml` styles (Card, Button/PrimaryButton/AccentButton, TextBox, PasswordBox, ComboBox, DataGrid, MutedText, CardShadow) **verbatim first**, then adds the new wireframe-derived styles (below). Extraction of the existing inline styles into `Controls.xaml` is a pure move (no behavior change) done in the foundation slice.

### Token -> resource map

| Wireframe token | Value | WPF resource | Notes |
|---|---|---|---|
| `--primary` | `#0369A1` | `ColorPrimary` / `BrushPrimary` / Material `PrimaryHueMid` | already present |
| `--primary-container` | `#E0F2FE` | new `ColorPrimaryContainer` / `BrushPrimaryContainer` | chip/quick-link hover, funcbar active bg |
| `--secondary` | `#0F766E` | `ColorAccent` / `BrushAccent` / Material `SecondaryHueMid` | already present (named "accent") |
| `--secondary-container` | `#CCFBF1` | new `BrushSecondaryContainer` | |
| `--surface` / `--surface-container` / `--surface-variant` | `#FFFFFF` / `#F4F4F5` / `#E8EAED` | `BrushSurface` / new `BrushSurfaceContainer` / `BrushSurfaceAlt` | surfaceAlt already present |
| `--on-surface` / `--on-surface-muted` | `#18181B` / `#71717A` | `BrushText` / `BrushMuted` | already present |
| `--outline` | `#D4D4D8` | `BrushBorder` | already present |
| `--success` / `--success-container` | `#16A34A` / `#DCFCE7` | new `BrushSuccess` / `BrushSuccessContainer` | stock chips |
| `--warning` / `--warning-container` | `#D97706` / `#FEF3C7` | new `BrushWarning` / `BrushWarningContainer` | stock chips |
| `--error` / `--error-container` | `#DC2626` / `#FEE2E2` | `BrushDanger` / `BrushDangerSoft` | already present (named "danger") |
| radius | `4px` (controls) / `6px` (cards) | `CornerRadius` already 4/6 in styles | matches |
| shadow | soft elevation | `CardShadow` (already defined) + Material elevation | matches |
| `--topbar-h` / `--funcbar-h` | `48px` / `44px` | fixed row heights in shell | |
| spacing `--s1..--s6` | `4..24px` | Thickness conventions in component styles | document as 4/8/12/16/20/24 |
| fonts | `Segoe UI`/`Inter`, mono `Consolas` | Window FontFamily `Segoe UI` (present); add a `MonoText` style for numeric/kbd hints | Material Symbols font optional; see risk |

## Shell / IA Rework (detail)

### Layout

`NavigationShellView.xaml` root becomes a `DockPanel`:

- **Topbar (48px, `Dock=Top`)**: left = app icon (20px primary square) + app name; center = current section title (`SelectedModule.Title` / section title); right = operator avatar/initials (`OperatorDisplayName`) + a live clock + logout affordance.
- **Funcbar (44px, `Dock=Bottom`)**: an `ItemsControl` of 5 primary items rendered as `funcbar-item` (F-key badge + label), with a separator between items and an `active` visual state bound to the current primary section.
- **Content region (`*`)**: swaps by selected section. Caja/Clientes/Cotizaciones/Facturas render their module View directly; **Admin** renders the Admin hub (subtab bar + KPI strip + quick-link grid) which is itself the `configuracion-admin-hub` slice.

### F-key binding

Add `Window.InputBindings` `KeyBinding`s to `NavigationShellView`: `F1`->Caja, `F4`->Clientes, `F6`->Cotizaciones, `F8`->Facturas, a chosen key (e.g. `F12` or `Ctrl+A`) or the ADM funcbar button -> Admin hub. Each `KeyBinding` invokes `SelectModuleCommand` with the target `ModuleNavigationItem`. Keep it declarative in XAML; no code-behind logic beyond what WPF `InputBinding` requires. (Note: existing `ModuleNavigationItem` shortcuts are F2/F3/F5/... — these are re-mapped to the wireframe's F1/F4/F6/F8 in the foundation slice.)

### `MainShellViewModel` changes (minimal, test-aware)

- Keep the record `ModuleNavigationItem` and the master module list. Derive two projections:
  - `FuncbarModules` (ordered: cashier, customers, quotes, invoices, admin-entry) with wireframe shortcuts.
  - `AdminHubModules` (products, inventory, purchases, reports, shifts, loyalty, barcode) for the hub grid.
- Drop `receivables` from the master list and delete `IsReceivablesSelected` + its `OnPropertyChanged` and the `Receivables` init property (removal boundary below).
- Keep `SelectedModule`, `SelectModuleCommand`, `IsXxxSelected`, dashboard metrics, and `LoadDashboardMetricsAsync` intact so `CashierView`, dashboard KPIs, and existing tests keep passing.
- The 5 funcbar sections plus the admin hub are a **presentation projection** of the existing keys — no new use cases, no domain change.

> The IA restructure and the `IsReceivablesSelected` removal must be done **together** in the foundation slice: the boolean `IsXxxSelected` properties are consumed by `NavigationShellView` bindings, so a half-migrated state breaks the shell. Build + run `POS.Presentation.Shared.Tests` after.

### Coordination Risk — `MainShellViewModelTests` collision (do NOT resolve here)

The open, unrelated change `openspec/changes/complete-architecture-design-foundation` **also edits `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs`** — its design asserts a **9-module** shell contract (and presence of `quotes`). The current committed test asserts **`ActiveShellModuleCount = 15`**. This change lowers the count again (drops `receivables`; the wireframe IA collapses the visible/module set). These are two different module-count contracts landing on the same file.

- **This is a flagged coordination risk, not resolved in this design.** Whichever change merges second must rebase the assertion to the union-consistent contract.
- Recommended: the foundation slice updates the count assertion to match the **post-Receivables-removal** module count and re-scopes any `receivables`-referencing test setup, and the PR description explicitly calls out the interaction with `complete-architecture-design-foundation`. Do not delete the 9-module intent of the other change; align, don't clobber.

## ReceivablesView UI Removal — precise boundary

**Delete / edit (Presentation only):**

| File | Action |
|---|---|
| `src/POS.Presentation/Views/ReceivablesView.xaml` | Delete |
| `src/POS.Presentation/Views/ReceivablesView.xaml.cs` | Delete |
| `src/POS.Presentation.Shared/ViewModels/ReceivablesViewModel.cs` | Delete |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Remove `receivables` list entry, `Receivables` init property, `IsReceivablesSelected` + its `OnPropertyChanged` line, and the `receivables` arm of `UpdateOperatorMessage` |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Remove the `IsReceivablesSelected` content `Grid` block |
| `src/POS.Presentation/App.xaml.cs` | Remove the `ReceivablesViewModel` construction block + `Receivables = receivables` initializer |
| Master module list `F7` shortcut | Removed with the list entry |

**Do NOT touch (Application/Domain/Infrastructure stay intact — explicit):**

`IReceivableQueryService`, `IApplyReceivablePaymentUseCase` / `ApplyReceivablePaymentUseCase`, `ReceivableRepository`, `ReceivableLookupQueryService`, `ReceivableSummary` DTO, DI registrations for those services in Infrastructure, `AppDbContext`, and all EF migrations. These remain because `CreateSaleUseCase` (credit sales), `CloseCashRegisterUseCase`/cash-closing, and reporting depend on them. The module simply disappears from the operator UI; sales-created receivable data stays consistent.

**Verification:** after removal, `grep -r "Receivable" src/POS.Presentation` and `src/POS.Presentation.Shared` should return **zero** matches (Presentation clean); the same grep across `POS.Application`/`POS.Domain`/`POS.Infrastructure` should still return matches (backend intact). Build + full test suite green.

## Dead-Code Removal — `MainShellView`

Delete `src/POS.Presentation/Views/MainShellView.xaml` and `.xaml.cs`.

**Verification method (state in the foundation-slice PR):** run a solution-wide search `grep -rn "MainShellView" .` (or Grep tool) **before** deletion. Expected: matches only inside `MainShellView.xaml`/`.xaml.cs` themselves (self-reference via `x:Class`/`InitializeComponent`). `App.xaml.cs` references `NavigationShellView`, not `MainShellView`. If any other reference appears, stop and re-scope. After deletion, `dotnet build POS.sln` must succeed.

## Per-Module View/ViewModel Pattern (the reusable contract for all 12 slices)

The foundation slice authors a **shared component library** in `Themes/Controls.xaml` (+ small `UserControl`s under `src/POS.Presentation/Controls/`) so the 12 module slices consume, never reinvent:

**Shared reusable components (authored once, in foundation):**
- `KpiCardStyle` / `KpiCard` UserControl — label + value + hint (already prototyped as dashboard metric card; generalize it).
- `ChipStyle` with `success`/`warning`/`error`/`neutral` variants (stock chips, statuses) — driven by a brush key or a small enum-to-brush converter.
- `SubtabBar` UserControl / `SubtabItemStyle` — the 40px subtab strip with active underline (Admin hub, and any module with sections).
- `QuickLinkStyle` / `QuickLink` UserControl — icon + title + subtitle card with primary hover (Admin hub grid, reused anywhere).
- `TopbarChrome` / `FuncbarItem` styles — shell chrome (foundation-owned; module slices don't touch them).
- `CardStyle` (exists), `PrimaryButton`/`AccentButton` (exist), `MonoText` (new, for numeric/kbd hints), section-header text styles.

**Per-module slice checklist (identical for all 12):**
1. ViewModel already exists in `POS.Presentation.Shared` (confirmed in `App.xaml.cs`). **Do not rebuild it.** If the wireframe surfaces a field/command the VM lacks, add a *presentation-only* property/command that delegates to an existing use case — never add business logic. If it needs a new use case, that is out of scope and must be flagged (the proposal says behavior is unchanged).
2. Rebuild the `*View.xaml` against MaterialDesign controls + the shared component library to match the wireframe (cards, chips, subtabs, KPI grid).
3. Bind only to existing VM members; no SQLite/EF/calculation in XAML or code-behind. Code-behind limited to purely-visual concerns (focus, scanner input) per `pos-wpf-mvvm`.
4. Keep `POS.Presentation.Shared` WPF-type-free.
5. Build on Windows + manual visual QA against the wireframe before the slice PR merges.
6. Stay within the 400-line budget; if the wireframe forces more, sub-split (flagged slices below).

## Clean Architecture Boundary Enforcement

- No business logic enters XAML or code-behind. All module ViewModels remain in `POS.Presentation.Shared` and continue calling Application use cases (dependency rule: Presentation -> Application -> Domain).
- `POS.Presentation.Shared` continues to target plain `net9.0` (not `-windows`) and must not gain WPF type references — MaterialDesign types live only in `POS.Presentation` (`net9.0-windows`).
- The Receivables removal touches **only** Presentation; Application/Domain/Infrastructure contracts are untouched, proving the boundary holds.
- Review checklist per slice (from `pos-clean-architecture`): Domain has no UI/persistence dependency; Presentation uses commands/bindings not DB calls; ViewModels testable without WPF.

## Data Flow (unchanged by this design)

```text
WPF View (Material XAML) ──binds──> Shared ViewModel ──calls──> Application UseCase
        │                                   │                          │
        └──── displays result/validation ◄──┴──── Infrastructure repos ─┘
                                                          │
                                                     SQLite / AppDbContext
```

This change alters only the left-most box (View XAML) and the shell projection in `MainShellViewModel`. Use cases, repositories, DTOs, and persistence are untouched (except Receivables *UI* unhook).

## Chained-PR / Stacked-to-Main Sequencing (13 slices)

Delivery: chained PRs, **stacked-to-main**, one capability per PR, work-unit commits, each PR carrying a dependency diagram marking itself. Order and what each slice unblocks:

1. **shell-theme-foundation** (prerequisite) — MaterialDesign packages + `BundledTheme`, `Themes/Palette.xaml` + `Themes/Controls.xaml`, **the shared component library** (KPI card, chip, subtab, quick-link, topbar/funcbar chrome, mono text), topbar+funcbar shell rebuild, flat->funcbar+Admin-hub IA, Receivables UI removal, `MainShellView` deletion, test-contract alignment. **Unblocks every module slice** by publishing the reusable components + the shell they render inside. *Likely >400 lines -> sub-split at sdd-tasks:* (1a) theme/deps + component library, (1b) shell rebuild + IA + Receivables removal + dead-code.
2. **caja-view** — validates the new shell + component library end-to-end (smallest gap): stock chips, row-hover actions, search polish, wire F6/F7 buttons.
3. **clientes-view** (F4), **cotizaciones-view** (F6), **facturas-view** (F8) — the other funcbar-primary modules; consume chip/card/subtab. *clientes likely >400 -> sub-split.*
4. **configuracion-admin-hub** — the Admin hub target (subtab bar + KPI strip + quick-link grid to the 7 remaining modules). Must land after the shell so the ADM funcbar entry has a destination; unblocks navigation to catalog/ops/utility modules. *Likely >400 -> sub-split.*
5. **productos-view**, **inventario-view**, **compras-view** — catalog/ops, reached via Admin hub. *productos + inventario likely >400 -> sub-split; watch compras.*
6. **turnos-view**, **lealtad-view**, **barcode-view**, **reportes-view** — utility/admin-hub modules, lowest daily frequency. *watch turnos.*

Each module slice depends transitively only on slice 1 (components + shell). Slices 2–13 are otherwise independent and revert individually. If slice 1 regresses, revert it and pause dependents.

## File Changes (foundation slice)

| File | Action | Description |
|---|---|---|
| `Directory.Packages.props` | Modify | Add `MaterialDesignThemes` + `MaterialDesignColors` versions |
| `src/POS.Presentation/POS.Presentation.csproj` | Modify | Add the two version-less `PackageReference`s |
| `src/POS.Presentation/App.xaml` | Modify | `MergedDictionaries`: BundledTheme + Material defaults + `Palette.xaml` + `Controls.xaml`; keep converters |
| `src/POS.Presentation/Themes/Palette.xaml` | Create | Token colors/brushes; map Material primary/secondary to `--primary`/`--secondary` |
| `src/POS.Presentation/Themes/Controls.xaml` | Create | Existing styles moved verbatim + new chip/subtab/funcbar/topbar/KPI/quick-link/mono styles |
| `src/POS.Presentation/Controls/*.xaml(.cs)` | Create | Optional UserControls: KpiCard, SubtabBar, QuickLink (view-only) |
| `src/POS.Presentation/Views/NavigationShellView.xaml` | Modify | Sidebar -> DockPanel topbar(48)+funcbar(44)+content; F-key `InputBindings`; remove Receivables block |
| `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs` | Modify | Funcbar/Admin-hub projections; drop `receivables`/`IsReceivablesSelected` |
| `src/POS.Presentation/Views/MainShellView.xaml(.cs)` | Delete | Dead code |
| `src/POS.Presentation/Views/ReceivablesView.xaml(.cs)` | Delete | UI unhook |
| `src/POS.Presentation.Shared/ViewModels/ReceivablesViewModel.cs` | Delete | UI unhook |
| `src/POS.Presentation/App.xaml.cs` | Modify | Remove Receivables construction + initializer |
| `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs` | Modify | Align module-count/contract post-Receivables-removal (coordinate with `complete-architecture-design-foundation`) |

## Testing Strategy

| Layer | What to test | Approach |
|---|---|---|
| Unit | Shell module contract (funcbar set, admin-hub set, no `receivables`, cashier default selected), `SelectModuleCommand`, dashboard metrics unchanged. | xUnit in `POS.Presentation.Shared.Tests` with existing fakes. |
| Build | Material dictionaries resolve; all 12 views + shell compile; no WPF types leaked into Shared. | `dotnet build POS.sln` on Windows. |
| Visual | Each module View matches its wireframe (topbar/funcbar, chips, cards, subtabs). | Manual visual QA on the user's Windows machine per slice before merge (no automated render path). |
| Regression | Sales/reporting/cash-closing still build + pass after Receivables UI removal. | Full `dotnet test` — Domain/Application/Infrastructure suites run on any OS. |

## Additional Architecture Risks (found during design)

1. **Material Symbols icon font** — wireframes use the `Material Symbols Outlined` web font. WPF has no web-font fallback; MaterialDesign ships `PackIcon` (Material Design Icons set), whose glyph names differ from Material Symbols. **Mitigation:** map each wireframe icon to the nearest `PackIconKind` in the foundation slice and publish an icon-mapping table; do not attempt to load the web font. Flag any missing glyph.
2. **`App.xaml` style-key collisions** — MaterialDesign defines many implicit `TargetType` styles (Button, TextBox, DataGrid). The app's existing implicit styles (no `x:Key`) will conflict/override. **Mitigation:** decide per control whether to base on Material (`BasedOn="{StaticResource MaterialDesign...}"`) or keep the app style; where kept, ensure the app style loads *after* Material defaults in the merge order. Validate visually.
3. **`ResourceDictionary` conversion regressions** — moving inline `App.xaml` resources into merged dictionaries can break `StaticResource` lookups if ordering is wrong (e.g., converters referenced before declared, brushes referenced across dictionaries). **Mitigation:** keep converters in the root `Resources`, brushes/styles in merged dictionaries loaded before views; smoke-test app launch in the foundation slice.
4. **Funcbar vs. existing shortcut map** — current `ModuleNavigationItem` shortcuts (F2/F3/F5/F9/...) differ from the wireframe (F1/F4/F6/F8). Re-mapping may collide with any existing global key handling (e.g., Caja's own `F1` = focus search in the wireframe JS). **Mitigation:** resolve F-key ownership between shell nav and in-module handlers in the foundation slice; document the final key map.
5. **Admin hub is both a funcbar section and a module container** — the `configuracion-admin-hub` slice is architecturally coupled to the shell IA (the ADM funcbar entry's destination). **Mitigation:** already sequenced after the shell foundation; do not attempt in parallel.
6. **Two in-flight changes on the shell/test** — beyond the test-file collision, `complete-architecture-design-foundation` also modifies `NavigationShellView.xaml`, `MainShellViewModel.cs`, and `App.xaml.cs`. **Mitigation:** treat merge order as a release-coordination item; whichever lands second rebases against the other. Flagged, not resolved here.
7. **Windows-only render loop** — no way to verify XAML rendering off Windows; each slice needs manual QA. (Already in proposal; restated as the dominant delivery risk.)

## Open Questions

- [ ] Confirm the exact final `MaterialDesignThemes`/`MaterialDesignColors` pair at apply time (net9.0-windows compatible) and pin in the foundation PR.
- [ ] Confirm the F-key assigned to the Admin hub (wireframe shows `ADM` label, no explicit F-key).
- [ ] Confirm the post-removal module-count contract with the `complete-architecture-design-foundation` change owner before editing `MainShellViewModelTests`.
