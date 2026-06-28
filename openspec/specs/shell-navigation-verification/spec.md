# Shell Navigation Verification Specification

## Purpose

Defines the expected shell module contract so verification covers the full module set exposed by navigation.

## Requirements

### Requirement: Nine-module shell contract

The system MUST expose and verify exactly the active shell modules required for the complete POS runtime, including quotations, products, inventory, invoices, and settings.

#### Scenario: Shell exposes required modules

- GIVEN the main shell is initialized
- WHEN navigation modules are inspected
- THEN the module set MUST contain 10 active modules
- AND the set MUST include `quotes`, `products`, `inventory`, `invoices`, and `settings`
- AND all 10 modules MUST have `IsImplemented: true`

#### Scenario: Verification rejects stale expectations

- GIVEN a shell verification test expects fewer than the active module set
- WHEN the test is executed
- THEN the expectation MUST fail until aligned with the current shell contract
