# Reportes View Specification

## Purpose

Defines the ReportsView UI translation to the wireframe's 3-subtab structure ("Resumen operacional" / "Rentabilidad" / "Cierres y arqueos") with KPI cards, chart placeholders, and a clear boundary that operative cash closing happens only in Turnos.

## Requirements

### Requirement: Three subtabs with date-range/preset filters

The view MUST expose subtabs "Resumen operacional", "Rentabilidad", and "Cierres y arqueos". The operational subtab MUST offer preset filters (`Hoy`, `Semana`, `Mes`) plus explicit date-range inputs, and export actions for PDF and Excel.

#### Scenario: Selecting a preset updates the active filter button

- GIVEN "Hoy" is the active preset
- WHEN the operator selects "Semana"
- THEN "Semana" MUST become the active-styled button and "Hoy" MUST revert to inactive

### Requirement: Operational subtab shows 5 KPI cards and two chart panels

The operational subtab MUST show 5 KPI cards (total facturado, efectivo esperado, efectivo ventas, pagos digitales, transferencias) and two chart panels: sales-by-hour bars and payment-method breakdown bars with percentage labels.

#### Scenario: Cash reconciliation KPI shows a "Cuadra" chip when it matches

- GIVEN efectivo esperado equals efectivo ventas
- WHEN the KPI card renders
- THEN it MUST display a `chip-success` labeled "Cuadra"

### Requirement: Rentabilidad subtab shows top products, category margin, and period comparison

The "Rentabilidad" subtab MUST show: a top-10 best-selling products ranked list with proportional bars, a per-category gross-margin breakdown, a two-period comparison panel, and a platform breakdown (POS vs converted quotations).

#### Scenario: Top-10 list ranks by units sold

- GIVEN 5 products with descending unit counts
- WHEN the top-10 panel renders
- THEN products MUST be listed in descending order of units sold with a proportional bar per row

#### Scenario: Period comparison shows the percentage difference

- GIVEN Período A total $1,842k and Período B total $1,643k
- WHEN the comparison renders
- THEN the difference MUST show "+12%" in success styling

### Requirement: Cierres y arqueos subtab is read-only and defers closing to Turnos

The "Cierres y arqueos" subtab MUST show the last registered shift closing summary and a closings query/export panel, and MUST NOT provide a cash-closing action — it MUST instead offer a link/button that navigates to Turnos for the actual closing operation.

#### Scenario: Attempting to close cash from Reportes redirects to Turnos

- GIVEN the operator is on "Cierres y arqueos"
- WHEN the operator activates "Ir a Turnos para cerrar caja"
- THEN the system MUST navigate to the Turnos module
- AND no cash-closing MUST be performed within Reportes

#### Scenario: Panel explains the scope boundary

- GIVEN the "Cierres y arqueos" subtab renders
- WHEN the query panel displays its explanatory notes
- THEN it MUST state that operative shift closing does not happen here and is performed from Turnos
