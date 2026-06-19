---
name: pos-didactic-usability
description: Project-specific usability guidance for making the POS didactic, clear, and easy for non-technical users.
---

# POS Didactic Usability

Use this skill when designing UI, messages, validations, confirmations, empty states, workflows, or documentation for end users.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/05_diseno_flujos_funcionales.md`
- `md/fase_disenio_pos_md/06_diseno_wireframes_interfaz.md`
- `md/fase_disenio_pos_md/07_diseno_casos_uso.md`
- `md/fase_disenio_pos_md/10_diseno_entregable_final.md`

## Core usability goal

The POS must be usable by cashiers and administrators without technical training. The system should guide the user step by step, prevent common mistakes, and make recovery obvious.

## Required style

- Short text.
- Everyday language.
- Visible keyboard/scanner affordances.
- Clear visual hierarchy.
- Only necessary fields per task.
- Buttons with verbs.
- Helpful success and error messages.
- Confirmation before critical actions.

## Message examples

Use messages like:

- `Producto agregado al carrito.`
- `Solo hay 2 unidades disponibles.`
- `La venta fue guardada. Puedes imprimir el recibo nuevamente desde Facturas.`
- `Inventario actualizado automáticamente.`
- `Esta venta sumará puntos al cliente.`

## Critical UX flows

Prioritize:

- First login.
- Product search/scanning in Caja.
- Cash payment and change.
- Stock insufficient recovery.
- Sale confirmation and receipt printing.
- Product registration.
- Purchase registration.
- Cash closing.
- Refund/cancellation with administrator authorization.
- Backup and restore.

## Design warning

Do not dump database forms on the user. That is not design; that is surrender. The operator is trying to run a business, not admire your tables. Every screen must map to a real task.

## Review checklist

- [ ] User can understand what to do next without reading a manual.
- [ ] Error messages explain both problem and correction.
- [ ] Destructive actions explain consequences.
- [ ] Frequent actions are fast and visible.
- [ ] Screens match the documented wireframes and flows.

