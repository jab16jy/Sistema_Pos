---
name: pos-wpf-mvvm
description: Project-specific WPF and MVVM guidance for the local POS desktop application.
---

# POS WPF MVVM

Use this skill when implementing WPF screens, ViewModels, commands, navigation, bindings, validation, and operator workflows.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/01_diseno_integracion_tecnologica.md`
- `md/fase_disenio_pos_md/05_diseno_flujos_funcionales.md`
- `md/fase_disenio_pos_md/06_diseno_wireframes_interfaz.md`
- `md/fase_disenio_pos_md/07_diseno_casos_uso.md`

## Required screens

Implement screens around these primary workflows:

- Login
- Main menu
- Caja / Sale
- Products
- Inventory
- Clients
- Suppliers
- Purchases
- Invoices and refunds
- Reports and cash closing
- Configuration, backup, and printing
- Barcode generation

## MVVM rules

- Views contain layout and bindings.
- ViewModels expose state, commands, validation messages, and loading/error state.
- ViewModels call Application use cases; they do not contain domain rules.
- Avoid code-behind except for purely visual behavior that cannot be expressed cleanly in XAML.
- Navigation should be explicit and predictable.

## POS interaction priorities

The cashier flow must be fast:

- Product search by name.
- Barcode scanner input through USB/HID.
- Quantity selection before or after adding to cart.
- Visible cart with product, unit price, quantity, subtotal.
- Automatic total calculation.
- Received amount and change calculation for cash.
- Payment method selection.
- Optional client association for points.
- Receipt printing after confirmation.

## Didactic UI requirements

Every screen should help non-technical users succeed:

- Use short, action-oriented labels.
- Prefer verbs on buttons: `Guardar`, `Cobrar`, `Imprimir`, `Cancelar`.
- Show only what the current task needs.
- Explain errors with recovery steps.
- Show confirmation before destructive actions.
- Keep frequent actions keyboard-friendly.
- Make scanner flow first-class, not an afterthought.

## Important boundaries

- The WPF project must not access SQLite directly.
- The WPF project must not implement business calculations as the source of truth.
- The WPF project may display calculated values returned by Application/Domain.

## Review checklist

- [ ] Main cashier path is operable with keyboard/scanner.
- [ ] ViewModels are testable without WPF.
- [ ] Validation messages are clear and human.
- [ ] Critical actions explain consequences.
- [ ] UI state distinguishes loading, empty, success, and error conditions.

