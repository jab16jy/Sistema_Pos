# Turnos View Specification

## Purpose

Defines the ShiftsView UI translation: an active-shift banner, a shift history grid, and open/close panels with a cash-count difference calculator — the wireframe's single source of truth for operative cash closing (Reportes only consults completed closings).

## Requirements

### Requirement: Active shift banner is always visible when a shift is open

When a shift is open, the view MUST show a persistent banner with cashier name, open time, base cash, running sales total, and an "Abierto" status chip.

#### Scenario: Banner reflects the currently open shift

- GIVEN a shift opened by "Admin" at 08:00 with base $100,000 and running sales $1,842,000
- WHEN the view renders
- THEN the banner MUST display cashier "Admin", "Abierto a las 08:00", base "$100.000", and sales "$1.842.000"

### Requirement: Shift history grid is filterable by date range

The history grid MUST list shifts (cashier, status, open/close time, base, sales, difference) filterable by a date range, with an export action, and MUST color-code the difference column (`Cuadra` = success neutral text, positive = primary, negative = error).

#### Scenario: A shift with a cash shortfall shows a negative difference

- GIVEN a closed shift with difference -$15,000
- WHEN the row renders
- THEN the difference cell MUST display "-$15,000" in the error color

### Requirement: Opening a shift requires cashier and base cash

The "Abrir turno" panel MUST require selecting a cashier and MUST accept a base cash amount and optional opening notes.

#### Scenario: Opening a shift confirms success

- GIVEN a cashier and base cash are provided
- WHEN the operator activates "Abrir turno"
- THEN a success toast ("Turno abierto exitosamente") MUST appear

### Requirement: Closing a shift computes a live cash-count difference

The "Cerrar turno activo" panel MUST show expected cash and expected digital totals, accept a counted-cash input, and MUST compute and display the difference live as the operator types, using success/primary/error coloring for exact/over/under counts respectively.

#### Scenario: Counted cash matches expected — shows "cuadra" styling

- GIVEN expected cash is $942,000
- WHEN the operator enters counted cash of $942,000
- THEN the difference indicator MUST show $0 with success styling

#### Scenario: Counted cash below expected shows a shortfall

- GIVEN expected cash is $942,000
- WHEN the operator enters counted cash of $900,000
- THEN the difference indicator MUST show "Faltante: $42,000" with error styling

#### Scenario: Closing without entering counted cash is blocked

- GIVEN the counted-cash field is empty
- WHEN the operator activates "Cerrar turno"
- THEN the system MUST show an error ("Ingresa el efectivo contado") and MUST NOT close the shift

#### Scenario: Successful close confirms and generates a report

- GIVEN counted cash has been entered
- WHEN the operator activates "Cerrar turno"
- THEN a success toast ("Turno cerrado · Reporte generado") MUST appear
