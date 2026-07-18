# Spec: User Authentication Module

**Change**: wt3-categorias-auditoria-login
**Feature**: user-auth
**Version**: 1.0
**Date**: 2026-07-09

---

## Purpose

Define the requirements for user authentication, specifically validation feedback and error messaging in the login process.

---

## Requirements

### Requirement: Active login validation feedback

The login interface MUST display immediate, explicit validation feedback when a user attempts to submit empty credentials, rather than silently disabling the login action. The validation message MUST instruct the user to enter the missing username or password.

#### Scenario: Error feedback for empty username
- GIVEN the user leaves the username field empty
- WHEN the user attempts to execute the login command
- THEN a validation error message MUST display "El usuario es requerido" or similar indicating that the username is empty
- AND the authentication attempt MUST NOT be sent to the backend

#### Scenario: Error feedback for empty password
- GIVEN the user enters a username but leaves the password field empty
- WHEN the user attempts to execute the login command
- THEN a validation error message MUST display "La contraseña es requerida" or similar indicating that the password is empty
- AND the authentication attempt MUST NOT be sent to the backend

### Requirement: Credentials error display

The login interface MUST bind to and display credential validation errors (such as incorrect password or user not found/inactive) returned from the authentication service.

#### Scenario: Incorrect credentials error display
- GIVEN a user enters an incorrect password for an existing username
- WHEN the login command is executed
- THEN the login interface MUST display the error message returned by the authentication service in the feedback area

### Requirement: Canonical active authenticated actor

The Application layer MUST derive the actor for sale confirmation, return processing, receivable payment, stock adjustment, purchase registration, shift start, shift close, and cash closing from the authenticated current session. It MUST reject commands when the session is absent or its user is inactive. These commands MUST NOT trust, authorize, or audit a caller-supplied actor identifier.

#### Scenario: Reject an unauthenticated financial command

- GIVEN no authenticated session exists
- WHEN a caller attempts a receivable payment
- THEN the Application layer MUST reject the command before persistence
- AND no audit, financial, inventory, or shift record MUST be created

#### Scenario: Reject an inactive session actor

- GIVEN the current session identifies an inactive user
- WHEN that user attempts a stock adjustment
- THEN the Application layer MUST reject the command
- AND no inventory movement MUST be created

#### Scenario: Ignore a divergent request actor

- GIVEN the authenticated active session belongs to user A
- AND a return request contains user B as an actor identifier
- WHEN the Application layer receives the request
- THEN the request MUST be rejected as an incompatible contract or omit that identifier entirely
- AND the system MUST NOT create an audit record attributed to user B

### Requirement: Application permission enforcement for critical commands

Before executing each targeted command, the Application layer MUST enforce its existing action-specific permission for the canonical active session actor. Presentation-layer visibility or command enablement MUST NOT be the sole authorization control.

#### Scenario: Reject an unauthorized purchase

- GIVEN an authenticated active user lacks the purchase-registration permission
- WHEN the user submits a valid purchase
- THEN the Application layer MUST reject the command before changing stock or persisting a purchase

#### Scenario: Permit an authorized sale

- GIVEN an authenticated active user has the sale-confirmation permission
- WHEN the user confirms a valid sale
- THEN authorization MUST succeed using the session actor
- AND any resulting audit actor MUST be that same session actor

### Requirement: Owner-only shift lifecycle

The system MUST derive the cashier identity for shift start from the canonical session actor. The system MUST allow shift close only when the canonical active session actor owns the open shift. Administrator status alone MUST NOT bypass ownership; an administrator override requires a separately specified permission and is out of scope for this change.

#### Scenario: Prevent closing another cashier's shift

- GIVEN cashier A owns an open shift
- AND cashier B has an authenticated active session
- WHEN cashier B attempts to close cashier A's shift
- THEN the system MUST reject the operation
- AND cashier A's shift MUST remain open

#### Scenario: Close the owner's open shift

- GIVEN the canonical active session actor owns an open shift
- WHEN that actor submits a valid closing amount
- THEN the system MUST allow the shift to close

### Requirement: Username-index preflight and identity preservation

Before applying or enforcing the username unique index, the migration process MUST run a read-only, case-sensitive duplicate-username preflight. On duplicates, it MUST report actionable conflicting user identifiers and MUST abort before schema mutation. The process MUST NOT rename, delete, merge, or otherwise sanitize login identities automatically; valid historical usernames MUST retain their exact values and case-sensitive semantics.

#### Scenario: Abort username migration on exact duplicate usernames

- GIVEN two legacy users have the same username using the existing case-sensitive comparison
- WHEN the operator runs the username-index preflight
- THEN diagnostics MUST identify both user records and the duplicate username
- AND the unique index MUST NOT be created

#### Scenario: Preserve distinct case-sensitive usernames

- GIVEN legacy usernames are distinct under the existing case-sensitive equality semantics
- WHEN the username-index migration runs
- THEN the usernames MUST remain unchanged
- AND the migration MUST NOT introduce case-insensitive matching

### Requirement: Operational actor referential integrity

New non-polymorphic sale and shift actor references MUST be protected by database-enforced foreign keys to existing users. Before enforcing those relationships on legacy data, the migration process MUST run read-only orphan preflight diagnostics and MUST abort before schema mutation if an actor reference is invalid. The process MUST NOT delete, substitute, or silently repair historical sale, shift, or user records.

#### Scenario: Abort actor relationship migration for an orphan shift actor

- GIVEN a legacy shift references a missing user
- WHEN the operator runs the actor relationship preflight
- THEN diagnostics MUST identify the shift and missing actor reference
- AND the foreign-key migration MUST stop before schema mutation

#### Scenario: Reject a new sale with an unknown actor

- GIVEN no user exists for a proposed sale actor reference
- WHEN the system attempts to persist the sale outcome
- THEN the database MUST reject the invalid actor relationship
- AND no partial sale outcome MUST be committed

### Requirement: Application-owned user unit of work

User repositories MUST stage user changes without committing them independently. User creation, update, and any role-bootstrap work MUST commit through the Application-owned unit of work as one atomic outcome.

#### Scenario: Roll back failed user creation with role bootstrap

- GIVEN user creation stages a user and required role-bootstrap changes
- WHEN role bootstrap fails before the Application unit of work commits
- THEN neither the user nor the partial role-bootstrap changes MUST persist

#### Scenario: Commit a successful user outcome once

- GIVEN a valid user create or update request
- WHEN the Application use case succeeds
- THEN the Application-owned unit of work MUST commit the complete outcome exactly once

### Requirement: Application-contract compatibility

Presentation and integration callers of the targeted commands MUST migrate to session-derived actor contracts before rollout. The Application layer MUST fail closed for obsolete actor-bearing contracts; it MUST NOT retain a caller-supplied actor fallback. Existing session, user, role, audit, and shift records MUST remain readable without rewriting their historical actor values.

#### Scenario: Read historical audit records after actor hardening

- GIVEN an audit record was persisted before canonical actor enforcement
- WHEN the audit history is queried after rollout
- THEN the record MUST remain readable with its stored historical actor value
- AND the system MUST apply canonical actor rules only to new targeted commands
