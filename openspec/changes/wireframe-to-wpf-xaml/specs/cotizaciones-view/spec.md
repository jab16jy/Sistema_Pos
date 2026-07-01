# Cotizaciones View Specification

## Purpose

Defines the reconciliation of `QuoteView` and `QuoteHistoryView` into a single subtabbed page ("Nueva cotización" / "Historial") matching the wireframe, including product cart building and WhatsApp/email SendDocument integration.

## Requirements

### Requirement: Single view with two subtabs

The Cotizaciones module MUST present exactly two subtabs — "Nueva cotización" and "Historial" — within one view, replacing the separate `QuoteView` and `QuoteHistoryView` navigation entries.

#### Scenario: Switching subtabs preserves in-progress quote data

- GIVEN the operator has added items to the quote cart on "Nueva cotización"
- WHEN the operator switches to "Historial" and back
- THEN the cart items MUST remain unchanged

### Requirement: New-quote form captures client data and product cart

The "Nueva cotización" subtab MUST capture client name (required), phone, email, validity in days, and observations, plus a searchable product list that adds items to a cart showing quantity, unit price, and subtotal, with computed subtotal/IVA/total.

#### Scenario: Adding a product increases cart totals

- GIVEN an empty quote cart
- WHEN the operator adds "Crema Cicatrizante 50g" (price $22,000, IVA 19%)
- THEN the cart MUST show 1 item
- AND subtotal, IVA, and total MUST update accordingly

#### Scenario: Confirming without a client name is blocked

- GIVEN the client name field is empty
- WHEN the operator activates "Confirmar cotización"
- THEN the system MUST show an error ("Ingresa el nombre del cliente") and MUST NOT create the quote

#### Scenario: Confirming without cart items is blocked

- GIVEN the cart is empty
- WHEN the operator activates "Confirmar cotización"
- THEN the system MUST show an error ("Agrega al menos un producto") and MUST NOT create the quote

### Requirement: Quote send actions integrate WhatsApp and Email

The new-quote footer MUST expose "WhatsApp" and "Email" send actions in addition to "Confirmar cotización", consistent with the wireframe decision that document sending lives inside Cotizaciones (no separate SendDocument module).

#### Scenario: Sending by email without an email address is blocked

- GIVEN the client email field is empty
- WHEN the operator activates "Email"
- THEN the system MUST show an error ("Ingresa el email del cliente") and MUST NOT attempt to send

#### Scenario: Sending by WhatsApp with an empty cart is blocked

- GIVEN the quote cart is empty
- WHEN the operator activates "WhatsApp"
- THEN the system MUST show an error ("Carrito vacío") and MUST NOT attempt to send

### Requirement: History subtab lists quotes with status and per-row actions

The "Historial" subtab MUST show a filterable, paginated grid of quotes (number, client, total, status chip, expiry date) with per-row actions for PDF, email, and WhatsApp, and an additional "Venta" action only when status is `aceptada`.

#### Scenario: Accepted quote offers conversion to sale

- GIVEN a quote row with status "aceptada"
- WHEN the row renders
- THEN a "Venta" action button MUST be visible
- AND activating it MUST create a sale from the quote

#### Scenario: Non-accepted quotes hide the conversion action

- GIVEN a quote row with status "vencida", "rechazada", or "borrador"
- WHEN the row renders
- THEN the "Venta" action button MUST NOT be visible
