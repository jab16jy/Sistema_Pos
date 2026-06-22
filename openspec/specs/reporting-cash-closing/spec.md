# Reporting Cash Closing Specification

## Purpose

Define operational reporting and cash-closing outputs from persisted data with strict physical-cash versus digital separation.

## Requirements

### Requirement: Produce operational persisted totals

The system MUST derive reporting totals from persisted sales, payments, receivable settlements, and closing records. It MUST separate physical cash from digital collections and SHOULD stay limited to operational reporting rather than advanced BI.

#### Scenario: Report mixed tender totals

- GIVEN persisted activity includes cash sales, digital sales, and receivable settlements
- WHEN an operator requests an operational report for that period
- THEN the report MUST show billed totals, cash collected, digital collected, and receivable settlement totals as separate values

#### Scenario: Reject invalid report range

- GIVEN the requested report range is invalid
- WHEN the operator runs the report
- THEN the system MUST reject the request with a recoverable validation result

### Requirement: Reconcile cash closing results

The system MUST produce a cash-closing summary with billed total, expected drawer cash, digital collections, counted cash, and difference. It MAY expose export-ready output for operational use.

#### Scenario: Close cash with mixed tenders

- GIVEN persisted activity exists for a closing period and counted cash is provided
- WHEN the operator completes cash closing
- THEN the summary MUST separate expected cash from digital collections
- AND the summary MUST show the difference between expected cash and counted cash

#### Scenario: Reject closing without counted cash

- GIVEN persisted activity exists for a closing period
- WHEN the operator attempts to close cash without counted cash
- THEN the system MUST reject the closing and preserve the open state
