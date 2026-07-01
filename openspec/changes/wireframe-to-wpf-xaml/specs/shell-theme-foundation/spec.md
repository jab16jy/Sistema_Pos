# Shell Theme Foundation Specification

## Purpose

Defines the MaterialDesignInXamlToolkit adoption, the topbar+funcbar shell layout that replaces the 300px left sidebar, the 5-item funcbar + Admin hub navigation IA, and the removal of Receivables UI and dead shell code. This is the prerequisite slice for all 12 module views.

## Requirements

### Requirement: MaterialDesign theme with token-mapped palette

The application MUST reference MaterialDesignThemes and MaterialDesignColors via `Directory.Packages.props` and `POS.Presentation.csproj`, and MUST configure a `BundledTheme` with resource dictionaries mapping to the wireframe design tokens (`--primary #0369A1`, `--secondary #0F766E`, `--surface #FFFFFF`, `--surface-container #F4F4F5`, `--outline #D4D4D8`, `--on-surface #18181B`, `--error #DC2626`, `--warning #D97706`, `--success #16A34A`).

#### Scenario: Application builds with MaterialDesign styles applied

- GIVEN the solution has MaterialDesignThemes and MaterialDesignColors package references
- WHEN the app starts on Windows
- THEN `App.xaml` MUST load a `BundledTheme` and card/button/chip styles MUST resolve colors from the token-mapped dictionaries
- AND the app MUST NOT crash due to missing resource keys

#### Scenario: Existing App.xaml palette is extended, not replaced

- GIVEN `App.xaml` already defines `ColorPrimary #0369A1` and `ColorAccent #0F766E`
- WHEN the Material theme dictionaries are added
- THEN the existing primary/accent hex values MUST remain consistent with the wireframe tokens
- AND no visual regression MUST occur for colors already matching wireframe tokens

### Requirement: Topbar replaces the left sidebar header area

The shell MUST render a 48px-high topbar with: app icon + "Esthetic POS" name on the left, the active module title centered, and an operator avatar + live clock (`HH:mm:ss`, updating every second) on the right.

#### Scenario: Topbar shows current module title

- GIVEN the operator navigates to the Caja module
- WHEN `NavigationShellView` renders
- THEN the topbar center text MUST display "Caja"
- AND switching to Clientes MUST update the center text to "Clientes"

#### Scenario: Clock updates live

- GIVEN the shell is running
- WHEN one second elapses
- THEN the topbar clock MUST advance by one second without requiring navigation

### Requirement: Funcbar replaces the 300px left sidebar navigation

The shell MUST render a 44px-high bottom funcbar with exactly 5 items in this order: `F1 · Caja`, `F4 · Clientes`, `F6 · Cotizaciones`, `F8 · Facturas`, `ADM · Admin`, separated by vertical dividers. The shell MUST NOT render a left sidebar `ListBox` of modules.

#### Scenario: Funcbar item shows active state

- GIVEN the operator is on the Caja view
- WHEN the funcbar renders
- THEN the `F1 · Caja` item MUST show the active style (primary-colored bottom border, primary-colored key/label)
- AND the other 4 items MUST NOT show the active style

#### Scenario: Funcbar navigation switches modules

- GIVEN the operator is on any module view
- WHEN the operator clicks `F6 · Cotizaciones` or presses the `F6` key
- THEN the shell MUST navigate to the Cotizaciones view
- AND the funcbar active indicator MUST move to `F6 · Cotizaciones`

### Requirement: Flat 15-item module list is restructured into 5-item funcbar plus Admin hub

`MainShellViewModel` MUST expose only the 5 funcbar-primary modules (cashier, customers, quotes, invoices, and the Admin hub entry point) as direct navigation targets. All other existing modules (products, inventory, suppliers, reports, loyalty, barcode, shifts) MUST be reachable only through the Admin hub, not as independent funcbar/sidebar items.

#### Scenario: Non-funcbar modules are not directly navigable from the shell chrome

- GIVEN the shell is initialized
- WHEN the funcbar is inspected
- THEN it MUST contain exactly 5 items
- AND Productos, Inventario, Compras, Turnos, Lealtad, Barcode, Reportes MUST NOT appear as funcbar items

### Requirement: Receivables UI is removed from the shell

The shell MUST NOT expose a Receivables navigation entry, `F7` shortcut, or `IsReceivablesSelected` binding. `ReceivablesView.xaml`/`.xaml.cs` and `ReceivablesViewModel` MUST be deleted from the Presentation layer. Domain/Application/Infrastructure Receivables code MUST remain untouched.

#### Scenario: Receivables is absent from navigation

- GIVEN the shell is initialized
- WHEN navigation modules are inspected
- THEN no module with key `receivables` MUST exist
- AND no `F7` shortcut MUST be bound to a Receivables view

#### Scenario: Sales and reporting still build after Receivables UI removal

- GIVEN `CreateSaleUseCase`, `CloseCashRegisterUseCase`, and reporting services still reference `Receivable` domain types
- WHEN the solution is built after removing the Receivables UI
- THEN the build MUST succeed
- AND existing sales/reporting tests MUST still pass

### Requirement: Dead MainShellView code is deleted

`MainShellView.xaml` and `MainShellView.xaml.cs` MUST be deleted from the Presentation project, since `App.xaml.cs` starts `NavigationShellView` and no other component references `MainShellView`.

#### Scenario: Application starts without MainShellView

- GIVEN `MainShellView.xaml(.cs)` has been deleted
- WHEN the application starts
- THEN `App.xaml.cs` MUST resolve `NavigationShellView` as the startup window
- AND the build MUST NOT reference the deleted files anywhere
