# Apply Progress: wireframe-to-wpf-xaml

## Work Unit

- Phase: 1 `shell-theme-foundation`
- Delivery: PR 1, stacked-to-main, `size:exception` pre-approved
- Mode: Standard Mode (`strict_tdd: false`)
- Date: 2026-07-01

## Completed

- 1.1 MaterialDesign packages added.
- 1.2 Theme dictionaries created and `App.xaml` converted to merged dictionaries.
- 1.3 Shared WPF controls created: `KpiCard`, `SubtabBar`, `QuickLink`; icon map published in `IconMap.cs`.
- 1.4 `NavigationShellView` rebuilt as topbar + funcbar shell; visual live clock added in code-behind.
- 1.5 `MainShellViewModel` restructured into `FuncbarModules` and `AdminHubModules`; Receivables shell state removed.
- 1.6 Receivables module UI removed from Presentation/Shared composition.
- 1.7 Dead `MainShellView.xaml` / `.xaml.cs` removed.
- 1.8 Presentation.Shared shell contract tests updated.
- 1.9 Automated build and test verification completed.

## Package Versions

- `MaterialDesignThemes`: 5.3.2
- `MaterialDesignColors`: 5.3.2

NuGet latest stable check on 2026-07-01 returned 5.3.2 for both packages. Prerelease 5.3.3-ci1401 was excluded.

## Verification

- `dotnet restore POS.sln` passed.
- `dotnet build POS.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test POS.sln --no-restore` passed:
  - `POS.Domain.Tests`: 43 passed
  - `POS.Presentation.Shared.Tests`: 54 passed
  - `POS.Application.Tests`: 112 passed

## Deviations / Open Items

- Task 1.6.4 remains unchecked. `rg "Receivable" src\POS.Presentation src\POS.Presentation.Shared` still finds reporting settlement fields in `ReportsView.xaml` and `ReportsViewModel.cs`. These are retained because the proposal/design explicitly keep sales/reporting receivable accounting intact while removing only the Receivables module UI.
- Task 1.9.3 remains unchecked. Manual visual QA requires running the WPF app on the user's Windows desktop session.
- Engram apply-progress creation could not be completed because the available Engram tool surface exposed `mem_search`, `mem_get_observation`, and `mem_update`, but no callable `mem_save`. The existing Engram tasks/design memories were updated where possible.

## Remaining Phase 1 Checklist Items

- [ ] 1.6.4 Decide whether to rename or hide reporting settlement terminology from Presentation/Shared, or accept the documented grep exception.
- [ ] 1.9.3 Manual visual QA on Windows.

## Next Recommended

Run `sdd-verify` for the implemented foundation slice, with visual QA as the manual verification item.
