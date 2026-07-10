# Exploration: wt3-categorias-auditoria-login

## Current State

1. **Part A (Categories ViewModel)**:
   - `CategoriesViewModel` is integrated as a sub-view editor in the Products module. It contains basic listing (loaded into a `Categories` collection via `SearchCategoriesUseCase`), and creation/editing of categories through a `SaveCommand` that delegates to `CreateCategoryUseCase` or `UpdateCategoryUseCase` depending on whether `SelectedCategory` is null.
   - **Gaps**:
     - The "Activa" CheckBox in `ProductsView.xaml` is statically set to `IsChecked="True"` and has no binding. The `CategoriesViewModel` lacks an `IsActive` property to track or toggle a category's active state.
     - The conceptual commands `CreateCategoryCommand`, `UpdateCategoryCommand`, and `DeleteCategoryCommand` required by `product-catalog/spec.md` are not explicitly defined under those names (instead, there's `SaveCommand` for both creation and editing, and no delete command exists).
     - There is no `DeleteCategoryUseCase` in the Application layer, and `ICategoryRepository` does not expose a delete method.
     - When a category is added/edited in `CategoriesViewModel`, the category list in `ProductsViewModel` (`ProductsViewModel.Categories` used by the product edit form's ComboBox) does not synchronize, causing newly created categories to remain hidden in the dropdown until products are reloaded.

2. **Part B (Audit Log Scrollbar)**:
   - `AuditLogView.xaml` displays logs in a `DataGrid` but is wrapped inside a `StackPanel`.
   - **Gaps**: In WPF, a `StackPanel` measures its children with infinite vertical space. This prevents the `DataGrid` from determining height constraints, which stops its internal `ScrollViewer` from rendering a vertical scrollbar. Consequently, the DataGrid renders all 100 rows per page, pushing the pagination controls off the visible screen.

3. **Part C (Login Error Messages)**:
   - `LoginViewModel.cs` calls `LoginUseCase` and correctly captures credentials errors (such as `"Contraseña incorrecta."` or `"Usuario no encontrado o inactivo."`) inside a `Message` property.
   - **Gaps**:
     - `LoginView.xaml` does not bind the `Message` property. It displays a static information string: `"Ingresa con el usuario asignado. Si olvidaste tu clave, llama al administrador."` inside a blue info box.
     - The login button is disabled via `CanExecute` if `Username` or `Password` is empty, which prevents the user from receiving immediate, explicit validation feedback (e.g. clicking the button to see "El usuario es requerido").

## Affected Areas

- `src/POS.Presentation.Shared/ViewModels/CategoriesViewModel.cs` — Needs to expose `IsActive` property, implement validation, and add aliases or support for `CreateCategoryCommand`, `UpdateCategoryCommand`, and `DeleteCategoryCommand`.
- `src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs` — Needs to subscribe to category updates or reload categories whenever `CategoriesEditor` saves a category.
- `src/POS.Presentation/Views/ProductsView.xaml` — Needs to bind the "Activa" CheckBox, dynamically display "Nueva categoría" vs "Editando categoría", and wire any new commands.
- `src/POS.Domain/Interfaces/ICategoryRepository.cs` — Needs a `DeleteAsync` (or `RemoveAsync`) signature to support deletion.
- `src/POS.Infrastructure/Persistence/Repositories/CategoryRepository.cs` — Needs implementation of the deletion method.
- `src/POS.Application/UseCases/Categories/DeleteCategoryUseCase.cs` — (New file) to check if products are associated via `HasProductsAsync` and delete a category.
- `src/POS.Presentation/Views/AuditLogView.xaml` — Needs to replace `StackPanel` with a grid layout containing row definitions (`Auto`, `*`, `Auto`) to constrain the `DataGrid` height and enable its scrollbar.
- `src/POS.Presentation/Views/LoginView.xaml` — Needs to bind the info card text block to `Message` instead of using static text.
- `src/POS.Presentation/ViewModels/LoginViewModel.cs` — Refactor `LoginCommand`'s `CanExecute` and validation logic to allow showing validation error messages inline upon interaction.

## Approaches

### 1. Spec-Driven CRUD Completion & WPF Standard Layout (Recommended)
- **Categories**: Implement full category CRUD, including the delete method in `ICategoryRepository`, `DeleteCategoryUseCase` with the product guard, and wire a `DeleteCategoryCommand` to the VM and a new "Eliminar" button in `ProductsView.xaml`. Implement an event-driven sync (`CategoryChanged`) between `CategoriesViewModel` and `ProductsViewModel` to update the dropdown. Bind `IsActive` in the category form.
- **Audit Log**: Replace the `StackPanel` inside the card with a `Grid` containing 3 rows (`Auto` for filters, `*` for DataGrid, `Auto` for pagination). Specify `ScrollViewer.VerticalScrollBarVisibility="Auto"` on the `DataGrid`.
- **Login**: Bind `{Binding Message}` to the text block. Allow execution of `LoginCommand` even with empty fields (except when loading) and validate inputs inside the execution method to update `Message` dynamically.
  - **Pros**: Delivers complete CRUD behavior as specified, correctly resolves WPF layout issues, and improves login UX considerably.
  - **Cons**: High code footprints (more files edited/added).
  - **Effort**: Medium

### 2. Minimalist Hardening without Schema/Use Case modification
- **Categories**: Only bind `IsActive` in the form. Avoid implementing category deletion (since there is no use case for it currently). Synchronize categories by calling `LoadCategoriesAsync()` whenever the subtab is clicked/focused.
- **Audit Log**: Keep the `StackPanel` but assign a hardcoded `MaxHeight` (e.g. `350`) directly to the `DataGrid` to force a scrollbar.
- **Login**: Bind `{Binding Message}` to the info block, but keep the command's `CanExecute` as-is, leaving the button disabled without any inline helper text for empty fields.
  - **Pros**: Low complexity, zero changes to repositories or use cases.
  - **Cons**: Brittle UI layout (hardcoded heights), does not solve the disabled-button lack of feedback, and leaves category delete unimplemented.
  - **Effort**: Low

## Recommendation

We recommend **Approach 1 (Spec-Driven CRUD Completion & WPF Standard Layout)**. Constraining the DataGrid using a `Grid` layout with a `*` row is the standard and correct way to handle scrollable collections in WPF. Adding the delete use case and category sync completes the missing features in a clean, maintainable way that aligns with the project's architecture. Adjusting the login validation logic provides active feedback to users.

## Risks

- **Category Deletion**: Must ensure `DeleteCategoryUseCase` checks `HasProductsAsync` and returns a clear, user-friendly error message if products are associated.
- **Layout Adjustments**: Must thoroughly test `ProductsView.xaml` and `AuditLogView.xaml` to verify that the refactored WPF layouts render correctly across window resizes.

## Ready for Proposal

**Yes**. The scope is fully explored and the implementation strategy is clear. The orchestrator should propose Approach 1 to implement complete, synchronized category CRUD, standard WPF grid-scrolling, and interactive login validation.
