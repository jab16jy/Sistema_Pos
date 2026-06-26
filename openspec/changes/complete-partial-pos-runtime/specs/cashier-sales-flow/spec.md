# Delta for Cashier Sales Flow

## ADDED Requirements

### Requirement: Persisted product lookup for cashier

The cashier flow SHOULD use persisted product lookup when the WPF infrastructure container is available, while tests MAY continue injecting in-memory lookup doubles.

#### Scenario: Cashier searches active product

- GIVEN an active product exists in SQLite
- WHEN the cashier searches by name or barcode
- THEN the cashier search results MUST include the persisted product
- AND the result MUST include price and available stock

#### Scenario: Cashier search excludes inactive product

- GIVEN an inactive product exists in SQLite
- WHEN the cashier searches by name or barcode
- THEN the inactive product MUST NOT appear in cashier search results
