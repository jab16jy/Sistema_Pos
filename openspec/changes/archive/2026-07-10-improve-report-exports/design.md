# Design: Improve Report Exports

## Scope

Keep the `IReportExportService` contract unchanged. The Infrastructure implementation will enrich presentation only from the DTOs it already receives, preserving Clean Architecture boundaries.

## PDF design

Both reports use a monochrome, print-friendly layout compatible with the invoice and quote style:

- A title header with report identity, selected period, and generation timestamp.
- A compact KPI section for the figures an operator needs first.
- Bordered, consistently headed tables for detailed figures.
- A footer with the page number and source label.

The operational report groups billed sales, collections, receivable settlements, and the expected physical drawer cash. Digital collections remain visibly distinct from physical cash.

The profitability report summarizes revenue, cost, gross margin, overall margin, and period growth before showing ranked top sellers and margin detail. Empty collections render an explanatory row rather than an empty table.

## Excel design

Each workbook starts with a `Resumen` sheet and uses a shared visual vocabulary:

- merged title band and period metadata;
- KPI blocks;
- table headers with frozen panes;
- COP currency and percentage number formats;
- sensible widths, filters, and conditional color cues;
- a second sheet for operational cash flow or separate detailed sheets for profitability data.

The values remain numeric rather than preformatted text, so users can filter, calculate, and reuse them in Excel.

## Verification

Add focused tests for generated PDF signatures/content and Excel sheet names, labels, numeric values, and styles. Run the focused test project plus solution build.
