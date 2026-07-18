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

The system MUST require the canonical active authenticated session actor to hold the existing purchase-registration permission before it persists a purchase. It MUST use that session actor for purchase and audit attribution, and it MUST preserve the existing atomic purchase, stock, and inventory-movement outcome.

#### Scenario: Reject unauthorized purchase before stock mutation

- GIVEN an authenticated active user lacks purchase-registration permission
- WHEN the user confirms valid purchase items
- THEN the system MUST reject the operation before persisting a purchase or inventory movement
- AND product stock MUST remain unchanged

#### Scenario: Attribute an authorized purchase to the session actor

- GIVEN an authenticated active user has purchase-registration permission
- WHEN the user confirms a valid purchase
- THEN the purchase and resulting audit record MUST be attributed to that session actor
- AND the stock increase and inventory movements MUST remain atomic with the purchase

#### Scenario: Roll back failed purchase persistence

- GIVEN a purchase confirmation cannot persist all required purchase or inventory records
- WHEN the operation fails
- THEN no partial purchase or stock increase MUST remain persisted
