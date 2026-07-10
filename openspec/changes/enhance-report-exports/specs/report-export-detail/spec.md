# Report export detail specification

## Requirement: Detailed operational workbook

The system MUST include a `Detalle de recaudo` worksheet when exporting an operational report to Excel.

### Scenario: Review collection components

Given an operational report with sales and receivable collection values
When the user exports the report to Excel
Then the workbook SHALL include each available collection component, its calculation, physical-cash impact, and operational interpretation
And the expected drawer cash SHALL remain distinct from digital collections.

## Requirement: Detailed profitability comparison workbook

The system MUST include a `Comparativo` worksheet when exporting a profitability report to Excel.

### Scenario: Review period changes

Given current and prior profitability values
When the user exports the profitability report to Excel
Then the workbook SHALL show current value, prior value, absolute change, percentage change, and an interpretation for income, costs, and gross margin.

## Requirement: Compact report PDFs

The system MUST render operational and profitability PDF body content at 75% scale.

### Scenario: Generate a report PDF

Given a generated operational or profitability report PDF
When the document is rendered
Then its report body SHALL be scaled to 75%
And its existing page header and footer SHALL remain available.
