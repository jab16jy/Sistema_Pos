# Design: custom-role-permissions

**Change**: custom-role-permissions
**Phase**: design
**Store**: openspec
**Date**: 2026-07-12
**Inputs**: `proposal.md` (decision contract), `exploration.md` (current-state paths + Guid gotcha), `specs/role-permissions/spec.md`

This design turns the proposal's locked decisions into buildable architecture: exact class/method
signatures, file paths, EF configuration, DI registration, seeding order, and the dependency
sequencing the tasks phase needs. It does not re-open any proposal decision.

---

## 0. Architecture approach (one paragraph)

Keep the existing Clean Architecture layering and the existing "Application-layer static authorization
helper" pattern — this change **replaces** `AdminUserAuthorization` with `PermissionAuthorization`, it
does not invent a new mechanism. `Role`/`RolePermission` are ordinary Domain entities beside `User`;
`PermissionKeys` is a Domain-owned static catalog (no DB table). The session carries an eager
`IReadOnlySet<string>` snapshot populated at login (the "effective on next login" mechanism). The
Administrator role is a code-level bypass — no rows, always-allow — which is represented in-session as a
single sentinel so both the fast (session) path and the slow (repository) path agree. No new patterns,
no speculative abstraction: one repository interface (`IRoleRepository`), one gate helper, four role
use cases mirroring the existing user use cases.

---

## 1. Domain layer

### 1.1 `Role` entity — `src/POS.Domain/Entities/Role.cs`

Mirrors `User.cs` style: sealed, private parameterless ctor for EF, public ctor with guard clauses,
private setters, behavior methods for mutation. Invariants that are *entity-local* (name required,
system flag immutable) live in the entity; *cross-entity* invariants (last-admin, role-in-use) live in
use cases per §7.

```csharp
namespace POS.Domain.Entities;

public sealed class Role
{
    private Role() { Name = string.Empty; }

    // Normal constructor for user-created roles (IsSystem = false).
    public Role(string name)
    {
        Id = Guid.NewGuid();
        Name = NormalizeName(name);
        IsSystem = false;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // Seeding constructor for the two system roles (deterministic Id so seeding is idempotent
    // and the admin user's RoleId FK is stable across runs).
    public static Role CreateSystemRole(Guid id, string name)
    {
        var role = new Role
        {
            Id = id,
            Name = NormalizeName(name),
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        role.UpdatedAt = role.CreatedAt;
        return role;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Rename(string newName)
    {
        Name = NormalizeName(newName);
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string NormalizeName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Role name is required.", nameof(name))
            : name.Trim();
}
```

> Decision: uniqueness of `Name` is enforced by a DB unique index (§3) plus a use-case pre-check for a
> friendly message (§8), not inside the entity (the entity cannot see other rows). The entity only
> guarantees non-empty/trimmed.

> Decision: `RolePermission` grants are **not** a navigation collection on `Role`. They are managed
> directly through `IRoleRepository` (add/remove rows). Rationale: the assignment surface replaces the
> whole grant set at once (§8), and a tracked owned collection would fight EF change-tracking on a
> replace-all operation. Keeping grants as a separate queryable set is simpler and matches how the
> gate and seeding consume them.

### 1.2 `RolePermission` entity — `src/POS.Domain/Entities/RolePermission.cs`

Join entity, composite key `(RoleId, PermissionKey)`. No surrogate Id.

```csharp
namespace POS.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission() { PermissionKey = string.Empty; }

    public RolePermission(Guid roleId, string permissionKey)
    {
        if (roleId == Guid.Empty) throw new ArgumentException("Role id is required.", nameof(roleId));
        RoleId = roleId;
        PermissionKey = string.IsNullOrWhiteSpace(permissionKey)
            ? throw new ArgumentException("Permission key is required.", nameof(permissionKey))
            : permissionKey.Trim();
    }

    public Guid RoleId { get; private set; }
    public string PermissionKey { get; private set; }
}
```

### 1.3 `PermissionKeys` catalog — `src/POS.Domain/Authorization/PermissionKeys.cs`

**Decision: nested static classes per module** (readable call sites like `PermissionKeys.Admin.Backup.Restore`),
plus a flat `All` enumerable for seeding and the assignment UI. The nested-const style is what the
proposal's examples already use (`PermissionKeys.Admin.Backup.Restore`, `PermissionKeys.Invoices.Cancel`).

`All` is built once via reflection over the const string fields (zero maintenance — new consts are
auto-included, satisfying spec "Full catalog is enumerable"). Each module also exposes its own keys so
the assignment UI can group by module without re-parsing key prefixes.

```csharp
namespace POS.Domain.Authorization;

public static class PermissionKeys
{
    // ---- v1-ENFORCED modules (Application-layer gate added in THIS change) ----
    public static class Users
    {
        public const string View = "Users.View";
        public const string Create = "Users.Create";
        public const string Edit = "Users.Edit";
        public const string Deactivate = "Users.Deactivate";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Deactivate = "Roles.Deactivate";
        public const string AssignPermissions = "Roles.AssignPermissions";
    }

    public static class Audit
    {
        public const string View = "Audit.View";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Edit = "Settings.Edit";
    }

    public static class Admin
    {
        public static class Backup
        {
            public const string View = "Admin.Backup.View";
            public const string Create = "Admin.Backup.Create";
            public const string Restore = "Admin.Backup.Restore";
        }
    }

    public static class Invoices  // Invoices.Cancel is v1-enforced; the rest are catalog-only for now
    {
        public const string View = "Invoices.View";        // catalog-only (drives shell visibility)
        public const string Reprint = "Invoices.Reprint";  // catalog-only
        public const string Return = "Invoices.Return";    // catalog-only
        public const string Cancel = "Invoices.Cancel";    // v1-ENFORCED (CancelSaleUseCase)
    }

    // ---- CATALOG-ONLY modules (keys exist, drive shell .View visibility, roles can grant them,
    //      but Application-layer enforcement is DEFERRED per proposal §2 cut line) ----
    public static class Cashier
    {
        public const string Sell = "Cashier.Sell"; // baseline; both system roles have it
    }

    public static class Products
    {
        public const string View = "Products.View";
        public const string Create = "Products.Create";
        public const string Edit = "Products.Edit";
        public const string Delete = "Products.Delete";
        public const string Import = "Products.Import";
    }

    public static class Categories
    {
        public const string View = "Categories.View";
        public const string Create = "Categories.Create";
        public const string Edit = "Categories.Edit";
        public const string Delete = "Categories.Delete";
    }

    public static class Inventory
    {
        public const string View = "Inventory.View";
        public const string Adjust = "Inventory.Adjust";
    }

    public static class Quotes
    {
        public const string View = "Quotes.View";
        public const string Create = "Quotes.Create";
        public const string Convert = "Quotes.Convert";
    }

    public static class Suppliers
    {
        public const string View = "Suppliers.View";
        public const string Create = "Suppliers.Create";
        public const string Edit = "Suppliers.Edit";
        public const string Delete = "Suppliers.Delete";
    }

    public static class Purchases
    {
        public const string View = "Purchases.View";
        public const string Create = "Purchases.Create";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string Create = "Customers.Create";
        public const string Edit = "Customers.Edit";
    }

    public static class Receivables
    {
        public const string View = "Receivables.View";
        public const string ApplyPayment = "Receivables.ApplyPayment";
    }

    public static class Reports
    {
        public const string View = "Reports.View";
    }

    public static class CashRegister
    {
        public const string Close = "CashRegister.Close";
    }

    public static class Shifts  // owner-vs-all scope deferred to a follow-up change (proposal §2)
    {
        public const string View = "Shifts.View";
        public const string Open = "Shifts.Open";
        public const string Close = "Shifts.Close";
    }

    public static class Loyalty
    {
        public const string View = "Loyalty.View";
        public const string Redeem = "Loyalty.Redeem";
        public const string ConfigureRules = "Loyalty.ConfigureRules";
        public const string ManageTiers = "Loyalty.ManageTiers";
    }

    public static class Barcode
    {
        public const string View = "Barcode.View";
        public const string Print = "Barcode.Print";
    }

    public static class Documents
    {
        public const string Send = "Documents.Send";
    }

    /// <summary>Every const string key across all nested modules. Built once via reflection.</summary>
    public static IReadOnlyList<string> All { get; } = BuildAll();

    private static IReadOnlyList<string> BuildAll()
    {
        var keys = new List<string>();
        void Collect(Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                                       .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                keys.Add((string)field.GetRawConstantValue()!);
            foreach (var nested in type.GetNestedTypes())
                Collect(nested);
        }
        Collect(typeof(PermissionKeys));
        return keys.Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }
}
```

> `using System.Reflection;` + `System.Linq` at the top. `BuildAll` runs once (static init). ~66 keys —
> trivial cost.

### 1.4 `UserRole` enum deletion — timing

**Decision: delete `src/POS.Domain/Enums/UserRole.cs` in the SAME slice as the `User` entity swap.**
The enum and `User.RoleId` cannot coexist compiling (every `UserRole` reference must migrate at once —
the ~64-file blast radius). Slice 1 (per proposal §Review Workload) does: add `Role`/`RolePermission`/
catalog → swap `User.Role`→`User.RoleId` → delete enum → fix all references (session, DTOs, VMs
temporarily, seeding) → migration. The enum's `.cs` file is deleted at the end of slice 1, not later.

### 1.5 `User` entity change — `src/POS.Domain/Entities/User.cs`

```csharp
// remove: using POS.Domain.Enums;
public User(string username, string passwordHash, Guid roleId)  // was UserRole role
{
    Id = Guid.NewGuid();
    Username = ...;             // unchanged
    PasswordHash = ...;          // unchanged
    RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;
    IsActive = true;
    CreatedAt = DateTimeOffset.UtcNow;
}

public Guid RoleId { get; private set; }        // was: public UserRole Role
public void ChangeRole(Guid roleId) => RoleId = roleId;   // was: UpdateRole(UserRole)
```

No navigation property on `User` (proposal says "navigation optional at design's discretion" — skip it,
YAGNI: the gate and VMs resolve role via `IRoleRepository`, not lazy nav). `ponytail:` no nav until a
query needs a join.

---

## 2. `PermissionAuthorization` — `src/POS.Application/UseCases/Auth/PermissionAuthorization.cs`

Replaces `AdminUserAuthorization`. Same static-helper shape, same "return error string or null" contract
(so call sites that do `if (error is not null) return (false, error);` change only the method name +
key argument). Two overloads: a **session fast-path** and a **repository fallback** for arbitrary user id.

```csharp
using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

internal static class PermissionAuthorization
{
    // FAST PATH — requester is the current operator. No DB round-trip; consults the session snapshot.
    public static string? EnsureHasPermission(
        ICurrentOperatorSession session,
        string permissionKey,
        string errorMessage)
        => session.IsAuthenticated && session.HasPermission(permissionKey) ? null : errorMessage;

    // FALLBACK PATH — must authorize an explicit user id that may differ from the session operator.
    // Mirrors AdminUserAuthorization's async signature so gated use cases that already take a
    // requestingUserId (users, cancel, audit) migrate with a one-line change.
    public static async Task<string?> EnsureHasPermissionAsync(
        IUserRepository users,
        IRoleRepository roles,
        Guid requestingUserId,
        string permissionKey,
        string errorMessage,
        CancellationToken ct)
    {
        if (requestingUserId == Guid.Empty) return errorMessage;

        var requester = await users.GetByIdAsync(requestingUserId, ct).ConfigureAwait(false);
        if (requester is not { IsActive: true }) return errorMessage;

        var role = await roles.GetByIdAsync(requester.RoleId, ct).ConfigureAwait(false);
        if (role is not { IsActive: true }) return errorMessage;

        // Administrator bypass — system admin always passes, no key lookup (proposal §4 step 3).
        if (RoleConstants.IsSystemAdministrator(role.Id)) return null;

        bool granted = await roles.HasPermissionAsync(role.Id, permissionKey, ct).ConfigureAwait(false);
        return granted ? null : errorMessage;
    }
}
```

**Which path each gate uses:**

| Use case | Path | Reason |
|---|---|---|
| `CreateUserUseCase` | `Async` | already takes `requestingUserId` |
| `UpdateUserUseCase` | `Async` | already takes `requestingUserId` |
| `DeactivateUserUseCase` | `Async` | already takes `requestingUserId` |
| `GetAuditLogsUseCase` | `Async` | already takes `requestingUserId` |
| `CancelSaleUseCase` | `Async` | authorizes `request.CancelledByUserId` (may differ from session) |
| `TriggerManualBackupUseCase` | fast (session) | already injects `ICurrentOperatorSession`; operator == requester |
| `RestoreBackupUseCase` | fast (session) | inject `ICurrentOperatorSession` (new dep) |
| `SaveSettingsUseCase` | fast (session) | inject `ICurrentOperatorSession` (new dep) |
| `LoadSettingsUseCase` | fast (session) | inject `ICurrentOperatorSession` (new dep) |
| `UpdateBackupDirectoryUseCase` | fast (session) | inject `ICurrentOperatorSession` (new dep) |
| new role use cases | `Async` | see §8 (they take `requestingUserId` for symmetry with user use cases) |

> `RoleConstants.IsSystemAdministrator(Guid)` — a shared const holding the seeded Administrator role's
> deterministic Guid. Placed in `POS.Domain/Authorization/RoleConstants.cs` so both the gate (Application)
> and seeding (Infrastructure) reference the same id. See §6.

### 2.1 Exact call-site replacements

Every current `AdminUserAuthorization.EnsureAdministratorAsync(...)` call → `PermissionAuthorization.EnsureHasPermissionAsync(_users, _roles, requestingUserId, <KEY>, <msg>, ct)`:

| Call site (file) | Old | New key |
|---|---|---|
| `CreateUserUseCase.cs` | `EnsureAdministratorAsync(..., "Solo un administrador puede crear usuarios.", ct)` | `PermissionKeys.Users.Create` |
| `UpdateUserUseCase.cs` | `EnsureAdministratorAsync(..., "Solo un administrador puede actualizar usuarios.", ct)` | `PermissionKeys.Users.Edit` |
| `DeactivateUserUseCase.cs` | `EnsureAdministratorAsync(..., "Solo un administrador puede desactivar usuarios.", ct)` | `PermissionKeys.Users.Deactivate` |
| `GetAuditLogsUseCase.cs` | `EnsureAdministratorAsync(..., "Solo un administrador puede consultar la auditoría.", ct)` | `PermissionKeys.Audit.View` |
| `CancelSaleUseCase.cs` | inline `if (user.Role != UserRole.Administrator) throw ...` (lines 47-50) | `PermissionKeys.Invoices.Cancel` (throw `UnauthorizedActionException("CancelSale")` when non-null) |

New gates (no prior check — add at top of `ExecuteAsync`, session fast-path):

| File | Key | Denial shape |
|---|---|---|
| `TriggerManualBackupUseCase.cs` | `PermissionKeys.Admin.Backup.Create` | return `new ManualBackupResult(false, <msg>, null)` |
| `RestoreBackupUseCase.cs` | `PermissionKeys.Admin.Backup.Restore` | return `new RestoreBackupResult(false, <msg>)` |
| `SaveSettingsUseCase.cs` | `PermissionKeys.Settings.Edit` | return a `SettingsResult` error variant OR throw `UnauthorizedActionException("SaveSettings")` — see note |
| `LoadSettingsUseCase.cs` | `PermissionKeys.Settings.View` | returns `SettingsResult?`; on deny throw `UnauthorizedActionException("LoadSettings")` |
| `UpdateBackupDirectoryUseCase.cs` | `PermissionKeys.Settings.Edit` | returns `bool`; on deny throw `UnauthorizedActionException("UpdateBackupDirectory")` |

> **Note on Settings denial shape**: `SettingsResult` has no natural error string field, and `LoadSettings`/
> `UpdateBackupDirectory` return `SettingsResult?`/`bool`. **Decision: settings gates throw
> `UnauthorizedActionException` on deny** (matching `CancelSaleUseCase`/`GetAuditLogsUseCase` which already
> throw it), rather than inventing error-carrying result variants. Backup use cases keep their existing
> result-object error channel (they already have a `(bool, string?)`-shaped result), so their denial is a
> returned error, not a throw. This keeps each use case's existing return contract intact.

---

## 3. EF Core

### 3.1 `RoleConfiguration.cs` — `src/POS.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`

```csharp
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion<string>();   // MANDATORY — lowercase TEXT Guid
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(r => r.Name).IsUnique();
    }
}
```

### 3.2 `RolePermissionConfiguration.cs`

```csharp
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionKey });      // composite key
        builder.Property(rp => rp.RoleId).HasConversion<string>();      // MANDATORY
        builder.Property(rp => rp.PermissionKey).IsRequired().HasMaxLength(100);
        builder.HasOne<Role>().WithMany().HasForeignKey(rp => rp.RoleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 3.3 `User.RoleId` config — extend `UserConfiguration.cs`

```csharp
builder.Property(u => u.Id).HasConversion<string>();       // existing
builder.Property(u => u.RoleId).HasConversion<string>();   // NEW — same lowercase-TEXT convention
builder.HasOne<Role>().WithMany().HasForeignKey(u => u.RoleId)
       .OnDelete(DeleteBehavior.Restrict);                 // can't delete a role that has users
```

Configs are auto-discovered via `ApplyConfigurationsFromAssembly` — no manual registration.

### 3.4 DbContext DbSet additions — `AppDbContext.cs` (after line 27, `Users`)

```csharp
public DbSet<Role> Roles => Set<Role>();
public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
```

### 3.5 Migration — `{ts}_ReplaceUserRoleEnumWithRbac.cs` (+ `.Designer.cs`)

Follows the exact format of `20260710064129_AddBackupMetadata.cs` (`[DbContext(typeof(AppDbContext))]`,
`[Migration("{ts}_ReplaceUserRoleEnumWithRbac")]`, `#nullable disable`). Generated via
`dotnet ef migrations add ReplaceUserRoleEnumWithRbac` after entities/configs compile — do NOT hand-write
the Designer/snapshot; regenerate. The `Up`/`Down` bodies below describe what the generated migration must contain.

**Up:**
1. `CreateTable("Roles")`: `Id` TEXT PK, `Name` TEXT (unique index), `IsSystem` INTEGER, `IsActive` INTEGER, `CreatedAt` TEXT, `UpdatedAt` TEXT.
2. `CreateTable("RolePermissions")`: `RoleId` TEXT, `PermissionKey` TEXT, composite PK `(RoleId, PermissionKey)`, FK → `Roles.Id` cascade.
3. `DropColumn("Role", "Users")` — the int enum column (currently `Role = table.Column<int>(...)` per `InitialCreate.cs` line 290).
4. `AddColumn<string>("RoleId", "Users", nullable: false, defaultValue: "")` — TEXT (lowercase-Guid). Clean-slate dev data → default empty is fine; seeding repopulates. Add FK `Users.RoleId → Roles.Id` restrict.
5. `CreateIndex` unique on `Roles.Name`; index on `Users.RoleId`.

**Down:** reverse — drop `Users.RoleId` (+FK/index), re-add `Users.Role` INTEGER not-null default 0, drop `RolePermissions`, drop `Roles`.

> No data-preservation step (proposal §8: clean-slate, dev data dropped/recreated). The seeding in §6
> runs after `MigrateAsync`, so a freshly-created DB gets both system roles and an admin pointing at
> the Administrator role.

---

## 4. Repositories

**Decision: one new interface `IRoleRepository` (Domain) + `RoleRepository` (Infrastructure).**
`IUserRepository` is NOT extended for role/permission lookups — role data is a different aggregate.
`IUserRepository` only gains what user-guardrails need: an active-admin count (§7).

### 4.1 `IRoleRepository` — `src/POS.Domain/Interfaces/IRoleRepository.cs`

```csharp
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);  // uniqueness pre-check
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);     // GetAllRoles use case + VM
    Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default); // gate fallback
    Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default);   // login snapshot + assignment UI load
    Task AddAsync(Role role, CancellationToken ct = default);
    Task UpdateAsync(Role role, CancellationToken ct = default);              // rename / deactivate
    Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default); // assignment (delete-all + add)
    Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default); // role-in-use guard (§7)
}
```

`ReplacePermissionsAsync` implementation: `DELETE FROM RolePermissions WHERE RoleId = @id` (or remove
tracked rows), then add the new set. It does NOT `SaveChanges` — the use case owns the `IUnitOfWork.SaveChangesAsync`
so rename + permission-replace commit atomically.

### 4.2 `IUserRepository` addition — `src/POS.Domain/Interfaces/IUserRepository.cs`

```csharp
Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default); // last-admin guard (§7)
```

`RoleRepository` follows the existing `UserRepository` pattern (constructor-injected `AppDbContext`,
async EF queries). Registered in DI (§10).

---

## 5. Session / login integration

### 5.1 `ICurrentOperatorSession` — `src/POS.Application/Interfaces/ICurrentOperatorSession.cs`

```csharp
// remove: using POS.Domain.Enums;
public interface ICurrentOperatorSession
{
    Guid OperatorId { get; }
    string Username { get; }
    Guid RoleId { get; }                          // replaces UserRole Role
    string RoleName { get; }                      // for the shell display name
    IReadOnlySet<string> Permissions { get; }     // eager snapshot
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionKey);
}
```

### 5.2 Administrator "all permissions" in the session — **decision**

**Materialize the full catalog into the admin session's permission set at login**, AND keep the
repository-path bypass (§2). Rationale:

- The proposal says the *persisted* admin role has **no rows** (bypass by construction — can't be
  stripped). That is about persistence, not the session set.
- For the *in-session* representation, the simplest correct thing is: at login, if the user's role is the
  system Administrator, set `Permissions = new HashSet<string>(PermissionKeys.All)`. Then `HasPermission`
  is the same plain set lookup for everyone — no admin special-casing in the session object, no sentinel
  string, no "always true" branch that could mask a bug. The shell's per-module visibility (§9) then
  "just works" for admin because every `.View` key is present.
- New catalog keys added later are auto-covered because `PermissionKeys.All` is reflection-built.

So there are two independent admin bypasses, each in its own layer, and they agree:
- **Session/fast-path**: admin's snapshot literally contains every key (materialized at login).
- **Repository/fallback-path**: `RoleConstants.IsSystemAdministrator(role.Id)` short-circuits before any
  row lookup (needed because the persisted admin role has no rows to check).

`ponytail:` no sentinel, no "is-admin" flag on the session — a fully-populated set is the least code and
the least surprising. Cost is one ~66-entry HashSet per admin login. Negligible.

### 5.3 `InMemoryUserSession` — `src/POS.Infrastructure/Persistence/InMemoryUserSession.cs`

```csharp
// remove: using POS.Domain.Enums;
public sealed class InMemoryUserSession : IWritableOperatorSession
{
    private Guid _operatorId = Guid.Empty;
    private string _username = string.Empty;
    private Guid _roleId = Guid.Empty;
    private string _roleName = string.Empty;
    private IReadOnlySet<string> _permissions = new HashSet<string>();  // empty when unauthenticated

    public Guid OperatorId => _operatorId;
    public string Username => _username;
    public Guid RoleId => _roleId;
    public string RoleName => _roleName;
    public IReadOnlySet<string> Permissions => _permissions;
    public bool IsAuthenticated => _operatorId != Guid.Empty;
    public bool HasPermission(string permissionKey) => _permissions.Contains(permissionKey);

    public void SetUser(Guid id, string username, Guid roleId, string roleName, IReadOnlySet<string> permissions)
    {
        _operatorId = id;
        _username = username;
        _roleId = roleId;
        _roleName = roleName;
        _permissions = permissions ?? new HashSet<string>();
    }

    public void ClearSession()
    {
        _operatorId = Guid.Empty;
        _username = string.Empty;
        _roleId = Guid.Empty;
        _roleName = string.Empty;
        _permissions = new HashSet<string>();
    }
}
```

`IWritableOperatorSession.SetUser` signature changes accordingly (interface lives in `POS.Application/Interfaces`).
**Decision: default to empty set when unauthenticated** (not "Cashier") — an unauthenticated session
grants nothing, which is the safe default and matches the proposal (§5).

### 5.4 `LoginResult` DTO — `src/POS.Application/DTOs/Auth/AuthDtos.cs`

```csharp
// remove: using POS.Domain.Enums;
public record LoginResult(
    bool Success, string? ErrorMessage,
    Guid UserId = default, string Username = "",
    Guid RoleId = default, string RoleName = "",
    IReadOnlySet<string>? Permissions = null);
```

**Decision:** `LoginResult` carries the snapshot too, so `AppBootstrapper` can populate both the session
(via `IWritableOperatorSession`) and `MainShellViewModel` from one result. `Permissions` nullable so the
failure ctor (`new LoginResult(false, msg)`) stays valid.

### 5.5 `LoginUseCase` — populate the snapshot

Add `IRoleRepository _roles` dependency. After credentials validate (line ~36):

```csharp
var role = await _roles.GetByIdAsync(user.RoleId, ct).ConfigureAwait(false)
    ?? throw new InvalidOperationException("User's role no longer exists.");

IReadOnlySet<string> permissions = RoleConstants.IsSystemAdministrator(role.Id)
    ? new HashSet<string>(PermissionKeys.All)                       // admin: full catalog materialized
    : await _roles.GetPermissionKeysAsync(role.Id, ct).ConfigureAwait(false);

// audit write unchanged ...
return new LoginResult(true, null, user.Id, user.Username, role.Id, role.Name, permissions);
```

> `AppBootstrapper` writes the snapshot into the session. Currently `AppBootstrapper` does the login and
> reads `session.Role` (lines 42, 207, 267). Those change to read `result.RoleName` / `result.Permissions`
> and call the new `SetUser(...)`. `MainShellViewModel` ctor changes from `(cashier, dashboard, UserRole, username)`
> to `(cashier, dashboard, IReadOnlySet<string> permissions, string roleName, string username)` — see §9.

---

## 6. Seeding — `DatabaseSchemaRollout.cs`

### 6.1 `RoleConstants` — `src/POS.Domain/Authorization/RoleConstants.cs`

Deterministic Guids so seeding is idempotent and the FK is stable:

```csharp
public static class RoleConstants
{
    public static readonly Guid AdministratorRoleId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid CashierRoleId       = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
    public const string AdministratorRoleName = "Administrator";
    public const string CashierRoleName = "Cashier";
    public static bool IsSystemAdministrator(Guid roleId) => roleId == AdministratorRoleId;
}
```

### 6.2 Seeding order (roles BEFORE admin user)

Rename `SeedDefaultAdminAsync` → `SeedDefaultRolesAndAdminAsync`, called at the same spot (line 21).
Order of operations inside:

1. If `Roles` empty → add both system roles via `Role.CreateSystemRole(RoleConstants.AdministratorRoleId, "Administrator")`
   and `Role.CreateSystemRole(RoleConstants.CashierRoleId, "Cashier")`. `SaveChanges`.
2. Seed Cashier's `RolePermission` rows (the explicit default grant list, §6.3). Administrator gets **no
   rows** (bypass). `SaveChanges`.
3. If `Users` empty → create the `admin` user with `new User("admin", hash, RoleConstants.AdministratorRoleId)`.
   `SaveChanges`.

Guard each step with its own `AnyAsync` so re-running is idempotent (spec "Seeding is idempotent").

### 6.3 Cashier default permission set — **exact list** (derived from "keep current effective capabilities")

**Correct premise (verified against `MainShellViewModel.cs`):** a Cashier session today can ONLY reach the
modules in `CashierFuncbarKeys = [cashier, customers, quotes, invoices, shifts]`. Every admin-hub module
(`AdminHubModuleKeys = { products, inventory, suppliers, reports, shifts, loyalty, backup }` plus
`users`, `audit`, `settings`) is populated in the shell ONLY when `isAdmin` is true and is an empty list
otherwise — so Cashier cannot reach Products, Categories, Inventory, Suppliers, Purchases, Reports,
Loyalty, Barcode, CashRegister, Documents, Settings, Backup, Users, or Audit at all. `shifts` appears in
BOTH sets: it is in the cashier funcbar (a cashier opens/closes their own shift) AND in the admin hub.

So Cashier's *effective current capability* = only the operations under those five funcbar modules. The
default grant list must match that surface exactly — granting the admin-hub `.View` keys would EXPAND
Cashier via the new `.View`-driven visibility mechanism (§9.1), violating "keep current capabilities."
Seed exactly these `RolePermission` rows for the Cashier role (nothing else):

```
Cashier.Sell                                              (the "cashier" POS/sell funcbar module)
Customers.View, Customers.Create, Customers.Edit          (the "customers" funcbar module)
Quotes.View, Quotes.Create, Quotes.Convert                (the "quotes" funcbar module)
Invoices.View, Invoices.Reprint, Invoices.Return          (the "invoices" funcbar module — NOT Invoices.Cancel, admin-only today)
Shifts.View, Shifts.Open, Shifts.Close                    (the "shifts" funcbar module; owner-vs-all scope deferred)
```

**Explicitly EXCLUDED from Cashier** (unreachable by Cashier through the shell today — admin-hub-gated or
admin-role-gated — so NOT in the default grant; they stay ungranted by default and remain assignable to
custom roles by an admin):
```
Products.*  Categories.*  Inventory.*  Suppliers.*  Purchases.*  Receivables.*
Reports.*  CashRegister.Close  Loyalty.*  Barcode.*  Documents.Send
Users.*  Roles.*  Audit.View  Admin.Backup.*  Settings.*  Invoices.Cancel
```

> This list is now consistent with §9.1's `ModuleViewKeys`: every admin-hub-gated `.View` key
> (`Products.View`, `Inventory.View`, `Suppliers.View`, `Reports.View`, `Loyalty.View`, `Admin.Backup.View`,
> `Users.View`, `Audit.View`, `Settings.View`, `Roles.View`) is absent from the Cashier grant, so under the
> new mechanism a Cashier still sees only the five funcbar modules — identical to today. `Shifts.View` is
> the one admin-hub key Cashier keeps, because `shifts` is also a cashier funcbar module today (a cashier
> already reaches Turnos directly). `ponytail:` the smaller grant is both correct and the safe default —
> anything a cashier legitimately needs beyond this can be added by granting keys to a custom role.

Provide the Cashier default list as a static array in `PermissionKeys` (e.g. `PermissionKeys.CashierDefaults`)
so seeding references it by name and a test can assert it, rather than duplicating the literal list in
`DatabaseSchemaRollout`.

---

## 7. Guardrails (self-lockout & immutability)

All at use-case layer, returning the existing `(bool Success, string? Error)` tuple for user/role use
cases (matching `DeactivateUserUseCase`). Error messages in neutral Spanish (matching existing messages).

| Proposal §6 row | Use case | Logic | Message |
|---|---|---|---|
| Deactivate last active admin | `DeactivateUserUseCase` | after auth + user-found: if `user.RoleId == AdministratorRoleId` AND `await _users.CountActiveUsersInRoleAsync(AdministratorRoleId) <= 1` → deny | "No puedes desactivar al último administrador activo." |
| Reassign last admin away | `UpdateUserUseCase` | if `user.RoleId == AdministratorRoleId` AND `newRoleId != AdministratorRoleId` AND `CountActiveUsersInRoleAsync(AdministratorRoleId) <= 1` → deny | "No puedes quitar el rol de administrador al último administrador activo." |
| Deactivate system role | `DeactivateRoleUseCase` | if `role.IsSystem` → deny | "Los roles del sistema no se pueden desactivar." |
| Deactivate role in use | `DeactivateRoleUseCase` | if `await _roles.CountUsersInRoleAsync(roleId) > 0` → deny | "No puedes desactivar un rol asignado a usuarios." |
| Strip admin permissions | `UpdateRoleUseCase` | if `RoleConstants.IsSystemAdministrator(roleId)` AND the request touches permission grants → deny | "Los permisos del rol Administrador no se pueden modificar." |
| Delete a role | — | no `DeleteRoleUseCase` exists | n/a |

> `UpdateUserUseCase` gains a `Guid newRoleId` param (replacing `UserRole newRole`) — the last-admin
> reassignment guard needs the target role id.
> Counts use `<= 1` (not `== 1`) defensively. The guard runs BEFORE mutating the entity.

---

## 8. New role use cases — `src/POS.Application/UseCases/Roles/`

All take `Guid requestingUserId` + gate via `PermissionAuthorization.EnsureHasPermissionAsync`
(async path, `_users` + `_roles`). Return `(bool Success, string? Error)` (or a result DTO for reads),
matching the user use cases. Each owns `IUnitOfWork.SaveChangesAsync` where it mutates.

### 8.1 `GetAllRolesUseCase`
```csharp
public sealed record RoleSummary(Guid Id, string Name, bool IsSystem, bool IsActive);
// gate: Roles.View
public Task<IReadOnlyList<RoleSummary>> ExecuteAsync(Guid requestingUserId, CancellationToken ct = default);
```
Used by `UserManagementViewModel.AvailableRoles` (live query) and `RolesManagementViewModel`.

### 8.2 `CreateRoleUseCase`
```csharp
// gate: Roles.Create
public Task<(bool Success, string? Error)> ExecuteAsync(
    string name, IReadOnlySet<string> permissionKeys, Guid requestingUserId, CancellationToken ct = default);
```
Validates: name non-empty, name not already taken (`_roles.GetByNameAsync`), every key ∈ `PermissionKeys.All`
(reject unknown keys — inert data guard). Creates `Role`, then `ReplacePermissionsAsync(role.Id, keys)`,
one `SaveChanges`.

### 8.3 `UpdateRoleUseCase` — **decision: single use case does BOTH rename and permission-set replacement**
```csharp
// gate: Roles.Edit for rename; additionally requires Roles.AssignPermissions if permissionKeys is provided
public Task<(bool Success, string? Error)> ExecuteAsync(
    Guid roleId, string newName, IReadOnlySet<string> permissionKeys, Guid requestingUserId, CancellationToken ct = default);
```
Rationale: the assignment UI edits a role's name and its grant set on one screen and saves once —
splitting into two use cases would need two round-trips and two `SaveChanges` for one user action.
Logic order:
1. Gate `Roles.Edit`. If `permissionKeys` differs from current grants, ALSO gate `Roles.AssignPermissions`
   (spec: "without `Roles.AssignPermissions` is denied editing role grants").
2. Load role; if not found → error.
3. **Admin-strip guard** (§7): if `IsSystemAdministrator(roleId)` and `permissionKeys` would change the
   admin's (empty) grant set → deny. (Admin has no rows; any non-empty set or any attempt to persist
   rows for it is a modification → deny.)
4. Validate `newName` unique (excluding self) + all keys ∈ catalog.
5. `role.Rename(newName)`, `ReplacePermissionsAsync(roleId, permissionKeys)`, one `SaveChanges`.

### 8.4 `DeactivateRoleUseCase`
```csharp
// gate: Roles.Deactivate
public Task<(bool Success, string? Error)> ExecuteAsync(Guid roleId, Guid requestingUserId, CancellationToken ct = default);
```
Guards per §7 (system role, role-in-use). On pass: `role.Deactivate()`, `UpdateAsync`, `SaveChanges`.

No `DeleteRoleUseCase` — deletion is not offered (spec).

---

## 9. WPF

### 9.1 `MainShellViewModel` — per-module visibility from the permission set

**Data structure change**: `ModuleNavigationItem` gains no field; instead the ctor takes the permission
set and each admin module's inclusion is decided by its `.View` key. Replace the single `bool isAdmin`
(line 75) with a `HashSet<string>` lookup. Add a static map of `moduleKey → requiredViewKey`:

```csharp
private static readonly IReadOnlyDictionary<string, string> ModuleViewKeys = new Dictionary<string, string>
{
    ["products"]  = PermissionKeys.Products.View,
    ["inventory"] = PermissionKeys.Inventory.View,
    ["suppliers"] = PermissionKeys.Suppliers.View,
    ["reports"]   = PermissionKeys.Reports.View,
    ["shifts"]    = PermissionKeys.Shifts.View,
    ["loyalty"]   = PermissionKeys.Loyalty.View,
    ["backup"]    = PermissionKeys.Admin.Backup.View,
    ["users"]     = PermissionKeys.Users.View,
    ["audit"]     = PermissionKeys.Audit.View,
    ["settings"]  = PermissionKeys.Settings.View,
    ["roles"]     = PermissionKeys.Roles.View,   // NEW module
};
```

Ctor signature: `MainShellViewModel(CashierViewModel cashier, GetDashboardSummaryUseCase? dashboard, IReadOnlySet<string> permissions, string roleName, string username)`.
The parameterless/design-time ctors default `permissions` to `new HashSet<string>(PermissionKeys.All)`
(so the designer/preview shows everything, matching the old `UserRole.Administrator` default at line 44).

Module list building:
- A module is visible if `permissions.Contains(ModuleViewKeys[key])` (admin sees all because its snapshot
  has every key — §5.2). Cashier-funcbar operational modules (`cashier, customers, quotes, invoices`)
  stay always-visible for any authenticated user; `shifts` gated by `Shifts.View` (cashier default grants it).
- `AdminHubModules` = admin modules whose `.View` key is present (drop the `isAdmin` all-or-nothing).
  Add `"roles"` to the admin hub module set alongside `users`/`audit`.
- `Role`/`BuildDisplayName` change: display name uses `roleName` string, not the enum switch.

> The exploration flags this VM as well-tested — its tests assert the 7-module hub contract. Tests must be
> updated to drive visibility from a permission set instead of `UserRole` (see §11). Admin (full set)
> still yields the same modules, so the existing "admin sees N modules" assertions hold if the admin set
> is the full catalog.

### 9.2 `InvoicesViewModel`

Replace `CurrentUserRole`/`IsAdministrator` (lines 285-294) with a session-permission lookup:
```csharp
public bool CanCancel => _session.HasPermission(PermissionKeys.Invoices.Cancel);
```
Inject `ICurrentOperatorSession` (if not already). `CancelSaleAsync`'s `CanExecute` uses `CanCancel`.
Remove the `CurrentUserRole` setter and the `UserRole` field. `AppBootstrapper` no longer passes
`CurrentUserRole = session.Role` (line 207) — the VM reads the session directly.

### 9.3 `UserManagementViewModel`

- `IsAdmin` (line 114) → per-command permission checks:
  - `CanCreate => _session.HasPermission(PermissionKeys.Users.Create)`
  - `CanEdit   => _session.HasPermission(PermissionKeys.Users.Edit)`
  - `CanDeactivate => _session.HasPermission(PermissionKeys.Users.Deactivate)`
  Command `CanExecute` predicates use these (create uses `CanCreate`, save uses `CanEdit && SelectedUser is not null`, etc.).
  `EnsureAdmin()` helper → `EnsurePermission(key, message)` variants (or a single check per action).
- `AvailableRoles` (line 49, hardcoded `[Cashier, Administrator]`) → `ObservableCollection<RoleSummary>`
  loaded in `LoadAsync` via injected `GetAllRolesUseCase` (only active roles). `NewRole`/`EditRole`
  become `Guid` (selected `RoleId`) instead of `UserRole`.
- `CreateUserUseCase`/`UpdateUserUseCase` calls pass `RoleId` (Guid) instead of `UserRole`.

### 9.4 New `RolesManagementViewModel` + view — mirrors `UserManagementViewModel`

- **`src/POS.Presentation.Shared/ViewModels/RolesManagementViewModel.cs`**: injects
  `GetAllRolesUseCase, CreateRoleUseCase, UpdateRoleUseCase, DeactivateRoleUseCase, IRoleRepository`
  (to load a selected role's current grants for the toggle UI), `ICurrentOperatorSession`.
  - `Roles` (ObservableCollection<RoleSummary>), `SelectedRole`.
  - Permission-assignment surface: a `PermissionOptions` collection grouped by module, built from
    `PermissionKeys.All` (or per-module lists), each item a `PermissionOptionRow(string Key, string Module, bool IsGranted)`
    with a toggle. On select-role, load current grants via `IRoleRepository.GetPermissionKeysAsync` and set
    `IsGranted`. On save, gather the checked keys into a set and call `CreateRoleUseCase`/`UpdateRoleUseCase`.
  - Command gating: create→`Roles.Create`, save→`Roles.Edit`(+`Roles.AssignPermissions` when grants changed),
    deactivate→`Roles.Deactivate`, load→`Roles.View`.
  - System roles: for the Administrator row, disable the permission toggles (bypass — grants aren't editable);
    for both system roles, disable Deactivate. Surface a message when denied.
- **View**: `RolesManagementView.xaml` mirroring `UserManagementView.xaml` structure (a roles list on the
  left, an edit/permission-assignment panel on the right with a grouped, toggleable permission list). This
  is MVVM structure, not pixel design — follow the existing user view's DataTemplate/ItemsControl layout.
- **Shell wiring**: add a `"roles"` `ModuleNavigationItem` to `MainShellViewModel.allModules`, add it to the
  admin hub set (§9.1), add `RolesManagement` VM as a `MainShellViewModel` `init` property and an
  `IsRolesSelected`/`OnPropertyChanged` line paralleling `IsUsersSelected`.

---

## 10. DI registration — `src/POS.Infrastructure/DependencyInjection.cs`

Add (repository near line 73, use cases near the user-management block line 155-158):

```csharp
services.AddScoped<IRoleRepository, RoleRepository>();          // after IUserRepository (line 73)

// Roles & permissions use cases (after DeactivateUserUseCase, line 158)
services.AddScoped<GetAllRolesUseCase>();
services.AddScoped<CreateRoleUseCase>();
services.AddScoped<UpdateRoleUseCase>();
services.AddScoped<DeactivateRoleUseCase>();
```

`LoginUseCase` and the gated use cases gain constructor deps (`IRoleRepository`, `ICurrentOperatorSession`
where noted) — no DI change needed (they're already `AddScoped` and the deps are already registered).
The Presentation project's composition (wherever `UserManagementViewModel` is built) adds
`RolesManagementViewModel` construction with its injected use cases. `add-migration` `using` for the new
config namespaces is auto-handled by `ApplyConfigurationsFromAssembly`.

---

## 11. Test strategy (maps to spec scenarios — feeds tasks phase; tests not written here)

**`POS.Domain.Tests`**
- `Role`: ctor rejects empty name; `CreateSystemRole` sets `IsSystem=true` + given id; `Rename` trims/rejects empty + bumps `UpdatedAt`; `Deactivate` flips `IsActive`.
- `RolePermission`: ctor rejects empty roleId/key.
- `User`: ctor requires non-empty `RoleId`; `ChangeRole` updates it.
- `PermissionKeys.All`: contains every gated key + every `.View` key (spec "Full catalog is enumerable"); `CashierDefaults` excludes the admin-only keys listed in §6.3.

**`POS.Application.Tests`**
- `PermissionAuthorization` (both paths): admin bypass allows without row lookup; non-admin with grant allows; non-admin without grant denies; inactive requester denies; inactive role denies; `Guid.Empty` denies.
- Backup **create-vs-restore separation** (spec): role with only `Admin.Backup.Create` denied on `RestoreBackupUseCase`, allowed on `TriggerManualBackupUseCase`; role with `Restore` allowed on restore; role with neither denied on both.
- Settings gates: `Settings.Edit` required for `SaveSettingsUseCase`/`UpdateBackupDirectoryUseCase`; `Settings.View` for `LoadSettingsUseCase` (throws `UnauthorizedActionException` on deny).
- `CancelSaleUseCase`: cashier without `Invoices.Cancel` denied (sale status unchanged); admin succeeds via bypass; no inline role check remains.
- Users gates: create/edit/deactivate denied without the matching key.
- **Self-lockout guards**: deny deactivating last active admin; allow when another admin remains; deny reassigning last admin's role away; deny stripping admin role permissions.
- **Role use cases**: create rejects duplicate name + unknown keys; `UpdateRoleUseCase` requires `Roles.AssignPermissions` when grants change; `DeactivateRoleUseCase` denies system role + role-in-use, allows unassigned custom role.
- **Session snapshot**: login materializes admin's full catalog; non-admin login snapshots exactly the role's grant keys; mid-session role edit does NOT change the live session set (snapshot behavior).

**`POS.Presentation.Shared.Tests`** (updates, flagged as behavior change)
- `MainShellViewModel`: module visibility driven by permission set — admin (full set) still yields the existing hub module contract; a partial set shows only the granted `.View` modules; add the new `roles` module assertion.
- `InvoicesViewModel`: `CanCancel` reflects `Invoices.Cancel` presence.
- `UserManagementViewModel`: command `CanExecute` follows `Users.*` keys; `AvailableRoles` is the live query result.
- `RolesManagementViewModel` (new): grant toggles load/save; system-role toggles disabled; command gating.

**`POS.Infrastructure.Tests`** (if the project exists — else fold into Application/integration)
- Seeding idempotency: two runs → no duplicate roles; admin user points at `AdministratorRoleId`; Cashier has exactly the §6.3 grant set; Administrator has zero `RolePermission` rows.
- EF Guid conversion: a `RolePermission`/`User.RoleId` round-trips and a lookup-by-id matches (regression guard for the lowercase-TEXT bug).
- `RoleConstants` seed-Guid collision guard: assert `AdministratorRoleId`/`CashierRoleId` are not the all-zeros/default Guid and are distinct from each other. (They are hand-picked sentinels well outside the space `Guid.NewGuid()` realistically produces for custom roles, but the assertion documents the invariant cheaply.)

---

## 12. Dependency / sequencing order (for tasks phase)

1. **Domain foundation** (no deps): `PermissionKeys`, `RoleConstants`, `Role`, `RolePermission` entities.
2. **User swap + enum deletion**: `User.RoleId`, delete `UserRole.cs`, fix `ICurrentOperatorSession`/`InMemoryUserSession`/`LoginResult` compile (session plumbing).
3. **EF layer**: configs, DbSets, `IRoleRepository`+impl, `IUserRepository` count method, migration (regenerate Designer/snapshot).
4. **Seeding**: `DatabaseSchemaRollout` roles-before-admin + Cashier defaults.
5. **Gate**: `PermissionAuthorization`, replace `AdminUserAuthorization` call sites, add new gates, delete `AdminUserAuthorization.cs`, unify `CancelSaleUseCase`.
6. **Login snapshot**: `LoginUseCase` + `AppBootstrapper` wiring.
7. **Guardrails**: user + role guard logic (needs repos from step 3).
8. **Role use cases**: Get/Create/Update/Deactivate (need gate + repo).
9. **DI**: register repo + role use cases.
10. **WPF**: `MainShellViewModel`, `InvoicesViewModel`, `UserManagementViewModel`, new `RolesManagementViewModel`+view, shell wiring.
11. **Tests** across all four test projects.

Steps 1-6 form proposal slice 1 (data/domain/session/enum removal + gate + login). Steps 7-9 slice 2
(guardrails + role use cases + DI). Step 10 slice 3 (WPF). This matches the proposal's suggested
feature-branch-chain boundaries; final slicing is the tasks phase's call.

---

## 13. ADR-style decisions (summary)

| # | Decision | Rejected alternative | Rationale |
|---|---|---|---|
| D1 | `PermissionKeys` nested static classes + reflection-built `All` | flat consts; DB permission table | readable call sites; auto-enumeration for seeding/UI; catalog is code, not data |
| D2 | Admin session = full catalog materialized at login (+ repo-path Guid bypass) | sentinel "all" string; `HasPermission` always-true branch | one uniform set lookup everywhere; no admin special-casing in session; new keys auto-covered |
| D3 | One `IRoleRepository`, don't overload `IUserRepository` | extend `IUserRepository` with role queries | role is a separate aggregate; keeps interfaces cohesive |
| D4 | No `Role`→`RolePermission` navigation; grants via `ReplacePermissionsAsync` | owned collection nav | replace-all assignment fights EF tracking; simpler queryable grants |
| D5 | `UpdateRoleUseCase` does rename + permission-replace in one call/one `SaveChanges` | split rename vs assign use cases | one screen, one save; atomic; fewer round-trips |

> **D5 tasks-phase detail — grant-diff subroutine.** "Requires `Roles.AssignPermissions` only if grants
> differ" is NOT a prose hand-wave: it is a concrete set-equality step the tasks phase must implement.
> Before deciding whether the second gate applies, load the role's current grant keys via
> `IRoleRepository.GetPermissionKeysAsync(roleId, ct)` and compare with the incoming `permissionKeys`
> using `IReadOnlySet<string>.SetEquals(...)` (ordinal). If `!current.SetEquals(permissionKeys)` → grants
> changed → additionally gate `Roles.AssignPermissions`; otherwise the call is a pure rename and only
> `Roles.Edit` is required. This same loaded-current-grants value also drives the admin-strip guard
> (§7): for the Administrator role, `permissionKeys` must equal the current (empty) set or the call is a
> modification → deny. So the current-grants load happens once and feeds both checks.
| D6 | Settings gates throw `UnauthorizedActionException`; backup gates return result-object errors | invent error fields on `SettingsResult` | preserve each use case's existing return contract; match `CancelSale`/`Audit` throw pattern |
| D7 | Cashier granted `Settings.View` only, not `Settings.Edit`/`Backup.*` | grant all non-admin keys incl. settings edit | preserve effective capabilities without silently granting newly-gated destructive surfaces |
| D8 | Deterministic seeded role Guids in `RoleConstants` | random Guids looked up by name | stable FK; idempotent seeding; gate/seeding share the admin id |
| D9 | Delete `UserRole.cs` in the same slice as the `User` swap | phased deletion | enum + `RoleId` can't compile together; the swap is atomic by necessity |
