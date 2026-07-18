# Delta for audit-trail

## ADDED Requirements

### Requirement: Audited actor matches authorized actor

For sale confirmation, return processing, receivable payment, stock adjustment, purchase registration, shift start, shift close, and cash closing, the audit trail MUST record the same canonical active authenticated session actor that the Application layer authorized. It MUST NOT record a caller-supplied actor identifier for these commands.

#### Scenario: Audit an authorized return with the canonical actor

- GIVEN an active authenticated user is authorized to process returns
- WHEN that user completes a valid return
- THEN the resulting audit entry MUST identify that session user
- AND the audit actor MUST match the actor used for authorization

#### Scenario: Do not audit a rejected command as a mutation

- GIVEN an authenticated user lacks the required permission for cash closing
- WHEN the user submits a cash-closing command
- THEN no successful mutation audit entry MUST be created
- AND no cash-closing record MUST be persisted

### Requirement: Historical audit compatibility

Existing audit records MUST remain immutable and readable. Canonical-actor enforcement applies to new targeted mutations only; the system MUST NOT rewrite historical audit actor values to infer session identity retroactively.

#### Scenario: View a pre-hardening audit record

- GIVEN an audit record was created before canonical-actor enforcement
- WHEN an operator views audit history
- THEN the stored record MUST remain available unchanged
- AND the viewer MUST NOT infer or replace its historical actor
