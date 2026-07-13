<Delta for audit-trail>
## ADDED Requirements

### Requirement: Audit log viewing is permission-gated

`GetAuditLogsUseCase` MUST require `Audit.View` via `PermissionAuthorization.EnsureHasPermissionAsync`. This replaces the previous `AdminUserAuthorization.EnsureAdministratorAsync` gate; the switch MUST NOT reduce or silently drop the protection the use case had before this change.

#### Scenario: User without Audit.View is denied reading audit logs
- GIVEN a requesting user whose role lacks `Audit.View`
- WHEN `GetAuditLogsUseCase` is invoked
- THEN the use case MUST return an authorization error
- AND no audit log entries MUST be returned

#### Scenario: Administrator retains audit log access via the bypass
- GIVEN an Administrator-role requesting user
- WHEN `GetAuditLogsUseCase` is invoked
- THEN the authorization check MUST succeed
- AND audit log entries MUST be returned
</Delta for audit-trail>
