# Exploration: custom-role-permissions

## Current State

**Role model**: `src/POS.Domain/Enums/UserRole.cs` — fixed enum `Administrator=1, Cashier=2`. `src/POS.Domain/Entities/User.cs` has `public UserRole Role { get; private set; }` set in constructor, mutable via `UpdateRole(UserRole role)`. No `Role`/`Permission` entities exist yet; `IUserRepository` (`src/POS.Domain/Interfaces/IUserRepository.cs`) has no role-lookup methods.

**Auth/session flow**: `LoginUseCase` (`src/POS.Application/UseCases/Auth/LoginUseCase.cs`) validates credentials, writes an `AuditLog` ("Login"/"LoginFailed"), returns `LoginResult(Success, Error, UserId, Username, Role)`. Session is `ICurrentOperatorSession` (`src/POS.Application/Interfaces/ICurrentOperatorSession.cs`: OperatorId, Username, Role, IsAuthenticated) backed by `InMemoryUserSession` (`src/POS.Infrastructure/Persistence/InMemoryUserSession.cs`, implements `IWritableOperatorSession`, defaults Role to Cashier). `AppBootstrapper.cs` (`src/POS.Presentation/AppBootstrapper.cs` lines ~42, ~207) reads `session.Role` and passes it into `MainShellViewModel`.

**Central admin-gate helper**: `AdminUserAuthorization.EnsureAdministratorAsync` (`src/POS.Application/UseCases/Auth/AdminUserAuthorization.cs`) — looks up requester by id, checks `IsActive && Role == UserRole.Administrator`, returns an error string or null. Used by `CreateUserUseCase`, `UpdateUserUseCase`, `DeactivateUserUseCase`, `GetAuditLogsUseCase`. `CancelSaleUseCase` (`src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs`) duplicates the same check inline (`if (user.Role != UserRole.Administrator) throw new UnauthorizedActionException(...)`) instead of reusing the helper — an inconsistency that should be unified when the new permission-check helper is introduced. This is the reusable pattern the new gate should replace/extend, e.g. `PermissionAuthorization.EnsureHasPermissionAsync(users, requestingUserId, "Sales.Cancel", ct)`.

**CRITICAL GAP FOUND**: Backup/Restore/Settings use cases have ZERO authorization checks today.
- `TriggerManualBackupUseCase`, `RestoreBackupUseCase`, `SaveSettingsUseCase`, `LoadSettingsUseCase`, `UpdateBackupDirectoryUseCase` (all in `src/POS.Application/UseCases/Settings/`) — no role/permission gate at all. Only WPF-side gating (if any) currently protects them. This is the most important gap the new RBAC design must close (backup creation/restoration + settings are named priority surfaces).

**WPF navigation-guard pattern (to be replaced with permission checks)**:
- `MainShellViewModel` (`src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs`) computes a single `bool isAdmin = role == UserRole.Administrator` (line ~75) and uses it to build `FuncbarModules`/`AdminHubModules`/`AdminHubExtraModules` — an all-or-nothing gate over 9+ admin modules (products, inventory, suppliers, reports, shifts, loyalty, backup, users, audit), not per-module/per-action. Needs to become permission-key driven (one permission per module-visibility key) instead of one boolean.
- `InvoicesViewModel.IsAdministrator` (`src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs` line 294) gates `CancelSaleAsync` (line 523) — sale/invoice cancellation UI check, duplicated by the Application-layer check in `CancelSaleUseCase`.
- `UserManagementViewModel.IsAdmin` (`src/POS.Presentation.Shared/ViewModels/UserManagementViewModel.cs` line 114) gates Create/Edit/Deactivate commands' `CanExecute`. `AvailableRoles` is currently a hardcoded `[Cashier, Administrator]` list (line 49) — will become a query against persisted roles.

**Seeding**: `DatabaseSchemaRollout.SeedDefaultAdminAsync` (`src/POS.Infrastructure/DatabaseSchemaRollout.cs` line 27) seeds a single `admin`/`admin123` user with `UserRole.Administrator` only if `Users` table is empty. No Cashier system user is seeded today (Cashier is just the enum default). New design must seed both a `Role` "Administrator" (system, immutable, all permissions) and `Role` "Cashier" (system, seeded with current default permissions) rows, and point the seeded admin User at the Administrator role id.

**EF Core / migrations conventions**: Migrations live in `src/POS.Infrastructure/Migrations/`, named `{yyyyMMddHHmmss}_{PascalCaseDescription}.cs` + matching `.Designer.cs` (e.g. `20260712050617_RemoveSupplierQuotations.cs`). `UserConfiguration` (`src/POS.Infrastructure/Persistence/Configurations/UserConfiguration.cs`) uses `HasConversion<string>()` on `User.Id` (Guid stored as lowercase TEXT) — any new `Role`/`RolePermission` entity FK/PK to User must follow the same conversion or lookups silently fail (this exact bug bit `AdminUserAuthorization` before, per an existing code comment: "Without this conversion, EF binds Guid parameters as uppercase TEXT, so lookups by Id never match — that broke AdminUserAuthorization's re-check"). `AppDbContext.OnModelCreating` uses `modelBuilder.ApplyConfigurationsFromAssembly` — new configs auto-discovered, no manual registration needed elsewhere. **Automatic audit trail**: `AppDbContext.SaveChangesAsync` override (`AddAuditEntries`, lines ~117+) audits every Added/Modified/Deleted entity automatically except `AuditLog` itself — so `Role`/`RolePermission`/`User.RoleId` changes get audited for free, no extra work needed for audit coverage of role changes.

**DI registration**: All use cases registered as `services.AddScoped<T>()` in `src/POS.Infrastructure/DependencyInjection.cs` (Auth use cases at lines 155-158, Settings/backup at 121-125). New Roles/Permissions use cases follow the same flat registration list.

## Full Module/Action Inventory (survey level, from 66 discovered use cases under src/POS.Application/UseCases/)

- **Auth/Users**: Login, Logout, CreateUser, UpdateUser (role reassignment), DeactivateUser, GetAllUsers → permissions: `Users.View`, `Users.Create`, `Users.Edit`, `Users.Deactivate`
- **Roles & Permissions** (NEW module, no use cases yet): `Roles.View`, `Roles.Create`, `Roles.Edit`, `Roles.Deactivate`, `Roles.AssignPermissions`
- **Audit**: GetAuditLogs → `Audit.View`
- **Settings**: LoadSettings, SaveSettings, UpdateBackupDirectory → `Settings.View`, `Settings.Edit`
- **Backup**: TriggerManualBackup, RestoreBackup → `Admin.Backup.View`, `Admin.Backup.Create`, `Admin.Backup.Restore` (restore is the most destructive action in the app — needs its own key, not folded into Create)
- **Products**: CreateProduct, UpdateProduct, DeleteProduct, SearchProducts, ImportProductsCsv, AssignInternalBarcode, FindProductByBarcode, AssignProductsToCategory → `Products.View`, `Products.Create`, `Products.Edit`, `Products.Delete`, `Products.Import`
- **Categories**: CreateCategory, UpdateCategory, DeleteCategory, SearchCategories → `Categories.View`, `Categories.Create`, `Categories.Edit`, `Categories.Delete`
- **Inventory**: AdjustStock, QueryStock, QueryLowStock, QueryMovementHistory → `Inventory.View`, `Inventory.Adjust`
- **Sales/Cashier**: CreateSale, PreviewPayment, GetDashboardSummary → `Cashier.Sell` (baseline cashier action, both roles have it)
- **Invoices**: QuerySalesHistory, GetInvoiceDetail, ReprintReceipt, ProcessReturn, CancelSale → `Invoices.View`, `Invoices.Reprint`, `Invoices.Return`, `Invoices.Cancel` (Cancel is currently Administrator-only via CancelSaleUseCase's inline check)
- **Quotes**: CreateQuote, ConvertQuoteToSale, GetCustomerPayableQuotes, GetQuoteHistory, GetQuoteDetail → `Quotes.View`, `Quotes.Create`, `Quotes.Convert`
- **Purchases/Suppliers**: RegisterPurchase, SaveSupplier, SearchSuppliers, DeleteSupplier, supplier catalog use cases (Get/Save/Delete), SearchPurchaseHistory → `Suppliers.View`, `Suppliers.Create`, `Suppliers.Edit`, `Suppliers.Delete`, `Purchases.View`, `Purchases.Create`
- **Customers**: SaveCustomer, SearchCustomers, GetCustomerHistory → `Customers.View`, `Customers.Create`, `Customers.Edit`
- **Receivables**: ApplyReceivablePayment → `Receivables.View`, `Receivables.ApplyPayment`
- **Reporting/Cash**: GetOperationalReport, CloseCashRegister → `Reports.View`, `CashRegister.Close`
- **Shifts (Turnos)**: StartShift, CloseShift, GetShifts → `Shifts.View`, `Shifts.Open`, `Shifts.Close` (own-vs-all-cashiers scope is a design question, not resolved here)
- **Loyalty**: AccumulateLoyaltyPoints, RedeemLoyaltyPoints, GetLoyaltyBalance, GetLoyaltyProgramOverview, SetLoyaltyRule, loyalty tier CRUD → `Loyalty.View`, `Loyalty.Redeem`, `Loyalty.ConfigureRules`, `Loyalty.ManageTiers`
- **Barcode**: PrintLabels, GenerateBarcode → `Barcode.View`, `Barcode.Print`
- **Communication**: SendDocument, ResendDocument → `Documents.Send`

## Constraints / Risks

1. Every current `UserRole` reference (64 files matched grep across the repo) needs updating — largest blast radius is `User.Role` type change (enum → `RoleId` FK/navigation), `ICurrentOperatorSession.Role`, `InMemoryUserSession`, and all ViewModel `IsAdmin`/`IsAdministrator` booleans becoming permission-set lookups.
2. Backup/Restore/Settings use cases currently have NO authorization — must gain checks in this change, not just navigation hiding (matches priority surfaces requirement).
3. `CancelSaleUseCase` bypasses the shared `AdminUserAuthorization` helper with an inline duplicate check — should be unified with the new permission-check helper for consistency, otherwise the new model ships with two divergent enforcement paths from day one.
4. `MainShellViewModel`'s all-or-nothing `isAdmin` boolean must become a per-module-visibility-permission-driven module list; this is a real behavior change to a well-tested ViewModel (check `tests/POS.Presentation.Shared.Tests`).
5. EF Guid-as-lowercase-TEXT conversion must be replicated for any new Role/RolePermission entity configs, or FK lookups silently fail (repeat of a prior real bug documented in `UserConfiguration.cs`).
6. Since dev data can be dropped, a single clean-slate EF Core migration adding `Roles`, `RolePermissions`, `Users.RoleId` (dropping `Users.Role` enum column) is acceptable — no migration/backward-compat path needed.
7. `UserManagementViewModel.AvailableRoles` hardcoded list needs to become a live query to a new `GetAllRolesUseCase` (or similar).

## Recommended Shape (input to propose, not a commitment)

- Domain: `Role` entity (Id, Name, IsSystem, IsActive, CreatedAt), `Permission` as a static code-owned catalog (const string keys, not a DB table) grouped by module, `RolePermission` join entity (RoleId, PermissionKey) as the persisted grant list.
- `User.RoleId` (Guid FK) replaces `User.Role` (enum). `UserRole` enum can be deleted after migration.
- `ICurrentOperatorSession` gains permission-lookup capability (either eager-loaded `IReadOnlySet<string> Permissions` populated at login, matching the "effective on next login" decision, or a `RoleId` + separate permission-check service call at Application layer).
- Reusable Application-layer gate: `PermissionAuthorization.EnsureHasPermissionAsync(users, requestingUserId, "Permission.Key", ct)` replacing/extending `AdminUserAuthorization`, with a hardcoded Administrator bypass (system role permissions can never be stripped, always passes).
- Self-lockout prevention: a check in DeactivateUser/UpdateUser (and future role deactivation) ensuring at least one active User with the Administrator role remains.
- New "Roles & Permissions" screen mirrors `UserManagementViewModel`'s pattern (CRUD ViewModel + Application use cases: GetAllRoles, CreateRole, UpdateRole (rename/permission assignment), DeactivateRole — no DeleteRole, matching "cannot delete, only deactivate" + "roles assigned to users cannot be deleted" decisions already locked).

## Open Questions (few — most decisions already locked by user)

- Shifts: should "close own shift" vs "close any cashier's shift" be two separate permission keys, or is shift scope out of v1 RBAC scope (defer to design phase)?
- Should the permission catalog be centralized in Domain or Application layer as a static class (`PermissionKeys`)? Domain has no outer dependency, so a Domain-layer catalog is cleaner if Domain-layer authorization checks are ever needed; otherwise Application layer matches where `AdminUserAuthorization` lives today.
