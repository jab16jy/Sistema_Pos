# Proposal: custom-role-permissions

**Change**: custom-role-permissions
**Phase**: propose
**Store**: openspec
**Date**: 2026-07-12

---

## 1. Intent

### Problem

Authorization today is a single hardcoded `UserRole` enum (`Administrator | Cashier`) with an all-or-nothing admin gate. Three problems make this worth fixing now:

1. **Security gap**: Backup, Restore, and Settings use cases have **zero** Application-layer authorization. Only WPF navigation hides them — anyone reaching the use case (test, future caller, refactor) executes them ungated. Restore is the single most destructive action in the app (it overwrites the live database) and is currently the least protected.
2. **Divergent enforcement**: authorization is enforced two ways — the shared `AdminUserAuthorization.EnsureAdministratorAsync` helper, and an inline duplicate role check in `CancelSaleUseCase`. Two paths means bugs land in one and not the other.
3. **No customization**: admins cannot grant a subset of capabilities. Every privileged action is "full admin or nothing," so any staff member who needs one admin capability (e.g. view reports) must be made a full administrator with backup/restore/user-management rights.

### Why now

We are in **pre-production development**. There is no production data or user base to migrate, so the enum-to-RBAC replacement can be a single clean-slate migration with no backward-compat cost. That window closes at first production deployment — after that, replacing the `Users.Role` column becomes a data-migration project. Doing it now is cheap; doing it later is not.

### Success looks like

- Every privileged use case enforces a specific permission key at the Application layer, not just in the UI.
- Backup create, backup restore, and settings are gated (the named priority surfaces closed).
- `CancelSaleUseCase` and every other gate route through **one** authorization helper.
- An admin can create a custom role (e.g. "Report Viewer"), assign it a permission subset, and assign a user to it — effective on that user's next login.
- The system `Administrator` role can never be stripped of permissions, and the system can never reach a state with zero active administrators.

---

## 2. Scope

### In scope (v1)

**Data & domain**
- New persistent entities: `Role`, `RolePermission`, and `User.RoleId` FK replacing the `User.Role` enum.
- Code-owned permission catalog (`PermissionKeys` static class) with stable string keys.
- Single clean-slate EF Core migration: create `Roles` + `RolePermissions`, drop `Users.Role`, add `Users.RoleId`. Delete the `UserRole` enum after migration.
- Seed two system roles: `Administrator` (immutable, all permissions) and `Cashier` (its current default permission set). Point the seeded `admin` user at the Administrator role.

**Enforcement**
- New `PermissionAuthorization.EnsureHasPermissionAsync(...)` replacing `AdminUserAuthorization`, with a hardcoded Administrator-role bypass (system admin always passes).
- Unify `CancelSaleUseCase`'s inline check onto the new helper (`Invoices.Cancel`).
- **Application-layer permission gates added to the priority surfaces**: user management (view/create/edit/deactivate), roles & permissions (new module), backup create, backup restore, settings (view/edit), and audit view. These are the surfaces that get real gating in THIS change.

**Session / login**
- `LoginUseCase` loads the user's role permission set at login and populates the session with it (eager `IReadOnlySet<string>` — matches "effective on next login").
- `ICurrentOperatorSession` / `InMemoryUserSession` carry the permission set and the role id instead of a `UserRole` enum.

**Guardrails**
- Self-lockout prevention: cannot deactivate the last active administrator; cannot deactivate the Administrator role; cannot remove permissions from the Administrator role.
- Roles assigned to any user cannot be deleted — only deactivated. No `DeleteRole` use case at all (only Deactivate).

**WPF**
- New admin-only "Roles & Permissions" screen (CRUD + permission assignment), mirroring `UserManagementViewModel`.
- `MainShellViewModel`'s single `isAdmin` boolean becomes per-module-visibility permission checks (one `.View` key per admin module).
- `InvoicesViewModel.IsAdministrator` and `UserManagementViewModel.IsAdmin` become permission-set lookups. `UserManagementViewModel.AvailableRoles` becomes a live query.

### Out of scope (deferred, explicitly)

| Deferred item | Why cut here |
|---|---|
| **Multi-role per user** | User locked to exactly one role in v1. No design pressure to add it; would complicate session load and every gate. |
| **Dynamic / user-defined permission keys** | Catalog is code-owned. User-defined keys have no code to enforce them — they'd be inert data. |
| **Field-level / row-level granularity** | Permission is per action key, not per field or per record. |
| **Shift scope ("close own" vs "close any cashier's" shift)** | The exploration flags this as unresolved. It is an *ownership scoping* problem (needs a "does this shift belong to me" check), a different mechanism from key-based RBAC. In v1, `Shifts.Open`/`Shifts.Close`/`Shifts.View` are ordinary keys with no owner scoping; owner-vs-all scoping is deferred to a follow-up change. |
| **Real Application-layer gating for non-priority modules** | See cut line below. |

### The gating cut line (priority vs. deferred enforcement)

The exploration inventoried ~66 use cases across ~20 modules. Gating **all** of them in one change is a 400+ line blast radius across dozens of use cases and their tests — a chained-PR-sized effort. The user's priority list is: users, roles, backup create, backup restore, settings.

**Decision — this change gates exactly these modules at the Application layer:**

- `Users.*` (view, create, edit, deactivate)
- `Roles.*` (view, create, edit, deactivate, assign-permissions) — new module
- `Admin.Backup.Create`, `Admin.Backup.Restore`, `Admin.Backup.View`
- `Settings.View`, `Settings.Edit`
- `Audit.View`
- `Invoices.Cancel` — **included even though not on the priority list**, because unifying the existing `CancelSaleUseCase` inline check is a stated in-scope goal and leaving it un-unified would ship the new model with a divergent path from day one.

**Deferred to a follow-up change** (`custom-role-permissions-rollout` or similar): real Application-layer gates on Products, Categories, Inventory, Quotes, Suppliers, Purchases, Customers, Receivables, Reports, CashRegister, Shifts, Loyalty, Barcode, Documents, and the non-Cancel Invoices actions.

**What the deferred modules still get in v1**: their permission keys exist in the catalog, their `.View` keys drive `MainShellViewModel` module visibility, and their roles can be granted/denied those keys. So the *catalog and the UI visibility* are complete in v1; only the *Application-layer enforcement inside those use cases* is deferred.

**Justification for the cut**: the priority surfaces are the ones with either a real security gap (backup/restore/settings have zero gating) or a real inconsistency (users via `AdminUserAuthorization`, cancel via inline check). The deferred modules are today gated by the same all-or-nothing admin boolean as everything else — deferring their per-key enforcement does not *regress* their current protection; it stays exactly as protective as today until the follow-up hardens it. The priority surfaces, by contrast, are either unprotected or inconsistent today and must not stay that way. This keeps v1 shippable and reviewable while closing the actual gaps.

---

## 3. Entity shape

Confirmed, refined:

**`Role`** (Domain entity)
- `Id` (Guid, PK, stored as lowercase TEXT via `HasConversion<string>()` — same convention as `User.Id`)
- `Name` (string, unique)
- `IsSystem` (bool) — true for Administrator and Cashier; blocks delete/deactivate/rename of system roles where invariants require
- `IsActive` (bool)
- `CreatedAt`, `UpdatedAt` (DateTime)

**`RolePermission`** (Domain entity — the persisted grant list)
- `RoleId` (Guid FK → Role, same lowercase-TEXT conversion)
- `PermissionKey` (string — must be a key that exists in the code catalog; not validated by a DB FK since the catalog is not a table)
- Composite key `(RoleId, PermissionKey)`.

**`User`**
- `RoleId` (Guid FK → Role) replaces `Role` (UserRole enum). Navigation optional at design's discretion.

**Permission catalog location — decision: Domain layer.**
`PermissionKeys` lives as a static class in `POS.Domain` (e.g. `POS.Domain/Authorization/PermissionKeys.cs`), grouped by module, with const string keys and a means to enumerate the full catalog (for the assignment UI and for seeding). Rationale: Domain has no outward dependency, so a Domain-owned catalog can be referenced from Application (gates), Infrastructure (seeding), and Presentation (assignment screen) without any layer inversion. The `Role`/`RolePermission` entities are already Domain concepts (the domain skill lists "Users and roles" as Domain); the keys they reference belong beside them. The catalog is **not** a DB table — it is code. `RolePermission` rows reference catalog keys by string.

---

## 4. Enforcement mechanism

Replace `AdminUserAuthorization` with `PermissionAuthorization` (Application layer, where the old helper lives):

```
PermissionAuthorization.EnsureHasPermissionAsync(
    users, requestingUserId, PermissionKeys.Admin.Backup.Restore, ct)
    -> returns error string or null (same shape as EnsureAdministratorAsync today)
```

Behavior:
1. Look up requester by id (respecting the lowercase-TEXT Guid conversion — the bug that previously broke `AdminUserAuthorization`).
2. If requester inactive → deny.
3. If requester's role is the system **Administrator** role → **always allow** (bypass — system admin permissions can never be stripped, so no key lookup needed).
4. Otherwise, check whether the requester's role grants the required `PermissionKey` (via the role's `RolePermission` set).

**Two resolution styles, decision:** gates consult the **session's eager permission set** when the requester is the current operator, and fall back to a repository role-permission lookup when a use case must authorize a *different* user id than the session. Most gates authorize the current operator, so the common path is a fast in-memory set check with no DB round-trip; the helper still accepts an explicit user id for the uncommon case. Design phase details the exact signature.

**`CancelSaleUseCase` unification**: delete its inline `if (user.Role != UserRole.Administrator) throw ...` and replace with a call to `PermissionAuthorization.EnsureHasPermissionAsync(..., PermissionKeys.Invoices.Cancel, ct)`. This removes the second enforcement path. The seeded Administrator role keeps `Invoices.Cancel`; Cashier does not, preserving current behavior.

---

## 5. Session / login integration

- **`LoginUseCase`**: after validating credentials, load the user's role and its permission set, and populate the session with `RoleId`, `RoleName`, and an `IReadOnlySet<string> Permissions`. Administrator role gets the full catalog (or a sentinel that the gate treats as "all"). The existing `AuditLog` write is unchanged.
- **`ICurrentOperatorSession`**: replace `UserRole Role` with `Guid RoleId`, `string RoleName`, and `IReadOnlySet<string> Permissions` (plus a convenience `HasPermission(key)`). `IsAuthenticated` unchanged.
- **`InMemoryUserSession` / `IWritableOperatorSession`**: default to an empty permission set (not "Cashier") when unauthenticated; set the full set on login write.
- **`LoginResult`**: `Role` field becomes role name / role id as needed by the shell; the design phase decides the exact DTO shape.
- **"Effective on next login" is satisfied for free**: because permissions are snapshotted into the session at login, editing a role mid-session does not retroactively change the logged-in operator's session set. No hot-reload code needed — the snapshot *is* the mechanism. A `ponytail` note: this is deliberate, not a missing feature.

---

## 6. Self-lockout & immutability guardrails

All checks live at the **use-case layer** (Application), not the DB, so they carry meaningful error messages and are unit-testable:

| Trigger | Guard | Where |
|---|---|---|
| Deactivate a user | Deny if that user is the last active user holding the Administrator role | `DeactivateUserUseCase` |
| Reassign a user's role away from Administrator | Deny if it would leave zero active administrators | `UpdateUserUseCase` |
| Deactivate a role | Deny if the role is a system role, or if any user is assigned to it (roles assigned to users cannot be deleted/deactivated) | `DeactivateRoleUseCase` |
| Edit Administrator role permissions | Deny any attempt to remove permissions from the system Administrator role | `UpdateRoleUseCase` |
| Delete a role | No `DeleteRole` use case exists — deletion is not offered | n/a |

The "at least one active administrator" invariant is enforced by counting active users whose role is the (immutable) system Administrator role before allowing a deactivation or role reassignment.

---

## 7. WPF impact (proposal-level; design details it)

- **`MainShellViewModel`**: replace the single `bool isAdmin` with per-module checks against `session.Permissions`. Each admin module's visibility is driven by its `.View` key (e.g. `Users.View`, `Admin.Backup.View`, `Audit.View`, plus the new `Roles.View`). This turns the all-or-nothing gate into a per-module gate. The exploration flags this ViewModel as well-tested — its tests will change and must be updated.
- **`InvoicesViewModel.IsAdministrator`** → `session.HasPermission(PermissionKeys.Invoices.Cancel)` gating the cancel command's CanExecute.
- **`UserManagementViewModel.IsAdmin`** → permission-set checks per command (`Users.Create`, `Users.Edit`, `Users.Deactivate`). `AvailableRoles` hardcoded `[Cashier, Administrator]` list → live query via a new `GetAllRolesUseCase`.
- **New `RolesManagementViewModel` + view**: CRUD over roles and a permission-assignment surface (list catalog keys grouped by module, toggle grants). Mirrors the `UserManagementViewModel` pattern. Gated by `Roles.*` keys.

---

## 8. Migration

- **Single clean-slate migration** (`{yyyyMMddHHmmss}_ReplaceUserRoleEnumWithRbac.cs` + `.Designer.cs`): create `Roles` and `RolePermissions` tables, drop `Users.Role`, add `Users.RoleId` (FK). No data-preservation step — dev data is dropped and recreated. Confirmed acceptable per locked decisions.
- **Guid conversion requirement**: `RoleConfiguration` and `RolePermissionConfiguration` MUST apply `HasConversion<string>()` on all Guid PK/FK columns (`Role.Id`, `RolePermission.RoleId`, `User.RoleId`), matching `UserConfiguration`. Omitting this reproduces the exact prior bug where EF binds Guids as uppercase TEXT and lookups silently never match. Configs are auto-discovered via `ApplyConfigurationsFromAssembly` — no manual registration.
- **Seeding**: extend `DatabaseSchemaRollout` (currently `SeedDefaultAdminAsync`) to seed the two system roles and their `RolePermission` rows before/alongside the seeded admin user, and point the admin user at the Administrator role id. Cashier role seeded with its current default set (baseline cashier keys: `Cashier.Sell`, `Invoices.View`/`Reprint`/`Return`, `Quotes.*`, `Customers.*`, etc. — design fixes the exact list).
- **Audit for free**: `AppDbContext.SaveChangesAsync` auto-audits all entity changes, so `Role`/`RolePermission`/`User.RoleId` changes are audited with no extra code.

---

## 9. Risks & open questions (calls made)

| # | Risk / question | Resolution |
|---|---|---|
| 1 | **Blast radius of the `UserRole` type change** — exploration found `UserRole` referenced in ~64 files. | Accept it. The enum deletion is the point of the change. Most references are the WPF booleans and session plumbing already itemized. Design phase produces the exact file list; tasks phase decides slicing. Flag: this is the single biggest source of changed lines. |
| 2 | **Administrator "all permissions" representation** — enumerate the full catalog into the admin role's `RolePermission` rows, or treat the system admin as a code-level bypass with no rows? | **Decision: code-level bypass** (gate short-circuits for the system Administrator role, step 3 in §4). Rationale: it makes "admin can never be stripped" true by construction (there are no rows to delete), and new catalog keys are automatically covered without re-seeding. Seed the admin role with no explicit `RolePermission` rows; the bypass covers it. |
| 3 | **Session snapshot staleness** — a user whose role permissions change mid-session keeps the old set until next login. | **Intended** per the locked "effective on next login" decision. Not a bug. Documented in §5. |
| 4 | **Deferred-module enforcement gap** — deferred modules still ride the old all-or-nothing visibility, so a custom role granted a deferred module's `.View` key sees the module but the use cases inside it are not per-key gated. | Acceptable for v1: deferred use cases keep whatever protection they have today (none regressed). The follow-up change hardens them. Called out as a known, bounded limitation, not a silent gap. Recommend the follow-up change be scheduled before production. |
| 5 | **`GetAuditLogsUseCase` currently uses `AdminUserAuthorization`** — must switch to `Audit.View` and not silently lose its gate during refactor. | In scope; audit view is in the priority gating set (§2 cut line). |
| 6 | **`LoginResult` DTO / shell coupling** — the shell reads `session.Role` today. | Design phase resolves the exact DTO/session field shape; proposal only commits that the enum is gone and a permission set + role identity replaces it. |
| 7 | **Cashier default permission set exact contents** | Deferred to design/spec — proposal commits to "Cashier keeps its current effective capabilities," design enumerates the precise key list so no cashier capability silently disappears. |

**Nothing here requires user sign-off before spec/design** — all locked decisions were respected and the open items are design-phase detail, not scope questions. The one thing worth surfacing to the user at review time: the **deferred-module cut line** (§2) — confirm the priority-only enforcement scope for v1 is acceptable and the follow-up hardening change is understood.

---

## Review Workload Forecast (directional — sdd-tasks recomputes)

**Blast radius estimate:**

- **New files** (~10–14): `Role`, `RolePermission` entities; `PermissionKeys` catalog; `RoleConfiguration`, `RolePermissionConfiguration`; `PermissionAuthorization` helper; role use cases (`GetAllRoles`, `CreateRole`, `UpdateRole`, `DeactivateRole`); the migration pair; `RolesManagementViewModel` + view; role repository interface + impl.
- **Modified files** (~20–30): `User` entity, `IUserRepository` (+ impl), `LoginUseCase`, `LoginResult`, `ICurrentOperatorSession`, `InMemoryUserSession`, `DatabaseSchemaRollout` seeding, `DependencyInjection`, `AppBootstrapper`, `MainShellViewModel`, `InvoicesViewModel`, `UserManagementViewModel`, `CancelSaleUseCase`, the ~6 priority use cases gaining gates (users ×3, settings ×3, backup ×2, audit), and the tests for all of the above. Delete `UserRole` enum + its references.
- **Modules getting real Application-layer enforcement in v1: ~6** (users, roles, backup, settings, audit, + cancel-sale unification). **Deferred: ~14** (products, categories, inventory, quotes, suppliers, purchases, customers, receivables, reports, cash register, shifts, loyalty, barcode, documents).

**Estimated changed lines: well above 400.** This is a hot-path change (auth/security/permissions/backup-restore surfaces).

**Chained PRs likely needed at tasks time: YES.** Natural slice boundaries:
1. Data + domain foundation (entities, catalog, migration, seeding, session plumbing, `UserRole` removal) — must land first, everything depends on it.
2. `PermissionAuthorization` helper + gate the priority use cases + unify `CancelSaleUseCase`.
3. WPF: shell per-module visibility + Roles & Permissions screen + the three ViewModel refactors.

Because slice 1 changes the `User` type and session contract that slices 2 and 3 build on, a `feature-branch-chain` (tracker branch accumulating integration) fits better than stacked-to-main. Final decision is the tasks phase's; flagging now so it isn't a surprise.
