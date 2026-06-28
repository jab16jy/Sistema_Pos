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

### Requirement: Export service is non-throwing

The reporting export service MUST provide bounded report bytes for the currently supported daily sales scope or return a recoverable unsupported/configuration result through callers.

#### Scenario: Daily report export succeeds

- GIVEN persisted completed sales exist for a date
- WHEN the report service generates the daily sales report
- THEN it MUST return non-empty bytes
- AND those bytes SHOULD preserve cash versus digital separation

#### Scenario: Report scope unsupported

- GIVEN a requested report format or scope is not yet supported
- WHEN the report service is invoked
- THEN the application MUST avoid `NotImplementedException`
- AND the caller MUST receive a recoverable failure path

### Requirement: Top-sellers report (top 10)

The existing ReportsViewModel MUST include a top-sellers section showing the top 10 products by units sold for the selected period. Columns: Rank, Product Name, Units Sold, Total Revenue. Data loaded via `IProfitQueryService.GetTopSellingProductsAsync(period, topN: 10)`.

#### Scenario: Top-sellers table loads correctly for current week

- GIVEN it is 2026-06-27 (Saturday) and the current week has sales
- AND ProductA sold 30 units, ProductB sold 25 units, ... (10+ products with sales)
- WHEN the user opens the Reports screen and views the Top Sellers section for "Esta Semana"
- THEN the table shows exactly 10 rows
- AND the first row shows ProductA with 30 units and correct revenue
- AND rows are sorted by units sold descending

### Requirement: Period comparison panel

The existing ReportsViewModel MUST include a period comparison section showing "Esta semana vs semana pasada" and "Este mes vs mes pasado" comparisons with Revenue delta and Gross Margin delta with percentage. Data loaded via `IProfitQueryService.GetPeriodComparisonAsync`.

#### Scenario: Period comparison shows revenue and margin deltas

- GIVEN this week revenue = $1,200,000 COP, gross margin = $350,000 COP
- AND last week revenue = $1,000,000 COP, gross margin = $300,000 COP
- WHEN the user views the Period Comparison panel
- THEN "Esta semana vs semana pasada" shows:
  Revenue: +$200,000 (+20.0%)
  Gross Margin: +$50,000 (+16.67%)

### Requirement: Payment platform breakdown table

The existing ReportsViewModel MUST include a platform breakdown section with columns: Platform, Number of Transactions, Total Amount, % of Total. Data loaded via `IProfitQueryService.GetPaymentPlatformBreakdownAsync`. Restricted to the canonical platform list: Nequi, Bancolombia, Daviplata, Efectivo.

#### Scenario: Payment breakdown table sums all platforms

- GIVEN in the current month:
  Nequi: 15 transactions, $450,000 COP
  Bancolombia: 8 transactions, $240,000 COP
  Daviplata: 2 transactions, $60,000 COP
  Efectivo: 40 transactions, $1,250,000 COP
- WHEN the user views the Payment Platform Breakdown table
- THEN 4 rows are displayed (all platforms regardless of 0-count)
- AND Efectivo shows $1,250,000 with the highest % of total
- AND all percentages sum to 100%

### Requirement: Gross margin per day table

The existing ReportsViewModel MUST include a gross margin per day section with columns: Date, Total Revenue, Total Cost, Gross Margin, Margin %. Data loaded via `IProfitQueryService` broken down by day. Covers the selected date range (default: current month).

#### Scenario: Gross margin per day table shows daily rows

- GIVEN the user selects "Este Mes" (June 2026) as the date range
- AND sales exist on 15 of the 27 days in the month so far
- WHEN the user views the Gross Margin per Day table
- THEN 15 rows are displayed (only days with sales, or all 27 days with 0s — either is acceptable)
- AND each row shows: Date, Revenue, Cost, Gross Margin, Margin %
- AND Gross Margin = Revenue - Cost for each row

### Requirement: All extended report data uses DB-side aggregation

All four extended report sections MUST source their data from `IProfitQueryService` implementations that use EF Core DB-side GroupBy. Loading full SaleItem or Payment rows into memory before aggregating is NOT permitted.

#### Scenario: Extended report sections show empty state when no data exists

- GIVEN the selected date range has no sales (e.g., future dates)
- WHEN the user views any of the four extended report sections
- THEN each section shows an empty-state message (e.g., "Sin datos para el período seleccionado")
- AND no exception is thrown
- AND Revenue/Margin values are shown as $0 or N/A where applicable
