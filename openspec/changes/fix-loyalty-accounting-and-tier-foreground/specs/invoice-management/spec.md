# Delta for Invoice Management

## MODIFIED Requirements

### Requirement: Sale return processing preserves loyalty accounting

The system MUST compensate sale-linked loyalty movements when it cancels a completed sale, processes a full return, or processes the supported single partial return. The system MUST perform loyalty compensation atomically with the existing sale-status, inventory, and return persistence work. It MUST NOT alter the existing refund settlement API, refund-method selection, or return UI workflow as part of loyalty compensation.

#### Scenario: Process a full return with loyalty movements

- GIVEN a completed sale has inventory effects and sale-linked loyalty movements
- WHEN an authorized operator processes a full return
- THEN the sale status, return record, inventory restoration, and exact loyalty compensations MUST be persisted in one unit of work

#### Scenario: Roll back a partial return when loyalty persistence fails

- GIVEN an authorized operator submits a valid partial return for a sale with loyalty movements
- WHEN persistence of the return or a loyalty compensation fails
- THEN the system MUST roll back the sale return, return items, inventory effects, and loyalty compensations together
- AND the original sale and ledger rows MUST remain unchanged

### Requirement: Item-level partial return keeps loyalty allocation independent of refund settlement

The system MUST preserve the existing `RefundAmount` field for refund settlement and record keeping. The system MUST NOT use `RefundAmount` to calculate loyalty compensation; it MUST calculate that compensation from immutable original sale data and returned quantities as defined by the loyalty-points specification.

The system MUST retain the existing one-return-per-sale boundary. It MUST NOT support multiple partial-return events for a sale in this change, because multiple events require cumulative per-source allocation to prevent rounding loss or duplicate compensation.

#### Scenario: Vary refund settlement without changing points compensation

- GIVEN two otherwise identical partial-return requests select the same original sale items and quantities
- AND the requests contain different `RefundAmount` values
- WHEN each request is evaluated for loyalty compensation
- THEN the system MUST calculate the same loyalty compensation for both requests
- AND the existing refund settlement behavior MUST remain outside this change

#### Scenario: Reject a second partial-return event

- GIVEN a sale already has a persisted partial-return event
- WHEN an operator submits another partial-return event for that sale
- THEN the system MUST reject the second event
- AND the system MUST NOT create additional loyalty compensation
