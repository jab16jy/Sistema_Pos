<Proposal: wt3-categorias-auditoria-login>
## Intent

Address gaps in category CRUD, audit log, and user authentication:
1. Category CRUD: Synchronized category management (IsActive, delete validation, collection sync).
2. Audit log scrollbar: Prevent DataGrid overflow.
3. Login validation: Replace silent credential failures with explicit feedback.

## Scope

### In Scope
- Bind `IsActive` in `ProductsView.xaml`.
- Implement `DeleteCategoryUseCase` and repository deletion.
- Sync `ProductsViewModel` categories dropdown on CRUD changes.
- Constrain `DataGrid` in `AuditLogView.xaml` via `Grid` layout.
- Refactor `LoginViewModel.cs` credentials feedback and bind UI to `Message`.

### Out of Scope
- Deleting categories with assigned products.
- Redesigning login UI or auth flow.
- Modifying audit log filter/pagination queries.

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- product-catalog: Support categories CRUD, IsActive toggling, and dropdown synchronization.
- audit-trail: Constrain audit log DataGrid using Grid row heights.
- user-auth: Display validation errors for credentials/required fields.

## Approach

Use Clean Architecture and WPF layout rules (Approach 1 from exploration):
1. **Category Sync/Delete**: Add `IsActive` property. Implement `DeleteCategoryUseCase` checking `HasProductsAsync`. Raise sync events to update `ProductsViewModel.Categories`.
2. **Audit Log**: Replace card `StackPanel` with `Grid` (`Auto`, `*`, `Auto` rows) to constrain `DataGrid` and enable vertical scrollbar.
3. **Login UX**: Refactor `LoginCommand.CanExecute` to allow execution when inputs are empty, perform credential validation in ViewModel, and bind UI info card to `Message`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| [CategoriesViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/CategoriesViewModel.cs) | Modified | Implement validation and category commands. |
| [ProductsViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs) | Modified | Synchronize collections on Category change. |
| [ProductsView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/ProductsView.xaml) | Modified | Bind active Category checkbox and headers. |
| [ICategoryRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Domain/Interfaces/ICategoryRepository.cs) | Modified | Add `DeleteAsync` signature. |
| [CategoryRepository.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Infrastructure/Persistence/Repositories/CategoryRepository.cs) | Modified | Implement category deletion. |
| [DeleteCategoryUseCase.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Application/UseCases/Categories/DeleteCategoryUseCase.cs) | New | Check product association before deleting. |
| [AuditLogView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/AuditLogView.xaml) | Modified | Set `Grid` container layout. |
| [LoginView.xaml](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/Views/LoginView.xaml) | Modified | Bind `Message` to feedback block. |
| [LoginViewModel.cs](file:///C:/Users/PC%20RYZEN/Sistema_Pos-worktrees/wt3-categorias-auditoria-login/src/POS.Presentation/ViewModels/LoginViewModel.cs) | Modified | Handle inline validation errors. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Category Deletion with products | Low | Check `HasProductsAsync` and return UI error. |
| AuditLog pagination rendering | Low | Constrain height to parent grid row. |

## Rollback Plan

Revert all changes using `git checkout -- .` or `git reset --hard origin/branch`.

## Dependencies

None.

## Success Criteria

- [ ] Categories can be created, updated, listed, and deleted safely.
- [ ] Category updates sync immediately with the product edit dropdown.
- [ ] Audit log shows vertical scrollbar when row count overflows screen.
- [ ] Login screen displays validation error messages instead of failing silently.
</Proposal: wt3-categorias-auditoria-login>
