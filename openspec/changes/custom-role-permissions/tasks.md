# Tasks: custom-role-permissions

**Change**: custom-role-permissions
**Phase**: tasks
**Store**: openspec
**Date**: 2026-07-12
**Inputs**: `proposal.md`, `design.md` (§12 sequencing, gate-passed — §6.3 Cashier defaults corrected), specs under `specs/`, `exploration.md`

Ordering matches design §12 dependency sequence exactly. Each task references exact file paths from
the design where specified. Test tasks are interleaved with the implementation task they verify (per
`work-unit-commits`: tests travel with the behavior they verify, not in a separate "tests" pass).

Strict TDD is OFF for this change — tests are written alongside/after each implementation task, not
test-first. Every scenario in the 5 delta specs is covered by at least one implementation task and at
least one test task; the mapping is noted inline as `Spec:`.

---

## Review Workload Forecast (see restated summary in final response — also here for file completeness)

- **Total tasks**: 47 (39 implementation/test tasks + 8 investigation/checklist tasks folded into their
  parent task, counted separately below).
- **Chained PRs recommended: Yes.**
- **400-line budget risk: High.**
- **Decision needed before apply: Yes** — orchestrator must resolve `delivery_strategy` (cached as
  `ask-on-risk`) into a concrete chaining decision before `sdd-apply` starts Slice 1.
- **Recommended chain strategy: `feature-branch-chain`** (tracker branch), confirming the proposal's
  and design's call — Slice 1 changes the `User` type, deletes `UserRole`, and changes the session
  contract; Slices 2 and 3 cannot compile against `main` until Slice 1 lands, so a stacked-to-main
  approach where each slice independently merges to `main` does not fit (Slice 2/3 would be broken on
  `main` between merges). A tracker branch absorbs that intermediate breakage; only the tracker merges
  to `main`.
- **Slice boundaries** (confirmed, matches proposal's 3-slice split, refined with exact task ranges):
  1. **Slice 1 — Data + domain foundation** (Tasks 1–15): Domain entities/catalog, `User` swap,
     `UserRole` enum deletion, EF configs/migration, repositories, seeding, `PermissionAuthorization`
     gate + call-site migration, `CancelSaleUseCase` unification, login snapshot, DI for what exists so
     far. **This is the largest slice** — everything downstream depends on it compiling.
  2. **Slice 2 — Guardrails + role use cases + remaining gates** (Tasks 16–29): self-lockout guardrails,
     new Roles use cases (Get/Create/Update/Deactivate), Settings/Backup gate wiring, DI registration
     for role use cases.
  3. **Slice 3 — WPF** (Tasks 30–39): `MainShellViewModel`, `InvoicesViewModel`,
     `UserManagementViewModel`, new `RolesManagementViewModel` + view + shell wiring.

Per-slice estimated changed lines (rough, from design's file list × typical entity/use-case/test size in
this codebase):
- Slice 1: ~550–700 lines (2 new entities, 1 catalog ~200 lines, 2 EF configs, 1 migration pair
  (~150–250 lines, EF-generated), 1 repository interface+impl, seeding rewrite, gate helper, 5 call-site
  edits, enum deletion touching ~15–20 compile-fixing files, session/DTO plumbing, ~10 test files).
- Slice 2: ~350–450 lines (4 new use cases + guardrail logic in 2 existing use cases + ~10 test files).
- Slice 3: ~500–650 lines (3 VM refactors + 1 new VM + 1 new XAML view + shell wiring + ~4 test files).

**Total: ~1,400–1,800 changed lines** — well above the 400-line single-PR budget, confirming the
proposal/design forecast. Slice 1 alone exceeds 400 lines and is not further splittable without
breaking the compile-atomicity constraint from design §1.4/D9 (enum + `RoleId` cannot coexist). Slice 1
is the accepted `size:exception`-shaped slice if the orchestrator chooses `feature-branch-chain` — it is
one PR against the tracker branch despite exceeding 400 lines, because splitting it further produces
non-compiling intermediate states, which is explicitly listed as an allowed exception in
`chained-pr/SKILL.md`'s Decision Gates ("Generated/vendor/migration diff cannot split cleanly → ask
maintainer for `size:exception`" — the same principle applies to an atomic type-migration diff).

---

## Slice 1 — Data + domain foundation

*Corresponds to design §12 steps 1–6. Must land first; Slices 2 and 3 do not compile without it.*

### 1. Domain foundation (design §12 step 1)

- [x] **1.1** Create `src/POS.Domain/Authorization/PermissionKeys.cs` — nested static classes per module
      exactly as specified in design §1.3 (all v1-enforced + all catalog-only modules), plus the
      reflection-built `All` property, plus `CashierDefaults` static array per design §6.3's exact list
      (`Cashier.Sell`, `Customers.View/Create/Edit`, `Quotes.View/Create/Convert`,
      `Invoices.View/Reprint/Return`, `Shifts.View/Open/Close` — 12 keys, nothing else).
      Spec: role-permissions "Permission catalog is code-owned in the Domain layer".
- [x] **1.2** Test: `POS.Domain.Tests` — `PermissionKeys.All` contains every gated key and every `.View`
      key (assert presence of a representative sample: `Users.Create`, `Roles.AssignPermissions`,
      `Admin.Backup.Restore`, `Products.View`, `Invoices.Cancel`); `CashierDefaults` contains exactly the
      12 design §6.3 keys and explicitly does NOT contain any admin-hub `.View` key (`Users.View`,
      `Roles.View`, `Audit.View`, `Settings.View`, `Admin.Backup.View`, `Products.View`,
      `Inventory.View`, `Suppliers.View`, `Reports.View`, `Loyalty.View`) or `Invoices.Cancel`.
      Spec: role-permissions "Full catalog is enumerable"; design §6.3 correction (Cashier must NOT gain
      admin-hub visibility — this is the exact defect the design review caught and fixed).
- [x] **1.3** Create `src/POS.Domain/Authorization/RoleConstants.cs` — `AdministratorRoleId`,
      `CashierRoleId` (exact Guids from design §6.1), name consts, `IsSystemAdministrator(Guid)`.
- [x] **1.4** Test: `POS.Domain.Tests` — `RoleConstants.AdministratorRoleId` and `CashierRoleId` are both
      non-default (`!= Guid.Empty`) and distinct from each other; `IsSystemAdministrator` returns true
      only for the admin id. (Explicit non-blocking-risk item: guard against seed-Guid collision.)
- [x] **1.5** Create `src/POS.Domain/Entities/Role.cs` per design §1.1 exactly (private ctor, public ctor,
      `CreateSystemRole` factory, `Rename`, `Deactivate`, `Touch`, `NormalizeName`). No navigation
      collection to `RolePermission` (design decision, §1.1).
- [x] **1.6** Test: `POS.Domain.Tests` — `Role` ctor rejects empty/whitespace name; `CreateSystemRole`
      sets `IsSystem = true` and the given `Id`; `Rename` trims and rejects empty, bumps `UpdatedAt`;
      `Deactivate` flips `IsActive` to false.
- [x] **1.7** Create `src/POS.Domain/Entities/RolePermission.cs` per design §1.2 exactly (composite key,
      ctor guards on empty `roleId`/`permissionKey`).
- [x] **1.8** Test: `POS.Domain.Tests` — `RolePermission` ctor rejects `Guid.Empty` roleId and
      empty/whitespace permissionKey.

### 2. User swap + enum deletion (design §12 step 2 — gated on all ~64 UserRole references migrating first)

- [x] **2.1** Investigation/checklist step (not a code change): run
      `grep -rn "UserRole" src/ tests/` to produce the authoritative current reference list (the
      design's "~64 files" figure is from exploration and may have drifted). Group findings into: (a)
      `User` entity itself, (b) session/DTO plumbing (`ICurrentOperatorSession`, `InMemoryUserSession`,
      `LoginResult`, `LoginUseCase`), (c) `AdminUserAuthorization` and its call sites, (d) WPF
      ViewModels (`MainShellViewModel`, `InvoicesViewModel`, `UserManagementViewModel`), (e) seeding
      (`DatabaseSchemaRollout`), (f) test files referencing `UserRole`. This grouping drives tasks 2.2
      through 2.8 and confirms nothing is missed before 2.9 deletes the enum.
- [x] **2.2** Modify `src/POS.Domain/Entities/User.cs` per design §1.5: ctor takes `Guid roleId` instead
      of `UserRole role`; `RoleId` property replaces `Role`; `ChangeRole(Guid)` replaces
      `UpdateRole(UserRole)`; remove `using POS.Domain.Enums;`. No navigation property (design decision).
      Spec: role-permissions "User has exactly one role".
- [x] **2.3** Test: `POS.Domain.Tests` — `User` ctor rejects `Guid.Empty` roleId; `ChangeRole` updates
      `RoleId`.
      Spec: role-permissions "User is created with a single role" scenario.
- [x] **2.4** Modify `src/POS.Application/Interfaces/ICurrentOperatorSession.cs` per design §5.1: remove
      `UserRole Role`, add `Guid RoleId`, `string RoleName`, `IReadOnlySet<string> Permissions`,
      `bool HasPermission(string permissionKey)`. Update the paired `IWritableOperatorSession.SetUser(...)`
      signature to the design §5.3 shape (`Guid id, string username, Guid roleId, string roleName,
      IReadOnlySet<string> permissions`).
- [x] **2.5** Modify `src/POS.Infrastructure/Persistence/InMemoryUserSession.cs` per design §5.3 exactly
      (empty-set default when unauthenticated, not "Cashier" — explicit spec requirement).
      Spec: user-auth "Unauthenticated session has no permissions".
- [x] **2.6** Test: `POS.Application.Tests` or `POS.Infrastructure.Tests` (wherever `InMemoryUserSession`
      is currently tested) — unauthenticated session has empty `Permissions` and `HasPermission` returns
      false for any key; after `SetUser`, `HasPermission` reflects the passed set; `ClearSession` resets
      to empty.
- [x] **2.7** Modify `src/POS.Application/DTOs/Auth/AuthDtos.cs` — `LoginResult` per design §5.4 (adds
      `RoleId`, `RoleName`, nullable `Permissions`; removes `Role` field/enum usage).
- [x] **2.8** Fix remaining compile breaks from the enum swap in non-WPF, non-seeding code identified in
      2.1(c)/(f) that are not covered by a later dedicated task — i.e., anything referencing `UserRole`
      that isn't `AdminUserAuthorization` (handled in Slice 1 §5), seeding (task 4.x), or WPF (Slice 3).
      Do NOT touch WPF ViewModels here — they are explicitly Slice 3 and are expected to be temporarily
      broken until then (acceptable within this atomic-swap slice per design §1.4/D9; the slice as a
      whole must compile by task 2.9, WPF projects may need a throwaway stub or are simply included in
      this same slice's compile scope — see note below).
- [x] **2.9** Delete `src/POS.Domain/Enums/UserRole.cs`. This task MUST be the last step of this
      sub-section, only after 2.1–2.8 confirm no remaining reference outside WPF/seeding/gate code that
      subsequent Slice-1 tasks (3.x, 4.x, 5.x, 6.x) will fix. **Note**: because WPF (`MainShellViewModel`,
      `InvoicesViewModel`, `UserManagementViewModel`) is design-assigned to Slice 3 but the solution as a
      whole must compile at the end of Slice 1 for CI to pass, Slice 1 MUST include the minimal WPF
      compile-fix (not full refactor — just swap `UserRole` references to the new `RoleId`/permission
      shape with the simplest correct expression, e.g. `session.RoleId == RoleConstants.AdministratorRoleId`
      in place of `role == UserRole.Administrator`) so the build is green; Slice 3 then replaces those
      minimal fixes with the real per-module permission-driven design (§9.1–9.4). Call this out explicitly
      in the Slice 1 PR description so reviewers know the WPF touch in Slice 1 is intentionally minimal
      and Slice 3 supersedes it.

### 3. EF layer (design §12 step 3)

- [x] **3.1** Create `src/POS.Infrastructure/Persistence/Configurations/RoleConfiguration.cs` per design
      §3.1 exactly. **Checklist item, do not skip**: `builder.Property(r => r.Id).HasConversion<string>()`
      MUST be present — omitting it reproduces the documented prior bug where EF binds Guid as
      uppercase TEXT and lookups silently never match.
- [x] **3.2** Create `src/POS.Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs`
      per design §3.2 exactly. **Checklist item**: `builder.Property(rp => rp.RoleId).HasConversion<string>()`
      MUST be present (same mandatory conversion, second occurrence in this same task).
- [x] **3.3** Modify `src/POS.Infrastructure/Persistence/Configurations/UserConfiguration.cs` per design
      §3.3: add `RoleId` property with `HasConversion<string>()` (**checklist item, third and last
      mandatory occurrence of this conversion in this change**) and the `HasOne<Role>().WithMany()`
      FK with `DeleteBehavior.Restrict`.
- [x] **3.4** Modify `src/POS.Infrastructure/Persistence/AppDbContext.cs` — add
      `DbSet<Role> Roles => Set<Role>();` and `DbSet<RolePermission> RolePermissions => Set<RolePermission>();`
      after the `Users` DbSet (design §3.4).
- [x] **3.5** Create `src/POS.Domain/Interfaces/IRoleRepository.cs` per design §4.1 exactly (9 methods:
      `GetByIdAsync`, `GetByNameAsync`, `GetAllAsync`, `HasPermissionAsync`, `GetPermissionKeysAsync`,
      `AddAsync`, `UpdateAsync`, `ReplacePermissionsAsync`, `CountUsersInRoleAsync`).
- [x] **3.6** Create `src/POS.Infrastructure/Persistence/Repositories/RoleRepository.cs` (or wherever
      `UserRepository` lives — mirror its location) implementing `IRoleRepository`, following the
      `UserRepository` pattern (constructor-injected `AppDbContext`, async EF queries).
      `ReplacePermissionsAsync` does NOT call `SaveChangesAsync` itself (design §4.1 — the calling use
      case owns the commit so rename + permission-replace are atomic).
- [x] **3.7** Modify `src/POS.Domain/Interfaces/IUserRepository.cs` — add
      `Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default);` (design §4.2).
- [x] **3.8** Implement `CountActiveUsersInRoleAsync` in `UserRepository`.
- [x] **3.9** Test: `POS.Infrastructure.Tests` (or equivalent integration test project against a real/
      in-memory SQLite context) — `RoleRepository.ReplacePermissionsAsync` deletes prior grants and
      inserts the new set in one logical operation; `HasPermissionAsync` / `GetPermissionKeysAsync`
      round-trip correctly; **EF Guid conversion regression guard**: a `RolePermission` row and a
      `User.RoleId` value round-trip and a lookup-by-id matches (this is the specific test named as
      required — it is what would have caught the historical uppercase-TEXT bug).
- [x] **3.10** Generate the migration: run `dotnet ef migrations add ReplaceUserRoleEnumWithRbac` from
      the Infrastructure project **after** tasks 3.1–3.8 compile. Do NOT hand-write the generated
      `.Designer.cs`/model snapshot — regenerate via the CLI per design §3.5. Verify the generated `Up`/
      `Down` match design §3.5's described contents (Roles table, RolePermissions table, drop
      `Users.Role`, add `Users.RoleId` FK, unique index on `Roles.Name`, index on `Users.RoleId`).

### 4. Seeding (design §12 step 4)

- [x] **4.1** Modify `src/POS.Infrastructure/DatabaseSchemaRollout.cs`: rename `SeedDefaultAdminAsync` →
      `SeedDefaultRolesAndAdminAsync`, implement the 3-step order from design §6.2 (seed both system
      roles idempotently → seed Cashier's `RolePermission` rows from `PermissionKeys.CashierDefaults`,
      Administrator gets zero rows → seed the `admin` user pointed at `RoleConstants.AdministratorRoleId`),
      each step guarded by its own `AnyAsync` check.
      Spec: role-permissions "System roles are seeded on empty database", "Seeding is idempotent".
- [x] **4.2** Test: `POS.Infrastructure.Tests` — seeding on empty DB creates exactly Administrator +
      Cashier system roles, both `IsActive = true`; admin user's `RoleId` equals
      `RoleConstants.AdministratorRoleId`; Cashier role's persisted grants exactly match
      `PermissionKeys.CashierDefaults` (set-equality assertion, catching the exact §6.3 defect class the
      design review fixed); Administrator role has **zero** `RolePermission` rows (bypass, not
      enumerated). Running seeding twice produces no duplicate roles/rows (idempotency).
      Spec: role-permissions "System roles are seeded on empty database", "Seeding is idempotent",
      "Administrator bypasses permission checks" (zero-rows precondition).

### 5. Gate (design §12 step 5)

- [x] **5.1** Create `src/POS.Application/UseCases/Auth/PermissionAuthorization.cs` per design §2 exactly
      — both overloads (`EnsureHasPermission` session fast-path, `EnsureHasPermissionAsync` repository
      fallback with the Administrator bypass short-circuit via `RoleConstants.IsSystemAdministrator`).
      Spec: role-permissions "PermissionAuthorization gate".
- [x] **5.2** Test: `POS.Application.Tests` — for both overloads: admin bypass allows without a
      `RolePermission` row lookup (assert the repository's `HasPermissionAsync` is never called on the
      admin path, or use a role with zero rows to prove it's not row-based); non-admin with the granted
      key allows; non-admin without the key denies; inactive requester denies even when role is
      Administrator; inactive role denies; `Guid.Empty` requesting user id denies.
      Spec: role-permissions "Administrator bypasses permission checks", "Non-administrator role with
      the required grant succeeds", "Non-administrator role without the required grant is denied",
      "Inactive requester is denied regardless of role".
- [x] **5.3** Replace all 4 `AdminUserAuthorization.EnsureAdministratorAsync` call sites per design §2.1
      table: `CreateUserUseCase.cs` → `PermissionKeys.Users.Create`; `UpdateUserUseCase.cs` →
      `PermissionKeys.Users.Edit`; `DeactivateUserUseCase.cs` → `PermissionKeys.Users.Deactivate`;
      `GetAuditLogsUseCase.cs` → `PermissionKeys.Audit.View`.
      Spec: role-permissions "Users and Roles module gating" (Create scenario); audit-trail "Audit log
      viewing is permission-gated".
- [x] **5.4** Test updates for 5.3's four use cases — existing "denies non-admin" tests become "denies
      caller without the specific key"; add a case for a custom role granted the specific key but not
      Administrator succeeding (proves it's no longer admin-or-nothing).
      Spec: role-permissions "User without Users.Create is denied creating a user"; audit-trail "User
      without Audit.View is denied reading audit logs", "Administrator retains audit log access via the
      bypass".
- [x] **5.5** Delete `src/POS.Application/UseCases/Auth/AdminUserAuthorization.cs` — confirm (via
      grep/build) it has zero remaining callers after 5.3.
- [x] **5.6** Modify `src/POS.Application/UseCases/Invoices/CancelSaleUseCase.cs`: delete the inline
      `if (user.Role != UserRole.Administrator) throw ...` check, replace with
      `PermissionAuthorization.EnsureHasPermissionAsync(..., PermissionKeys.Invoices.Cancel, ct)`
      authorizing `request.CancelledByUserId`, throwing `UnauthorizedActionException("CancelSale")` on
      deny (matches existing throw shape).
      Spec: role-permissions "CancelSaleUseCase unified onto PermissionAuthorization";
      invoice-management "Sale cancellation authorization" (MODIFIED requirement).
- [x] **5.7** Test: `POS.Application.Tests` — Cashier without `Invoices.Cancel` denied, sale status
      unchanged; Administrator succeeds via bypass; custom role explicitly granted `Invoices.Cancel`
      succeeds; assert no inline `UserRole` comparison remains (structural/behavioral proof, e.g. a
      custom non-admin role WITH the grant succeeds — this could not pass under the old inline check).
      Spec: invoice-management all 3 scenarios ("Cashier without Invoices.Cancel is denied cancelling a
      sale", "Administrator succeeds cancelling a sale via the bypass", "Custom role explicitly granted
      Invoices.Cancel succeeds").
- [x] **5.8** Add new gates (session fast-path, no prior check existed) per design §2.1 second table:
      `TriggerManualBackupUseCase.cs` → `Admin.Backup.Create`, deny returns
      `ManualBackupResult(false, <msg>, null)`; `RestoreBackupUseCase.cs` → `Admin.Backup.Restore`, deny
      returns `RestoreBackupResult(false, <msg>)`. Both require injecting `ICurrentOperatorSession` (new
      dependency per design §2 table).
      Spec: system-settings "Manual backup creation is permission-gated", "Backup restore is
      permission-gated"; role-permissions "Backup Create and Restore are distinct permission keys".
- [x] **5.9** Test: `POS.Application.Tests` — the exact Create-vs-Restore matrix required by the user and
      named in the spec: role with only `Admin.Backup.Create` → denied on Restore, allowed on Create;
      role with `Admin.Backup.Restore` → allowed on Restore; role with neither → denied on both.
      Spec: system-settings both scenarios; role-permissions all 4 "Backup Create and Restore are
      distinct permission keys" scenarios (this is the explicitly user-required backup test coverage).
- [x] **5.10** Add new gates for Settings per design §2.1 note (throw `UnauthorizedActionException` on
      deny, per D6): `SaveSettingsUseCase.cs` → `Settings.Edit`; `LoadSettingsUseCase.cs` →
      `Settings.View`; `UpdateBackupDirectoryUseCase.cs` → `Settings.Edit`. All session fast-path,
      inject `ICurrentOperatorSession`.
      Spec: system-settings "Settings view and edit are permission-gated" (all 3 scenarios).
- [x] **5.11** Test: `POS.Application.Tests` — `LoadSettingsUseCase` throws without `Settings.View`;
      `SaveSettingsUseCase` throws without `Settings.Edit`, settings unchanged; `UpdateBackupDirectoryUseCase`
      succeeds with `Settings.Edit`, persists the path.
      Spec: system-settings all 3 "Settings view and edit are permission-gated" scenarios.

### 6. Login snapshot (design §12 step 6)

- [x] **6.1** Modify `src/POS.Application/UseCases/Auth/LoginUseCase.cs` per design §5.5: inject
      `IRoleRepository`, after credential validation load the role, materialize
      `new HashSet<string>(PermissionKeys.All)` for the Administrator role or
      `GetPermissionKeysAsync(role.Id, ct)` otherwise, return the extended `LoginResult`.
      Spec: user-auth "Login snapshots the role's permission set into the session" (both scenarios).
- [x] **6.2** Modify `src/POS.Presentation/AppBootstrapper.cs` (design §5.5 note, lines ~42/~207/~267):
      read `result.RoleName`/`result.Permissions` instead of `session.Role`; call the new
      `SetUser(id, username, roleId, roleName, permissions)` signature.
- [x] **6.3** Test: `POS.Application.Tests` — login for a custom role "Report Viewer" granted only
      `Reports.View` populates `session.Permissions` with exactly `{Reports.View}` and `RoleId` matches;
      login for Administrator yields `HasPermission` true for every valid key including one not
      explicitly enumerated in any row; unauthenticated session has empty permissions.
      Spec: user-auth "Login populates session permissions for a custom role", "Login grants
      Administrator full access via bypass", "Unauthenticated session has no permissions".
- [x] **6.4** Test: `POS.Application.Tests` — **session snapshot / "effective on next login" behavior**
      (explicitly user-required coverage): a logged-in user's session `Permissions` does NOT change when
      an admin edits that user's role's grants mid-session (assert the live session object is unaffected
      by a `ReplacePermissionsAsync` call on the same role); a subsequent fresh login for the same user
      reflects the updated grants.
      Spec: user-auth "Mid-session role edit does not affect the active session", "Next login reflects
      the updated grants".

---

## Slice 2 — Guardrails + role use cases + DI

*Corresponds to design §12 steps 7–9. Depends on Slice 1 (repositories, gate helper, `RoleId`).*

### 7. Guardrails (design §12 step 7)

- [x] **7.1** Modify `src/POS.Application/UseCases/Users/DeactivateUserUseCase.cs`: after auth + user
      lookup, if `user.RoleId == RoleConstants.AdministratorRoleId` AND
      `await _users.CountActiveUsersInRoleAsync(AdministratorRoleId) <= 1` → deny with the design §7
      message. Guard runs before mutating the entity.
      Spec: role-permissions "Self-lockout guardrails" — "Deny deactivating the last active
      administrator", "Allow deactivating an administrator when another remains active".
- [x] **7.2** Modify `src/POS.Application/UseCases/Users/UpdateUserUseCase.cs`: replace `UserRole newRole`
      param with `Guid newRoleId` (design §7 note); if reassigning the last active administrator away
      from the Administrator role → deny.
      Spec: role-permissions "Deny reassigning the last active administrator's role away from
      Administrator".
- [x] **7.3** Test: `POS.Application.Tests` — exactly the 4 self-lockout scenarios named as required user
      coverage: deny deactivating the sole active admin; allow deactivating an admin when a second admin
      remains active (and confirm the deactivated user's `IsActive` becomes false); deny reassigning the
      sole active admin's role away from Administrator (and confirm `RoleId` remains unchanged); (the
      4th — deny stripping admin permissions — is covered in task 8.3 alongside `UpdateRoleUseCase`
      since that's where the guard lives).
      Spec: role-permissions "Self-lockout guardrails" — first 3 scenarios.

### 8. Role use cases (design §12 step 8)

- [x] **8.1** Create `src/POS.Application/UseCases/Roles/GetAllRolesUseCase.cs` per design §8.1
      (`RoleSummary` record, gate `Roles.View`).
- [x] **8.2** Create `src/POS.Application/UseCases/Roles/CreateRoleUseCase.cs` per design §8.2: gate
      `Roles.Create`; validate name non-empty and not taken (`GetByNameAsync`); validate every key ∈
      `PermissionKeys.All` (reject unknown keys); create `Role`, `ReplacePermissionsAsync`, one
      `SaveChanges`.
      Spec: role-permissions "Assigning a permission persists a RolePermission row".
- [x] **8.3** Create `src/POS.Application/UseCases/Roles/UpdateRoleUseCase.cs` per design §8.3 and the D5
      grant-diff subroutine detailed in design §13: gate `Roles.Edit`; load current grants via
      `GetPermissionKeysAsync`; compare with incoming set via `IReadOnlySet<string>.SetEquals` (ordinal);
      if not equal, additionally gate `Roles.AssignPermissions`; admin-strip guard — for the
      Administrator role, if the incoming set does not equal the current (empty) set → deny; validate
      new name uniqueness (excluding self) and all keys ∈ catalog; `Rename` + `ReplacePermissionsAsync`
      in one `SaveChanges`.
      Spec: role-permissions "Revoking a permission removes the RolePermission row", "User without
      Roles.AssignPermissions is denied editing role grants", "Deny stripping permissions from the
      system Administrator role".
- [x] **8.4** Test: `POS.Application.Tests` — **grant-diff subroutine, explicit non-blocking-risk item**:
      `SetEquals` comparison correctly identifies unchanged-grant renames (only `Roles.Edit` required) vs
      changed-grant edits (both `Roles.Edit` and `Roles.AssignPermissions` required); cover the
      empty-set-vs-empty-set case (rename-only on a role with zero grants) and the
      identical-non-empty-set case (both must NOT require `AssignPermissions`); cover a caller with only
      `Roles.Edit` attempting a grant change being denied; assigning `Reports.View` persists the row;
      revoking it removes the row.
      Spec: role-permissions "User without Roles.AssignPermissions is denied editing role grants",
      "Assigning a permission persists a RolePermission row", "Revoking a permission removes the
      RolePermission row".
- [x] **8.5** Test: `POS.Application.Tests` — **self-lockout guardrail #4** (admin-strip): attempting to
      modify the Administrator role's permission grants in any way (add or remove) via `UpdateRoleUseCase`
      is denied and the role's grant state (zero rows) remains unchanged.
      Spec: role-permissions "Deny stripping permissions from the system Administrator role".
- [x] **8.6** Test: `POS.Application.Tests` — `CreateRoleUseCase` rejects a duplicate name; rejects a
      request containing a key not in `PermissionKeys.All`.
- [x] **8.7** Create `src/POS.Application/UseCases/Roles/DeactivateRoleUseCase.cs` per design §8.4: gate
      `Roles.Deactivate`; deny if `role.IsSystem`; deny if `CountUsersInRoleAsync(roleId) > 0`; else
      `Deactivate()` + `UpdateAsync` + `SaveChanges`. Confirm no `DeleteRoleUseCase` file exists anywhere
      in the change (spec explicitly requires its absence).
      Spec: role-permissions "System roles cannot be deactivated" (both scenarios), "Roles assigned to a
      user cannot be deactivated" (both scenarios), "No delete role use case exists".
- [x] **8.8** Test: `POS.Application.Tests` — deny deactivating Administrator (IsSystem); deny
      deactivating Cashier (IsSystem); deny deactivating a custom role with an assigned user (active or
      inactive assignment); allow deactivating an unassigned custom role, confirm `IsActive` becomes
      false.
      Spec: role-permissions all 4 "System roles cannot be deactivated" / "Roles assigned to a user
      cannot be deactivated" scenarios.

### 9. DI registration (design §12 step 9)

- [x] **9.1** Modify `src/POS.Infrastructure/DependencyInjection.cs` per design §10: register
      `IRoleRepository → RoleRepository` near the `IUserRepository` registration (~line 73); register
      `GetAllRolesUseCase`, `CreateRoleUseCase`, `UpdateRoleUseCase`, `DeactivateRoleUseCase` near the
      user-management use case block (~line 155–158).
- [x] **9.2** Verify (build-only check, no new test file): the app builds and starts with the new DI
      graph — `LoginUseCase`'s new `IRoleRepository` dependency and the settings/backup use cases' new
      `ICurrentOperatorSession` dependency resolve without a missing-registration error (both are
      already-registered service types, per design §10's note — confirm, don't assume).

---

## Slice 3 — WPF

*Corresponds to design §12 step 10. Depends on Slice 1 (session/permission shape) and Slice 2
(`GetAllRolesUseCase`, role use cases for the new screen).*

### 10. Settings navigation investigation (non-blocking risk — do FIRST in this slice)

- [x] **10.1** Investigate how the `Settings` module is actually surfaced in the current WPF shell today.
      Design/exploration confirms `Settings` is NOT present in `MainShellViewModel`'s `FuncbarModules`,
      `AdminHubModules`, or `AdminHubExtraModules` collections as they exist pre-change — this is a
      pre-existing quirk, not introduced by this change. Read `MainShellViewModel.cs` and any Settings
      view/window construction path (check `AppBootstrapper.cs` and any menu/toolbar XAML) to find the
      actual current entry point (e.g. a separate window, a menu item outside the module list, or a
      currently-dead/unreachable path). Document the finding as a code comment at the `Settings.View`
      wiring site in task 10.2, and if Settings turns out to have no current UI entry point at all, note
      that explicitly rather than inventing a new one — this change's scope is gating existing surfaces,
      not adding new navigation UX.

### 11. `MainShellViewModel` (design §9.1)

- [x] **11.1** Modify `src/POS.Presentation.Shared/ViewModels/MainShellViewModel.cs`: replace ctor's
      `UserRole role` param with `IReadOnlySet<string> permissions, string roleName` (design §9.1 exact
      signature: `(CashierViewModel cashier, GetDashboardSummaryUseCase? dashboard, IReadOnlySet<string>
      permissions, string roleName, string username)`); add the `ModuleViewKeys` static dictionary
      exactly as design §9.1 lists (including the new `"roles"` entry); replace the single `bool isAdmin`
      module-list logic with per-module `permissions.Contains(ModuleViewKeys[key])` checks; add `"roles"`
      to the admin hub module set; parameterless/design-time ctors default to
      `new HashSet<string>(PermissionKeys.All)` (matches old Administrator-default behavior); replace the
      enum-switch display-name logic with the passed `roleName` string. Wire `Settings.View` onto
      whatever entry point task 10.1 identified.
- [x] **11.2** Test updates: `POS.Presentation.Shared.Tests` — the existing hub-module-count assertions
      (flagged in design §9.1 as testing a "7-module hub contract") are updated to construct the VM with
      a permission set instead of `UserRole`; admin (full catalog set) still yields the same module count
      as before (regression guard — full-permission Administrator must see everything a plain
      `UserRole.Administrator` did); a partial custom-role set shows only the modules whose `.View` key
      is present; add a case asserting the new `"roles"` module appears only when `Roles.View` is
      granted; add a case for a Cashier-shaped permission set (exactly `PermissionKeys.CashierDefaults`)
      confirming it sees ONLY the five funcbar modules and none of the admin-hub modules — this is the
      direct regression test for the §6.3 defect the design review caught and fixed.
      Spec: role-permissions "Deferred module key exists in the catalog and is assignable" (`.View` key
      drives visibility, confirmed at VM level).

### 12. `InvoicesViewModel` (design §9.2)

- [x] **12.1** Modify `src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs`: remove
      `CurrentUserRole`/`IsAdministrator`, add `CanCancel => _session.HasPermission(PermissionKeys.Invoices.Cancel)`
      (inject `ICurrentOperatorSession` if not already present); `CancelSaleAsync`'s `CanExecute` uses
      `CanCancel`.
- [x] **12.2** Modify `src/POS.Presentation/AppBootstrapper.cs` — remove the
      `CurrentUserRole = session.Role` assignment (design §9.2, was line ~207); VM now reads the session
      directly via its injected dependency.
- [x] **12.3** Test: `POS.Presentation.Shared.Tests` — `CanCancel` is true when the session has
      `Invoices.Cancel`, false otherwise; `CancelSaleAsync`'s `CanExecute` follows `CanCancel`.

### 13. `UserManagementViewModel` (design §9.3)

- [x] **13.1** Modify `src/POS.Presentation.Shared/ViewModels/UserManagementViewModel.cs`: replace
      `IsAdmin` with `CanCreate`/`CanEdit`/`CanDeactivate` per-command permission checks against
      `Users.Create`/`Users.Edit`/`Users.Deactivate`; command `CanExecute` predicates updated
      accordingly; replace the `EnsureAdmin()` helper with permission-keyed variants.
- [x] **13.2** Modify `AvailableRoles` (currently a hardcoded `[Cashier, Administrator]` list at line ~49):
      change to `ObservableCollection<RoleSummary>` loaded in `LoadAsync` via the injected
      `GetAllRolesUseCase` (active roles only); `NewRole`/`EditRole` fields become `Guid` (`RoleId`)
      instead of `UserRole`.
- [x] **13.3** Update `CreateUserUseCase`/`UpdateUserUseCase` call sites in this VM to pass `RoleId`
      (Guid) instead of `UserRole`.
- [x] **13.4** Test: `POS.Presentation.Shared.Tests` — command `CanExecute` predicates follow the
      `Users.*` permission checks (a session with only `Users.Create` can create but not edit/deactivate,
      etc.); `AvailableRoles` reflects the live `GetAllRolesUseCase` result (mock/fake the use case,
      assert the VM's collection matches).

### 14. New `RolesManagementViewModel` + view (design §9.4) — new module, mirrors `UserManagementViewModel`

- [x] **14.1** Create `src/POS.Presentation.Shared/ViewModels/RolesManagementViewModel.cs` per design
      §9.4: inject `GetAllRolesUseCase, CreateRoleUseCase, UpdateRoleUseCase, DeactivateRoleUseCase,
      IRoleRepository, ICurrentOperatorSession`; `Roles` (`ObservableCollection<RoleSummary>`),
      `SelectedRole`; `PermissionOptionRow(string Key, string Module, bool IsGranted)` collection built
      from `PermissionKeys.All` grouped by module; on role selection, load current grants via
      `IRoleRepository.GetPermissionKeysAsync` and set toggle state; on save, gather checked keys into a
      set and call `CreateRoleUseCase`/`UpdateRoleUseCase`; command gating per design (create→
      `Roles.Create`, save→`Roles.Edit` [+`Roles.AssignPermissions` when grants changed — VM-side mirror
      of the use-case-side check for responsive `CanExecute`/error messaging], deactivate→
      `Roles.Deactivate`, load→`Roles.View`); disable permission toggles for the Administrator row
      (bypass, not editable); disable Deactivate for both system roles; surface a message on denial.
- [x] **14.2** Create `src/POS.Presentation.Shared/Views/RolesManagementView.xaml` (+ code-behind if the
      project's convention uses one, matching `UserManagementView.xaml`'s pattern) — roles list on the
      left, edit/permission-assignment panel on the right with a grouped, toggleable permission list.
      Mirror `UserManagementView.xaml`'s DataTemplate/ItemsControl structure; this is MVVM wiring, not
      pixel-level design.
- [x] **14.3** Wire the new module into the shell: add a `"roles"` `ModuleNavigationItem` to
      `MainShellViewModel.allModules`/admin hub set (if not already fully covered by task 11.1's
      `ModuleViewKeys` addition — this task is the remaining ctor/property wiring: a `RolesManagement`
      init property and an `IsRolesSelected` toggle paralleling `IsUsersSelected`); add
      `RolesManagementViewModel` construction to wherever `UserManagementViewModel` is composed (DI/
      composition root in the Presentation project).
- [x] **14.4** Test: `POS.Presentation.Shared.Tests` — `RolesManagementViewModel`: grant toggles
      correctly load the selected role's current grants and correctly save a modified set; the
      Administrator row's toggles are disabled; both system roles have Deactivate disabled; command
      gating follows the `Roles.*` keys (a session with only `Roles.View` cannot execute create/save/
      deactivate).
      Spec: role-permissions "No delete role use case exists" (confirmed at UI level — no delete command
      exists on this VM).

---

## Cross-cutting final check (end of Slice 3, or a short standalone task if the orchestrator prefers)

- [ ] **15.1** Full-repo grep for `UserRole` and `AdminUserAuthorization` returns zero matches outside
      this change's own historical docs/comments. Confirms spec "User has exactly one role" ("The
      `UserRole` enum MUST be fully removed from the codebase; no code path may reference it after this
      change") and the `AdminUserAuthorization` deletion (task 5.5) has no orphaned references introduced
      later in Slices 2–3.
- [ ] **15.2** Confirm the deferred-module spec requirement holds with no extra work needed: spot-check
      one deferred use case (e.g. `CreateProductUseCase`) has NOT been touched by this change and has no
      new permission gate — this is intentionally a no-op verification task, not an implementation task,
      confirming the cut line in proposal §2 was respected.
      Spec: role-permissions "Deferred module key exists in the catalog and is assignable", "Deferred
      module use case has no Application-layer gate in this change".

---

## Task-to-spec coverage summary

| Spec file | Requirements | Covered by |
|---|---|---|
| role-permissions | Role entity/seeding | 1.5–1.6, 4.1–4.2 |
| role-permissions | No delete, only deactivate | 8.7–8.8, 14.4 |
| role-permissions | System roles cannot be deactivated | 8.7–8.8 |
| role-permissions | Roles assigned to a user cannot be deactivated | 8.7–8.8 |
| role-permissions | RolePermission persisted grants | 1.7–1.8, 8.2–8.4 |
| role-permissions | User has exactly one role / enum removal | 2.2–2.3, 2.9, 15.1 |
| role-permissions | PermissionAuthorization gate | 5.1–5.2 |
| role-permissions | CancelSaleUseCase unified | 5.6–5.7 |
| role-permissions | Backup Create/Restore distinct keys | 5.8–5.9 |
| role-permissions | Users/Roles module gating | 5.3–5.4, 8.4 |
| role-permissions | Self-lockout guardrails | 7.1–7.3, 8.5 |
| role-permissions | Permission catalog code-owned | 1.1–1.2 |
| role-permissions | Deferred-module keys exist, not enforced | 1.1, 11.2, 15.2 |
| user-auth | Login snapshots permissions | 6.1, 6.3 |
| user-auth | Effective on next login only | 6.4 |
| system-settings | Settings view/edit gated | 5.10–5.11 |
| system-settings | Manual backup gated | 5.8–5.9 |
| system-settings | Backup restore gated | 5.8–5.9 |
| audit-trail | Audit log viewing gated | 5.3–5.4 |
| invoice-management | Sale cancellation authorization | 5.6–5.7 |

Every scenario across all 5 delta specs maps to at least one implementation task and at least one test
task. The two explicitly user-required coverage areas — backup Create-vs-Restore allow/deny matrix
(5.9) and self-lockout guardrails (7.3, 8.5) — each have a dedicated test task, not a folded-in
assertion.
