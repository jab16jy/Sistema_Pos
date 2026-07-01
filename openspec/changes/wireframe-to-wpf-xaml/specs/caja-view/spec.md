# Caja View Specification

## Purpose

Defines the CashierView UI translation to the wireframe: stock-status chips in search results, row-hover cart actions, a polished search dropdown, and wiring for the `F6`/`F7` secondary action buttons.

## Requirements

### Requirement: Search dropdown shows stock-status chips

The product search dropdown MUST show, per result row: thumbnail icon, name, code, a stock-status chip (`En stock` = success, `Stock bajo` = warning, `Sin stock` = error), and unit price.

#### Scenario: Low-stock product shows warning chip

- GIVEN a search result "Ibuprofeno 600mg · Caja x20" with stock 4 below its minimum
- WHEN the search dropdown renders
- THEN the row MUST display a `chip-warning` labeled "Stock bajo"

#### Scenario: In-stock product shows success chip

- GIVEN a search result with stock above its minimum
- WHEN the search dropdown renders
- THEN the row MUST display a `chip-success` labeled "En stock"

### Requirement: Cart rows reveal action icons on hover

Each cart table row MUST show product name+code, an editable quantity input, unit price, computed subtotal, and edit/delete icon buttons that are hidden by default and become visible when the row is hovered.

#### Scenario: Hovering a cart row reveals edit and delete actions

- GIVEN a cart row for "Crema Cicatrizante 50g"
- WHEN the operator hovers the row
- THEN the edit and delete icon buttons MUST become visible
- AND moving the pointer away MUST hide them again

#### Scenario: Changing quantity recalculates subtotal and totals

- GIVEN a cart row with quantity 2 at unit price $8,500
- WHEN the operator changes quantity to 3
- THEN the row subtotal MUST update to $25,500
- AND the cart summary (item/unit counts) and the total-to-pay MUST recompute

### Requirement: F6 and F7 secondary actions are wired

The "F6 · Cliente" and "F7 · En espera" secondary action buttons in the payment panel MUST be enabled and MUST invoke their respective ViewModel commands (associate a customer to the sale; put the current sale on hold), matching the wireframe's `pago-panel` layout.

#### Scenario: F6 associates a customer to the current sale

- GIVEN a sale in progress with no customer attached
- WHEN the operator activates "F6 · Cliente"
- THEN the ViewModel MUST expose a command to select/associate a customer
- AND the command MUST be enabled (not `IsEnabled=False`)

#### Scenario: F7 holds the current sale

- GIVEN a sale in progress with items in the cart
- WHEN the operator activates "F7 · En espera"
- THEN the ViewModel MUST expose an enabled command to hold the sale for later resume

### Requirement: Payment panel keeps numpad, change calculation, and F5 confirm

The payment panel MUST retain: total display, payment method selector, received-amount field, numeric keypad, computed change (or a "Falta $X" shortfall indicator when insufficient), and an `F5 · Finalizar venta` CTA.

#### Scenario: Insufficient received amount shows shortfall, not change

- GIVEN total to pay is $84,000 and received amount is $50,000
- WHEN the change is calculated
- THEN the panel MUST show "Falta $ 34,000" in the error color
- AND the `F5` CTA MUST remain available but the sale MUST NOT confirm with a shortfall

#### Scenario: Empty cart blocks sale confirmation

- GIVEN the cart has no items
- WHEN the operator activates `F5 · Finalizar venta`
- THEN the system MUST show an error message ("El carrito está vacío") and MUST NOT register a sale
