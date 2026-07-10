## ADDED Requirements

### Requirement: Export reports with actionable visual hierarchy

The system MUST export operational and profitability reports as legible PDF and Excel documents. Each export MUST identify its reporting period, expose key totals before detailed rows, and retain numeric data in reusable form.

#### Scenario: Export operational report with separated funds

- GIVEN an operational report contains sales collections and receivable settlements
- WHEN an operator exports it to PDF or Excel
- THEN the export MUST clearly distinguish physical cash from digital collections
- AND it MUST present expected drawer cash as a separately labelled key total
- AND it MUST include the selected reporting period

#### Scenario: Export profitability report with decision-ready summary

- GIVEN a profitability report contains product sales and margin data
- WHEN an operator exports it to PDF or Excel
- THEN the export MUST show revenue, cost, gross margin, and growth summary figures before the detail
- AND it MUST present top-selling products and product-margin data in readable tables

#### Scenario: Export an empty report

- GIVEN a selected period has no products in its profitability result
- WHEN the operator exports the report
- THEN the export MUST remain valid and readable
- AND it MUST communicate that no detail exists for the selected period
