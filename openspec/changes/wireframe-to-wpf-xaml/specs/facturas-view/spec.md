# Facturas View Specification

## Purpose

Defines the InvoicesView UI translation: a filterable invoice grid, a selected-invoice action panel with reprint/PDF/email/WhatsApp, and a return/refund registration panel, per the wireframe's "no separate communication module" decision.

## Requirements

### Requirement: Invoice grid supports search, date range, and status filters

The invoice list MUST be filterable by free-text search (number or client), a date range, and a status selector (`Todo`, `Activa`, `Anulada`, `Devuelta`), with a paginated grid showing number, client, total, status chip, and date.

#### Scenario: Filtering by status narrows the grid

- GIVEN invoices with statuses `activa`, `anulada`, and `devuelta`
- WHEN the operator selects status `Anulada`
- THEN the grid MUST show only invoices with status `anulada`

#### Scenario: Selecting an invoice populates the action panel

- GIVEN the grid shows invoice `#1082`
- WHEN the operator clicks that row
- THEN the "Factura seleccionada" panel MUST display its number, client, total, and date

### Requirement: Selected-invoice panel exposes reprint, PDF, email, WhatsApp

The action panel for the selected invoice MUST expose four actions: Reimprimir, PDF, Email, WhatsApp, each producing a confirmation toast on success.

#### Scenario: Reprint without a selected invoice is blocked

- GIVEN no invoice is selected
- WHEN the operator activates "Reimprimir"
- THEN the system MUST show an error ("Selecciona una factura") and MUST NOT attempt to print

#### Scenario: Email send shows success confirmation

- GIVEN invoice `#1082` is selected
- WHEN the operator activates "Email"
- THEN a success toast ("Factura enviada por email") MUST appear

### Requirement: Return registration requires a reason

The "Registrar devolución" panel MUST require selecting a return reason before processing, MUST offer an optional description and a refund type (Efectivo, Nota crédito, Cambio de producto), and MUST require a selected invoice.

#### Scenario: Processing a return without selecting a reason is blocked

- GIVEN an invoice is selected but no return reason is chosen
- WHEN the operator activates "Procesar devolución"
- THEN the system MUST show an error ("Selecciona un motivo") and MUST NOT register the return

#### Scenario: Successful return registration confirms and clears the form

- GIVEN invoice `#1082` is selected with reason "Producto dañado o defectuoso"
- WHEN the operator activates "Procesar devolución"
- THEN a success toast referencing the invoice number MUST appear
- AND the reason and description fields MUST reset
