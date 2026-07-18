# Delta for customer-receivables

## ADDED Requirements

### Requirement: Derived return reconciliation

The system MUST reconcile a return against a receivable only by the return amount derived from the immutable completed-sale lines. It MUST link the reconciliation to the originating receivable and return, MUST NOT reduce a receivable below zero, and MUST reject a reconciliation when the sale, customer, or receivable linkage is invalid.

#### Scenario: Reconcile a valid derived return

- GIVEN a completed sale has an open receivable of 30,000 COP
- AND an accepted return has a derived value of 12,000 COP
- WHEN the return is persisted
- THEN the receivable balance MUST become 18,000 COP
- AND the reconciliation MUST identify both the receivable and the return

#### Scenario: Reject excessive reconciliation

- GIVEN a completed sale has an open receivable of 5,000 COP
- AND its accepted return has a derived value of 8,000 COP
- WHEN the system attempts receivable reconciliation
- THEN the system MUST reject the return outcome or require the remaining value to use a separately classified settlement
- AND the receivable balance MUST NOT become negative

### Requirement: Receivable referential integrity and legacy preflight

New receivable and receivable-settlement records MUST reference existing customers and originating sales through database-enforced non-polymorphic relationships. Before adding or enforcing such relationships for legacy data, the migration process MUST run read-only orphan preflight diagnostics and MUST abort before schema mutation if invalid references exist. The process MUST NOT delete, relink, or silently repair historical records.

#### Scenario: Abort a receivable relationship migration for legacy orphan data

- GIVEN a legacy receivable references a missing sale or customer
- WHEN the operator runs the migration preflight
- THEN diagnostics MUST identify the offending record identifiers
- AND the migration MUST stop before changing the schema or historical data
