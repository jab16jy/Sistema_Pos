# Delta for cashier-sales-flow

## ADDED Requirements

### Requirement: Derived and eligible return value

The system MUST accept a return only for a persisted completed sale. The return command MUST NOT accept a caller-supplied monetary refund amount. For every returned quantity, the system MUST derive the monetary amount from the immutable completed-sale line price, discount, quantity, and previously returned quantity; it MUST reject quantities that exceed the unreturned quantity or otherwise produce an invalid derived amount.

#### Scenario: Reject a return for a non-completed sale

- GIVEN a sale is held, cancelled, or already returned
- WHEN an operator requests a return against that sale
- THEN the system MUST reject the request before changing stock, receivables, loyalty, or financial records

#### Scenario: Derive a partial-return amount

- GIVEN a completed sale line for 4 units has immutable net value 40,000 COP
- AND 1 unit was already returned
- WHEN the operator returns 2 of the remaining units
- THEN the system MUST derive the return amount from the completed-sale line
- AND the request MUST NOT be able to increase or decrease that amount
- AND no more than 1 additional unit MUST remain returnable

#### Scenario: Reject a legacy caller-controlled amount

- GIVEN an integration submits a return request containing a refund amount field
- WHEN the Application layer receives the request
- THEN the request MUST be rejected as an incompatible contract
- AND no return MUST be persisted

### Requirement: Persist a classified return settlement

For every accepted return that disburses value to the customer, the system MUST persist exactly one settlement linked to that return, with the derived amount, settlement timestamp, and cash or digital classification. A receivable reduction that does not disburse cash or digital value MUST be persisted as receivable reconciliation and MUST NOT create a cash or digital return settlement. The return, stock restoration, settlement or reconciliation, and loyalty effect MUST commit atomically.

#### Scenario: Persist a cash refund

- GIVEN a completed sale has a derived return value of 20,000 COP
- WHEN the operator completes a cash return
- THEN exactly one cash return settlement for 20,000 COP MUST be persisted and linked to the return
- AND the stock restoration and return records MUST commit in the same unit of work

#### Scenario: Reconcile a receivable without a cash outflow

- GIVEN a completed credit sale has an outstanding receivable sufficient for its derived return value
- WHEN the operator returns items against that sale
- THEN the system MUST reduce the receivable by the derived value
- AND the system MUST NOT create a cash or digital return settlement

### Requirement: Return-contract compatibility and historical preservation

Completed-sale prices, discounts, quantities, existing returns, and historical status changes MUST remain immutable during this hardening. Returns created after rollout MUST use the derived-value contract. Historical returns that have no persisted settlement MUST remain visible as historical records but MUST NOT be inferred or backfilled as cash or digital disbursements without an explicitly audited data migration.

#### Scenario: Preserve an historical return without settlement evidence

- GIVEN a return was persisted before settlement support and has no settlement record
- WHEN the system reads its history after rollout
- THEN the return MUST remain readable
- AND the system MUST NOT fabricate a cash or digital settlement for it
