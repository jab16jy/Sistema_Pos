# Improve Report Exports

## Why

The Reports module currently generates valid but sparse PDF and Excel files. Operators need clear hierarchy, explicit cash-versus-digital separation, and decision-ready profitability information when opening an exported report away from the POS.

## What Changes

- Redesign operational and profitability PDFs with a consistent print-friendly report header, KPI summary, grouped data sections, explanatory labels, and page footer.
- Redesign Excel workbooks with summary dashboards, consistent title bands, readable tables, frozen headers, currency/percentage formats, conditional cues, and practical column sizing.
- Add automated export tests that verify both formats are generated and expose the expected report structure.

## Impact

- Affected capability: `reporting-cash-closing`
- Affected code: `src/POS.Infrastructure/Services/ReportExportService.cs`, reporting export tests
- No domain model, persistence schema, or UI workflow changes
