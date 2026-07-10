# Implementation Tasks: Category Deletion, Audit Log Filtering & Login Flow

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

## Phase 1: Foundation
- [x] 1.1 Add `DeleteAsync` to [ICategoryRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Domain/Interfaces/ICategoryRepository.cs).
- [x] 1.2 Implement `DeleteAsync` in [CategoryRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Infrastructure/Persistence/Repositories/CategoryRepository.cs) using EF's `Remove`.
- [x] 1.3 Create [DeleteCategoryUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Application/UseCases/Categories/DeleteCategoryUseCase.cs) to check category existence, verify `HasProductsAsync` is false, and invoke delete.
- [x] 1.4 Register `DeleteCategoryUseCase` as a scoped service in [DependencyInjection.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Infrastructure/DependencyInjection.cs).

## Phase 2: Categories UI & Synchronization
- [x] 2.1 Add `DeleteCommand` and `DeleteAsync()` to [CategoriesViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/CategoriesViewModel.cs). Raise button status change in `SelectedCategory` setter.
- [x] 2.2 Declare a `CategoriesChanged` event in [CategoriesViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/CategoriesViewModel.cs) and invoke it after successful category save or delete.
- [x] 2.3 Subscribe to `CategoriesChanged` in [ProductsViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs) to refresh categories list by calling `LoadCategoriesAsync()`.
- [x] 2.4 Inject `DeleteCategoryUseCase` in [AppBootstrapper.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/AppBootstrapper.cs) and pass it to `CategoriesViewModel`.
- [x] 2.5 Add an "Eliminar" button to the category form in [ProductsView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/ProductsView.xaml) visibility-bound to selected category presence using `NullToVisibilityConverter`.

## Phase 3: Audit Log Scrollbar
- [x] 3.1 Replace the outer `StackPanel` inside the main `Border` with a `Grid` containing three rows (`Auto`, `*`, `Auto`) in [AuditLogView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/AuditLogView.xaml). Set correct row attachments for the filter wrap panel, `DataGrid`, and pagination.
- [x] 3.2 Add `"Categoría"` to the `AvailableEntityFilters` list in [AuditLogViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/AuditLogViewModel.cs).

## Phase 4: Login Error Messages
- [x] 4.1 Modify `LoginCommand`'s `canExecute` check in [LoginViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/ViewModels/LoginViewModel.cs) to check only `!IsLoading`.
- [x] 4.2 Add input validation logic in `ExecuteLoginAsync` of [LoginViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/ViewModels/LoginViewModel.cs) to assign a specific validation message to `Message` when `Username` or `Password` is empty.
- [x] 4.3 Update [LoginView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/LoginView.xaml) to bind the info card's `TextBlock` or a dedicated warning label to `Message`.

## Phase 5: Verification
- [x] 5.1 Add tests to [CategoryUseCaseTests.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/tests/POS.Application.Tests/Categories/CategoryUseCaseTests.cs) verifying `DeleteCategoryUseCase` throws if category is missing or has active products.
- [x] 5.2 Add tests to [CategoriesViewModelTests.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/tests/POS.Presentation.Shared.Tests/CategoriesViewModelTests.cs) verifying delete command behavior and category list reloading.
- [x] 5.3 Create [LoginViewModelTests.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/tests/POS.Presentation.SmokeTests/LoginViewModelTests.cs) to verify username/password validation error messages.
- [x] 5.4 Run `dotnet build` and `dotnet test` to verify everything compiles cleanly and all test suites pass.
