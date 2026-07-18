# Delta for quotations-ui-flow

## ADDED Requirements

### Requirement: Concurrent-safe year-scoped quote numbering

The system MUST allocate each new quote number through a transactional, year-scoped persisted counter. The counter MUST issue a distinct number under concurrent creation, MUST seed from the highest existing number for that year before its first allocation, and MUST NOT reuse an issued number after quote deletion. This requirement applies only to quote numbering and MUST NOT introduce a shared document-sequence mechanism.

#### Scenario: Allocate concurrent quotes distinctly

- GIVEN two SQLite contexts create quotes for the same year concurrently
- WHEN both quote creations commit
- THEN each quote MUST have a distinct number for that year
- AND neither creation MUST depend on counting existing rows

#### Scenario: Seed the counter from historical quotes

- GIVEN existing quotes for 2026 have a highest issued number of 125
- AND no counter row exists for 2026
- WHEN the first new 2026 quote is created
- THEN the quote number MUST be 126
- AND the counter MUST persist the next allocation state atomically

#### Scenario: Do not reuse a deleted quote number

- GIVEN quote number 126 was issued and later deleted
- WHEN another quote is created for the same year
- THEN the new quote number MUST be greater than 126

#### Scenario: Start a new year independently

- GIVEN the current allocation is for 2026
- WHEN the first quote for 2027 is created
- THEN the system MUST allocate from the 2027 counter without changing 2026 issued numbers

### Requirement: Quote relationship migration safety

New quote customer and quote-conversion relationships MUST reference existing non-polymorphic customer and sale records through database-enforced foreign keys where those references are present. Before enforcement, the migration process MUST preflight legacy orphan references and MUST abort before schema mutation if any are found. Historical quote numbers and valid quote data MUST remain unchanged.

#### Scenario: Abort quote relationship migration for orphan conversion data

- GIVEN a legacy quote conversion references a missing sale
- WHEN the operator runs the migration preflight
- THEN diagnostics MUST identify the orphaned quote reference
- AND the migration MUST not modify the schema or quote data
