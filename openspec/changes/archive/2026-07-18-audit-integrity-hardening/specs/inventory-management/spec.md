# Delta for inventory-management

## ADDED Requirements

### Requirement: Optimistic concurrency for stock-affecting products

The system MUST protect tracked products with a persistence concurrency token that changes whenever a product modification is saved. Sales, returns, purchases, and manual stock adjustments MUST detect a stale product write and return a recoverable, retryable conflict. The system MUST NOT automatically replay a financial or stock command after a conflict.

#### Scenario: Reject a stale stock write

- GIVEN two SQLite contexts load the same tracked product at the same version
- AND the first context saves a stock-affecting change
- WHEN the second context attempts to save its stale stock-affecting change
- THEN the second operation MUST return a recoverable concurrency conflict
- AND the first persisted stock change MUST remain intact
- AND the stale operation MUST NOT persist an inventory movement or financial outcome

### Requirement: Durable inventory operational references

Each inventory movement MUST reference an existing product through a database-enforced non-polymorphic foreign key. Return items MUST reference their originating return, completed-sale item, and product through database-enforced non-polymorphic foreign keys. The intentionally polymorphic inventory movement business reference MUST remain unconstrained by a foreign key.

#### Scenario: Reject an orphan inventory movement

- GIVEN no product exists for a proposed inventory movement
- WHEN the system attempts to persist the movement
- THEN the database MUST reject the orphaned relationship
- AND no stock change MUST be committed

### Requirement: Inventory relationship migration safety

Before adding or enforcing the required operational foreign keys, the migration process MUST run read-only preflight diagnostics for orphan product, sale-item, return, and return-item references. If a diagnostic finds an orphan, the process MUST abort before schema mutation and MUST report the offending identifiers. Valid historical data MUST migrate without changing its business values.

#### Scenario: Migrate valid inventory history

- GIVEN historical inventory movements and return items all reference existing required records
- WHEN the migration preflight and migration run
- THEN the relationships MUST be enforced
- AND historical quantities, timestamps, and references MUST remain unchanged
