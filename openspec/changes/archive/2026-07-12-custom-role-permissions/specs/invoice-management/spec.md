<Delta for invoice-management>
## MODIFIED Requirements

### Requirement: Sale cancellation authorization

Cancelling a sale MUST require the requesting user's role to be granted `Invoices.Cancel` via `PermissionAuthorization.EnsureHasPermissionAsync`, replacing the previous inline `if (user.Role != UserRole.Administrator)` check. This unifies `CancelSaleUseCase` onto the same authorization path used by every other gated use case in this change, eliminating the previously divergent enforcement path.

(Previously: `CancelSaleUseCase` inline-compared `user.Role` to `UserRole.Administrator`, bypassing the shared `AdminUserAuthorization` helper entirely — a second, divergent enforcement path from every other admin-gated use case.)

The seeded Administrator role keeps effective access to `Invoices.Cancel` via the bypass; the seeded Cashier role is NOT granted `Invoices.Cancel`, preserving current behavior (cashiers could not cancel sales before this change either).

#### Scenario: Cashier without Invoices.Cancel is denied cancelling a sale
- GIVEN a Cashier-role user whose role has no `RolePermission` row for `Invoices.Cancel`
- WHEN the user calls `CancelSaleUseCase` for a completed sale
- THEN the use case MUST return/throw an authorization error
- AND the sale's status MUST remain unchanged

#### Scenario: Administrator succeeds cancelling a sale via the bypass
- GIVEN an Administrator-role user
- WHEN the user calls `CancelSaleUseCase` for a completed sale
- THEN the authorization check MUST succeed
- AND the sale MUST be cancelled per existing cancellation behavior

#### Scenario: Custom role explicitly granted Invoices.Cancel succeeds
- GIVEN a custom role granted `Invoices.Cancel`
- WHEN a user with that role calls `CancelSaleUseCase` for a completed sale
- THEN the authorization check MUST succeed
- AND the sale MUST be cancelled per existing cancellation behavior
</Delta for invoice-management>
