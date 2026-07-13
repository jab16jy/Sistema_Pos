<Delta for user-auth>
## ADDED Requirements

### Requirement: Login snapshots the role's permission set into the session

`LoginUseCase` MUST, after validating credentials, load the authenticating user's role and its granted permission set, and populate `ICurrentOperatorSession` with `RoleId`, `RoleName`, and an `IReadOnlySet<string> Permissions`. For an Administrator-role user, the session MUST behave as having the full permission catalog (via the bypass — an explicit "has-all" representation, not necessarily an enumerated set).

#### Scenario: Login populates session permissions for a custom role
- GIVEN a user assigned to a custom role "Report Viewer" granted only `Reports.View`
- WHEN the user logs in successfully
- THEN `ICurrentOperatorSession.Permissions` MUST contain `Reports.View`
- AND `ICurrentOperatorSession.RoleId` MUST equal the "Report Viewer" role id

#### Scenario: Login grants Administrator full access via bypass
- GIVEN a user assigned to the system Administrator role
- WHEN the user logs in successfully
- THEN `ICurrentOperatorSession.HasPermission(key)` MUST return `true` for any valid `PermissionKey`, including keys not explicitly enumerated in any `RolePermission` row

#### Scenario: Unauthenticated session has no permissions
- GIVEN no user is logged in
- WHEN `ICurrentOperatorSession.Permissions` is inspected
- THEN it MUST be empty
- AND `ICurrentOperatorSession.HasPermission(key)` MUST return `false` for any key

### Requirement: Role permission changes are effective on next login only

Editing a role's `RolePermission` grants while a user with that role is logged in MUST NOT retroactively change that user's already-active session permission set. The updated grants take effect only the next time that user logs in.

#### Scenario: Mid-session role edit does not affect the active session
- GIVEN a user is logged in with a session snapshot containing `Reports.View` but not `Settings.Edit`
- WHEN an administrator grants `Settings.Edit` to that user's role while the user remains logged in
- THEN the logged-in user's session `Permissions` set MUST still NOT contain `Settings.Edit`
- AND `HasPermission("Settings.Edit")` for that active session MUST return `false`

#### Scenario: Next login reflects the updated grants
- GIVEN a user's role was granted `Settings.Edit` while the user was logged in (per the prior scenario)
- WHEN that user logs out and logs in again
- THEN the new session's `Permissions` set MUST contain `Settings.Edit`
</Delta for user-auth>
