# Proposal: Complete Architecture Design Foundation

## Proposal question round
Because this run is planning-only and the requested first slice is explicit, proposal assumes: (1) cashier sales must persist real sales before adding advanced POS modules; (2) quotes must be usable from Avalonia, not only Application tests; (3) implementation will not ship in this PR. Product questions for review: What invoice numbering policy is required? Which payment methods must affect cash drawer metrics? Should quote conversion reserve or decrement inventory only when converted to sale?

## Intent
Turn the verified missing-work plan into reviewable SDD planning for the first three gaps: stale shell verification, real cashier sale persistence, and usable quotation workflows. Success means proposal/spec/design/tasks define the work without production implementation.

## Scope
### In Scope
- Repair `MainShellViewModelTests` expectations for 9 modules including `quotes`.
- Plan cashier UI/ViewModel integration with `CreateSaleUseCase`, SQLite persistence for sales/payments/items/inventory, real invoice numbers, and dashboard metrics from real data.
- Plan Avalonia quote/history/send views, `quotes` navigation, real customer contact, create/search/convert/send UI flows.

### Out of Scope
- Production implementation or runtime migrations in this planning PR phase.
- Reports, printing, auth, backup, suppliers, and settings beyond dependencies.

## Capabilities
### New Capabilities
- `shell-navigation-verification`: Shell module contract and navigation-test alignment.
- `cashier-sales-flow`: Cashier sale confirmation, persistence, invoice numbering, inventory, and dashboard metrics.
- `quotations-ui-flow`: Avalonia quote creation, history, conversion, sending, and customer contact usage.

### Modified Capabilities
- None; `openspec/specs/` has no existing spec files.

## Approach
Use Clean Architecture boundaries: Presentation.Shared ViewModels orchestrate Application use cases; Infrastructure owns EF Core/SQLite repositories and unit-of-work behavior; Avalonia views bind to shared ViewModels. Specs/design/tasks will split planning from implementation and respect strict TDD.

## Affected Areas
| Area | Impact | Description |
|------|--------|-------------|
| `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs` | Modified | Update shell contract for 9 modules. |
| `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs` | Modified | Replace prototype confirmation with real sale use case planning. |
| `src/POS.Infrastructure/Persistence`, `Repositories` | Modified | Persist sale/payment/item/inventory state via SQLite. |
| `src/POS.Presentation.Avalonia/Views` | New/Modified | Add quote/history/send UI surfaces. |
| `src/POS.Presentation.Shared/ViewModels/Quote*.cs`, `SendDocumentViewModel.cs` | Modified | Wire UI flows to real quote/contact data. |

## Risks
| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Scope exceeds one reviewable PR | High | Keep this PR planning-only; tasks forecast slices. |
| Invoice/contact rules are ambiguous | Medium | Record assumptions and require spec scenarios. |
| WPF verification is limited on Linux | Medium | Verify shared/Avalonia layers on Linux; note WPF Windows requirement. |

## Rollback Plan
Revert `openspec/changes/complete-architecture-design-foundation/` and Engram topic `sdd/complete-architecture-design-foundation/proposal`; no runtime behavior changes are made.

## Dependencies
- Existing `sdd-init/sistema_pos` hybrid context.
- Existing Application/Domain use cases for sales, quotes, and document sending.

## Success Criteria
- [ ] Proposal, specs, design, and tasks exist in hybrid storage.
- [ ] Planning covers all three requested missing-work items.
- [ ] Tasks explicitly prevent production implementation while allowing this SDD planning commit and PR.
