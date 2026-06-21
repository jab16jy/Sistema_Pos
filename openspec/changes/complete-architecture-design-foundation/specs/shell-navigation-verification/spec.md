# Shell Navigation Verification Specification

## Purpose

Defines the expected shell module contract so verification covers the full module set exposed by navigation.

## Requirements

### Requirement: Nine-module shell contract

The system MUST expose and verify exactly the active shell modules required for the first architecture-design slice, including quotations.

#### Scenario: Shell exposes required modules

- GIVEN the main shell is initialized
- WHEN navigation modules are inspected
- THEN the module set MUST contain 9 active modules
- AND the set MUST include `quotes`

#### Scenario: Verification rejects stale expectations

- GIVEN a shell verification test expects fewer than the active module set
- WHEN the test is executed
- THEN the expectation MUST fail until aligned with the current shell contract
