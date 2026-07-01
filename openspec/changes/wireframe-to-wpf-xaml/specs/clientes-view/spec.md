# Clientes View Specification

## Purpose

Defines the CustomersView UI translation to the wireframe's 3-column layout: customer list with stats, a detail/edit form with loyalty points, and a filterable transaction history panel.

## Requirements

### Requirement: Three-column layout — list, form, history

`CustomersView` MUST render three columns: a searchable customer list with total/active stats, a customer detail form (name, ID, phone, email, address, notes, active toggle), and a history panel for the selected customer.

#### Scenario: Selecting a customer fills the form and history

- GIVEN the customer list shows "María López"
- WHEN the operator selects that row
- THEN the form MUST populate name, ID, phone, email, address, and active state
- AND the history panel MUST show that customer's KPIs (purchases count, total, balance) and transaction list

#### Scenario: New customer clears the form

- GIVEN a customer is currently selected
- WHEN the operator activates "Nuevo"
- THEN the form MUST reset to empty fields with title "Nuevo cliente"
- AND the delete button and status chip MUST be hidden

### Requirement: Loyalty points are visible on the customer form

When the selected customer has loyalty points greater than zero, the form MUST display a points badge showing the point count, and MUST offer a link to loyalty rules.

#### Scenario: Customer with points shows the points badge

- GIVEN a selected customer with 850 loyalty points
- WHEN the form renders
- THEN the points badge MUST display "850 puntos de lealtad disponibles"

#### Scenario: Customer with zero points hides the badge

- GIVEN a selected customer with 0 loyalty points
- WHEN the form renders
- THEN the points badge MUST NOT be displayed

### Requirement: History panel filters by transaction type

The history panel MUST offer filter buttons ("Todo", "Ventas", "Cotizaciones") that restrict the displayed list to sales, quotes, or all transaction types for the selected customer, each row showing type icon, title, date, and amount.

#### Scenario: Filtering by sales shows only sale transactions

- GIVEN a customer with mixed sale, quote, and payment history
- WHEN the operator selects the "Ventas" filter
- THEN only rows of type "sale" MUST be displayed

#### Scenario: No customer selected shows an empty-state prompt

- GIVEN no customer is currently selected
- WHEN the history panel renders
- THEN it MUST display "Selecciona un cliente para ver su historial" instead of a transaction list

### Requirement: Save and delete actions provide feedback

Saving or deleting a customer MUST trigger a toast confirmation and refresh the customer list.

#### Scenario: Saving a customer shows a confirmation toast

- GIVEN a valid customer form
- WHEN the operator activates "Guardar"
- THEN a success toast "Cliente guardado correctamente" MUST appear
- AND the customer list MUST reflect the saved data
