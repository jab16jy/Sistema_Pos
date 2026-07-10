# Design: Bulk Category Assignment

## Technical Approach

The Categories subsection remains embedded in the Products module. The WPF view binds only to `CategoriesViewModel`; that ViewModel searches products and sends a focused request to an Application use case. The use case validates the target category and every requested product before changing domain state, then calls the unit of work once when there is work to save.

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| UI location | Extend the existing Categories subsection. | Categories already belong to product organization; a separate shell module would duplicate navigation. |
| Business operation | Add `AssignProductsToCategoryUseCase`. | Reassignment is business behavior, not WPF behavior. |
| Repository contract | Reuse `IProductRepository.GetByIdAsync` for each selected ID. | Avoids widening a widely used repository interface for a focused batch workflow. |
| Atomic validation | Load all selected products before assigning any category. | A missing product leaves all earlier products unchanged. |
| Persistence | Save once only when one or more products change. | Keeps the batch operation coherent and avoids redundant writes. |
| Target categories | Show only active categories in the UI. | Operators should not assign catalog items to inactive categories. Existing Application category validation remains consistent with product create/update behavior by requiring existence. |

## Data Flow

```text
WPF Categories tab
  -> CategoriesViewModel
  -> SearchProductsUseCase / AssignProductsToCategoryUseCase
  -> Product.AssignCategory + IUnitOfWork
  -> EF Core repositories / SQLite
```

After a successful assignment, `CategoriesViewModel` updates the displayed category names and raises `ProductsAssigned`. `ProductsViewModel` receives that event and reloads its product grid.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/POS.Domain/Entities/Product.cs` | Modify | Add validated category assignment behavior. |
| `src/POS.Application/DTOs/Products/AssignProductsToCategoryRequest.cs` | Add | Carry target category and selected product IDs. |
| `src/POS.Application/UseCases/Products/AssignProductsToCategoryUseCase.cs` | Add | Validate, assign, and persist the batch. |
| `src/POS.Infrastructure/DependencyInjection.cs` | Modify | Register the use case. |
| `src/POS.Presentation.Shared/ViewModels/CategoriesViewModel.cs` | Modify | Coordinate product selection and assignment feedback. |
| `src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs` | Modify | Reload products after assignment. |
| `src/POS.Presentation/Views/ProductsView.xaml` | Modify | Add product search, multi-select grid, target picker, and action. |
| `tests/POS.Application.Tests/Products/ProductUseCaseTests.cs` | Modify | Verify update count, one save, and no partial update. |
| `tests/POS.Presentation.Shared.Tests/CategoriesViewModelTests.cs` | Modify | Verify the selection-to-assignment workflow. |

## Testing Strategy

- Application tests verify two products change in one save and a missing product prevents every change.
- Shared ViewModel tests verify loading, selecting, searching, assignment, success feedback, and refresh notification.
- The full Windows solution build verifies WPF XAML compilation; the full test suite verifies all affected layers.
