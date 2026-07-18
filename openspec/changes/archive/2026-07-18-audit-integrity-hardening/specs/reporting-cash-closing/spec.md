# Delta for reporting-cash-closing

## MODIFIED Requirements

### Requirement: Produce operational persisted totals

The system MUST derive operational totals from persisted sales, payments, receivable settlements, return settlements, and closing records. It MUST report gross billed sales from completed and returned sales, separately report dated cash and digital return outflows, and preserve the distinction between physical cash and digital value. Gross billing MUST be attributed to the sale date; return outflows MUST be attributed to the settlement date.
(Previously: reporting included only completed sales and receivable settlements, so returned sales and refund outflows were omitted.)

#### Scenario: Report a sale and later cash return in different periods

- GIVEN a sale billed for 50,000 COP on 2026-07-01 and later returned for cash on 2026-07-03
- WHEN an operator reports 2026-07-01
- THEN gross billed sales MUST include 50,000 COP
- AND return outflows MUST be zero for that period
- WHEN an operator reports 2026-07-03
- THEN cash return outflows MUST include 50,000 COP
- AND the original gross billing MUST NOT be moved to 2026-07-03

#### Scenario: Report a digital return separately from drawer cash

- GIVEN a persisted digital return settlement of 20,000 COP
- WHEN an operator requests the operational report for its settlement date
- THEN digital return outflows MUST include 20,000 COP
- AND expected drawer cash MUST NOT be reduced by that digital amount

### Requirement: Reconcile cash closing results

The cash-closing summary MUST calculate expected drawer cash from physical-cash inflows less persisted cash return outflows. It MUST NOT treat digital sales, digital receivable settlements, or digital return outflows as physical drawer cash. The summary MUST expose gross billing, cash return outflows, and digital return outflows as separate values.

#### Scenario: Close cash after a cash refund

- GIVEN the closing period has 100,000 COP in cash inflows and a persisted 15,000 COP cash return settlement
- WHEN the operator completes cash closing with 85,000 COP counted
- THEN expected drawer cash MUST be 85,000 COP
- AND the reported cash-return outflow MUST be 15,000 COP
- AND the cash difference MUST be zero

### Requirement: Historical reporting compatibility

The system MUST preserve historical completed and returned sale records without changing their billed values. Historical returns without persisted settlement evidence MUST remain excluded from cash and digital return-outflow totals, rather than being estimated. Report consumers MUST treat the new outflow fields as additive and MUST remain able to read prior reports that do not contain them.

#### Scenario: Read a period containing a legacy return without settlement evidence

- GIVEN a historical returned sale has no persisted return settlement
- WHEN an operator runs a report for the relevant period
- THEN gross billing MUST follow the preserved sale record
- AND no cash or digital return outflow MUST be inferred for that return
