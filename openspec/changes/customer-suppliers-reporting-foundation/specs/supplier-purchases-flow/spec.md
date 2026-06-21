# Supplier Purchases Flow Specification

## Purpose

Define supplier records and purchase registration that increases stock with auditable inventory traceability.

## Requirements

### Requirement: Maintain supplier records for purchasing

The system MUST maintain supplier records that can be searched and selected during purchase registration.

#### Scenario: Register supplier for later purchase use

- GIVEN an operator provides the required supplier data
- WHEN the supplier record is saved
- THEN the supplier MUST be available for later purchase selection and lookup

#### Scenario: Reject purchase against unknown supplier

- GIVEN a purchase references a supplier that is not available for selection
- WHEN the operator attempts to confirm the purchase
- THEN the system MUST reject the purchase with a recoverable validation result

### Requirement: Persist purchases atomically with stock traceability

The system MUST persist purchase header, purchase items, supplier linkage, stock increase, and inventory-movement audit data as one atomic operation.

#### Scenario: Confirm valid purchase

- GIVEN a supplier and valid purchase items are ready for confirmation
- WHEN the purchase is confirmed
- THEN stock MUST increase according to the purchased quantities
- AND matching inventory-movement records MUST exist for the same purchase outcome

#### Scenario: Roll back failed purchase persistence

- GIVEN a purchase confirmation cannot persist all required purchase or inventory records
- WHEN the operation fails
- THEN no partial purchase or stock increase MUST remain persisted
