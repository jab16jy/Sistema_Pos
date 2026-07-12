<Delta for role-permissions>
## ADDED Requirements

### Requirement: Role entity and system roles

The system MUST persist roles as a `Role` entity with `Id`, `Name` (unique), `IsSystem`, `IsActive`, `CreatedAt`, and `UpdatedAt`. The system MUST seed exactly two system roles on first run: `Administrator` (all permissions, via bypass) and `Cashier` (a fixed baseline permission set). The seeded `admin` user MUST be assigned the Administrator role.

#### Scenario: System roles are seeded on empty database
- GIVEN the `Roles` table is empty
- WHEN the application starts and seeding runs
- THEN an `Administrator` role MUST exist with `IsSystem = true` and `IsActive = true`
- AND a `Cashier` role MUST exist with `IsSystem = true` and `IsActive = true`
- AND the seeded `admin` user's `RoleId` MUST point to the Administrator role

#### Scenario: Seeding is idempotent
- GIVEN the `Roles` table already contains the Administrator and Cashier roles
- WHEN the application starts again
- THEN no duplicate roles MUST be created

### Requirement: Roles cannot be deleted, only deactivated

The system MUST NOT expose any delete operation for roles. The only lifecycle transition for a role after creation is deactivation (`IsActive = false`) via `DeactivateRoleUseCase`.

#### Scenario: No delete role use case exists
- GIVEN the Roles & Permissions module
- WHEN available role operations are enumerated
- THEN only Create, Edit (rename/permission assignment), and Deactivate operations MUST exist
- AND no Delete operation MUST be present

### Requirement: System roles cannot be deactivated

`DeactivateRoleUseCase` MUST deny deactivation when the target role has `IsSystem = true`.

#### Scenario: Deny deactivating the Administrator role
- GIVEN the system Administrator role
- WHEN an authorized caller attempts to deactivate it
- THEN the use case MUST return an error and MUST NOT change `IsActive`

#### Scenario: Deny deactivating the Cashier role
- GIVEN the system Cashier role
- WHEN an authorized caller attempts to deactivate it
- THEN the use case MUST return an error and MUST NOT change `IsActive`

### Requirement: Roles assigned to a user cannot be deactivated

`DeactivateRoleUseCase` MUST deny deactivation when at least one user (active or inactive) is currently assigned that `RoleId`.

#### Scenario: Deny deactivating a role with an assigned user
- GIVEN a custom role "Report Viewer" assigned to at least one `User.RoleId`
- WHEN an authorized caller attempts to deactivate "Report Viewer"
- THEN the use case MUST return an error indicating the role is in use
- AND `IsActive` MUST remain unchanged

#### Scenario: Allow deactivating an unassigned custom role
- GIVEN a custom role "Report Viewer" with zero users assigned
- WHEN an authorized caller with `Roles.Deactivate` deactivates it
- THEN `IsActive` MUST become `false`

### Requirement: RolePermission persisted grants keyed by catalog string keys

The system MUST persist role-to-permission grants as `RolePermission` rows with a composite key `(RoleId, PermissionKey)`, where `PermissionKey` is a string matching a key defined in the code-owned `PermissionKeys` catalog (`POS.Domain`). The catalog MUST NOT be represented as a database table; `RolePermission.PermissionKey` is not FK-constrained to a table, only convention-constrained to the catalog's known keys.

#### Scenario: Assigning a permission persists a RolePermission row
- GIVEN a custom role "Report Viewer" with no granted permissions
- WHEN an authorized caller assigns the `Reports.View` key to it
- THEN a `RolePermission` row `(RoleId = <Report Viewer id>, PermissionKey = "Reports.View")` MUST be persisted

#### Scenario: Revoking a permission removes the RolePermission row
- GIVEN a custom role with a granted `Reports.View` key
- WHEN an authorized caller revokes `Reports.View`
- THEN the corresponding `RolePermission` row MUST no longer exist

### Requirement: User has exactly one role

`User.RoleId` MUST reference exactly one `Role`. The `UserRole` enum MUST be fully removed from the codebase; no code path may reference it after this change.

#### Scenario: User is created with a single role
- GIVEN a request to create a user with `RoleId` set to the Cashier role
- WHEN the user is created
- THEN the persisted `User.RoleId` MUST equal the Cashier role's id
- AND the user MUST have no other role association

### Requirement: PermissionAuthorization gate

The system MUST provide a single Application-layer authorization helper, `PermissionAuthorization.EnsureHasPermissionAsync`, that all permission-gated use cases call. It MUST:
1. Resolve the requesting user by id.
2. Deny if the requester is inactive.
3. Allow unconditionally if the requester's role is the system Administrator role (bypass — no `RolePermission` row lookup).
4. Otherwise allow only if the requester's role has a `RolePermission` row for the required `PermissionKey`.

#### Scenario: Administrator bypasses permission checks
- GIVEN a requesting user whose role is the system Administrator role
- AND the Administrator role has zero explicit `RolePermission` rows (bypass-based, not enumerated)
- WHEN `EnsureHasPermissionAsync` is called with any valid `PermissionKey`
- THEN the check MUST succeed with no error, without querying `RolePermission` rows

#### Scenario: Non-administrator role with the required grant succeeds
- GIVEN a requesting user whose role has a `RolePermission` row for `Settings.Edit`
- WHEN `EnsureHasPermissionAsync` is called with `PermissionKey = "Settings.Edit"`
- THEN the check MUST succeed with no error

#### Scenario: Non-administrator role without the required grant is denied
- GIVEN a requesting user whose role has no `RolePermission` row for `Settings.Edit`
- WHEN `EnsureHasPermissionAsync` is called with `PermissionKey = "Settings.Edit"`
- THEN the check MUST return an authorization error
- AND the underlying use case MUST NOT execute its side effects

#### Scenario: Inactive requester is denied regardless of role
- GIVEN a requesting user with `IsActive = false` whose role is Administrator
- WHEN `EnsureHasPermissionAsync` is called with any `PermissionKey`
- THEN the check MUST return an authorization error

### Requirement: CancelSaleUseCase unified onto PermissionAuthorization

`CancelSaleUseCase` MUST authorize via `PermissionAuthorization.EnsureHasPermissionAsync(..., PermissionKeys.Invoices.Cancel, ct)` and MUST NOT contain any inline role-comparison check. This is the only permission gate that consumes `PermissionAuthorization` for a pre-existing inline check; all other gated use cases in this change are newly gated (they had no prior check).

#### Scenario: Cashier without Invoices.Cancel is denied
- GIVEN a Cashier-role user whose role has no `RolePermission` row for `Invoices.Cancel`
- WHEN the user calls `CancelSaleUseCase` for a completed sale
- THEN the use case MUST return/throw an authorization error
- AND the sale's status MUST remain unchanged

#### Scenario: Administrator succeeds via the bypass
- GIVEN an Administrator-role user
- WHEN the user calls `CancelSaleUseCase` for a completed sale
- THEN the authorization check MUST succeed
- AND the sale MUST be cancelled per existing cancellation behavior

### Requirement: Backup Create and Restore are distinct permission keys

`Admin.Backup.Create` and `Admin.Backup.Restore` MUST be separate `PermissionKey` values. A role MAY be granted one without the other. `TriggerManualBackupUseCase` MUST require `Admin.Backup.Create`; `RestoreBackupUseCase` MUST require `Admin.Backup.Restore`.

#### Scenario: Role with only Create key is denied on Restore
- GIVEN a custom role granted `Admin.Backup.Create` but not `Admin.Backup.Restore`
- WHEN a user with that role calls `RestoreBackupUseCase`
- THEN the use case MUST return an authorization error
- AND no restore operation MUST be performed

#### Scenario: Role with Restore key succeeds at Restore
- GIVEN a custom role granted `Admin.Backup.Restore`
- WHEN a user with that role calls `RestoreBackupUseCase` with a valid backup file
- THEN the authorization check MUST succeed
- AND the restore MUST proceed per existing restore behavior

#### Scenario: Role with only Create key succeeds at Create
- GIVEN a custom role granted `Admin.Backup.Create` but not `Admin.Backup.Restore`
- WHEN a user with that role calls `TriggerManualBackupUseCase`
- THEN the authorization check MUST succeed
- AND a backup MUST be created per existing backup behavior

#### Scenario: Role with neither key is denied on both
- GIVEN a custom role granted neither `Admin.Backup.Create` nor `Admin.Backup.Restore`
- WHEN a user with that role calls `TriggerManualBackupUseCase`
- THEN the use case MUST return an authorization error
- WHEN the same user calls `RestoreBackupUseCase`
- THEN the use case MUST also return an authorization error

### Requirement: Users and Roles module gating

`Users.View`, `Users.Create`, `Users.Edit`, and `Users.Deactivate` MUST gate their respective use cases (`GetAllUsersUseCase`, `CreateUserUseCase`, `UpdateUserUseCase`, `DeactivateUserUseCase`). `Roles.View`, `Roles.Create`, `Roles.Edit`, `Roles.Deactivate`, and `Roles.AssignPermissions` MUST gate the equivalent role-management use cases.

#### Scenario: User without Users.Create is denied creating a user
- GIVEN a requesting user whose role lacks `Users.Create`
- WHEN `CreateUserUseCase` is invoked
- THEN the use case MUST return an authorization error
- AND no `User` row MUST be persisted

#### Scenario: User without Roles.AssignPermissions is denied editing role grants
- GIVEN a requesting user whose role lacks `Roles.AssignPermissions`
- WHEN the caller attempts to add or remove a `RolePermission` grant on any role via `UpdateRoleUseCase`
- THEN the use case MUST return an authorization error
- AND no `RolePermission` row MUST change

### Requirement: Self-lockout guardrails

The system MUST prevent any operation that would leave zero active users holding the Administrator role, and MUST prevent stripping permissions from the system Administrator role.

#### Scenario: Deny deactivating the last active administrator
- GIVEN exactly one active user holds the Administrator role
- WHEN `DeactivateUserUseCase` is called for that user
- THEN the use case MUST return an error and MUST NOT deactivate the user

#### Scenario: Allow deactivating an administrator when another remains active
- GIVEN two active users hold the Administrator role
- WHEN `DeactivateUserUseCase` is called for one of them
- THEN the use case MUST succeed
- AND the deactivated user's `IsActive` MUST become `false`

#### Scenario: Deny reassigning the last active administrator's role away from Administrator
- GIVEN exactly one active user holds the Administrator role
- WHEN `UpdateUserUseCase` attempts to change that user's `RoleId` to a non-Administrator role
- THEN the use case MUST return an error
- AND the user's `RoleId` MUST remain the Administrator role

#### Scenario: Deny stripping permissions from the system Administrator role
- GIVEN the system Administrator role (bypass-based, with no explicit `RolePermission` rows)
- WHEN `UpdateRoleUseCase` is called attempting to modify its permission grants in any way
- THEN the use case MUST return an error
- AND the Administrator role's grant state MUST remain unchanged

### Requirement: Permission catalog is code-owned in the Domain layer

`PermissionKeys` MUST be a static class in `POS.Domain` containing const string keys grouped by module, with a means to enumerate the full catalog for seeding and for the permission-assignment UI. The catalog MUST be referenceable from Application, Infrastructure, and Presentation without introducing a dependency from Domain outward.

#### Scenario: Full catalog is enumerable
- GIVEN the `PermissionKeys` catalog
- WHEN the catalog is enumerated
- THEN every permission key used by any gated use case or by any module's `.View` visibility check MUST be present in the enumeration

### Requirement: Deferred-module permission keys exist but are not enforced in Application use cases

Permission keys for deferred modules (Products, Categories, Inventory, Quotes, Suppliers, Purchases, Customers, Receivables, Reports, CashRegister, Shifts, Loyalty, Barcode, Documents, and non-Cancel Invoices actions) MUST exist in the `PermissionKeys` catalog and MUST be assignable to roles. Their `.View` keys MUST drive `MainShellViewModel` module visibility. Application-layer enforcement inside the use cases of these deferred modules is explicitly OUT of scope for this change and MUST NOT be treated as a regression or a verification failure against this spec.

#### Scenario: Deferred module key exists in the catalog and is assignable
- GIVEN the `PermissionKeys` catalog
- WHEN `Products.View` is looked up
- THEN it MUST be present in the catalog
- AND a role MUST be able to have a `RolePermission` row granting it

#### Scenario: Deferred module use case has no Application-layer gate in this change
- GIVEN a role without `Products.Create`
- WHEN a user with that role calls `CreateProductUseCase`
- THEN this change does NOT require the use case to reject the call on authorization grounds
- AND this absence of gating MUST NOT be reported as a spec violation by verification of this change
</Delta for role-permissions>
