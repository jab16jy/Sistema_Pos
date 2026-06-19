---
name: pos-reporting-cash
description: Project-specific guidance for reports, cash closing, invoices, utilities, exports, and sales analysis.
---

# POS Reporting and Cash Closing

Use this skill when implementing sales reports, cash closing, invoice history, refunds, utilities/profit, PDF/Excel exports, or dashboard summaries.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/Exploracion/modulos.md`
- `md/fase_disenio_pos_md/05_diseno_flujos_funcionales.md`
- `md/fase_disenio_pos_md/06_diseno_wireframes_interfaz.md`
- `md/fase_disenio_pos_md/08_diseno_integracion_modulos.md`

## Reporting modules

Relevant modules:

- Informe de Ventas
- Facturas
- Utilidades
- Caja / Cierre de Caja
- Productos vendidos
- Métodos de pago
- Inventario

## Cash distinction rule

This is non-negotiable:

- Cash payments increase physical cash in drawer.
- Digital payments are income but do not increase physical cash in drawer.

Reports must clearly separate:

- Total facturado
- Efectivo esperado
- Pagos digitales
- Efectivo contado
- Diferencia

## Required report capabilities

- Daily sales summary.
- Date range filtering.
- Sales by payment method.
- Products sold by period.
- Best-selling products.
- Cash closing summary.
- Profit by product and by date range.
- Export to PDF and Excel.
- Charts for sales by day, week, and month.

## Invoice and refund rules

- Invoices must be searchable by date, number, and client.
- Invoice detail includes products, quantities, prices, and total.
- Invoices can be reprinted.
- Refunds/cancellations require administrator authorization.
- Refunds/cancellations must record reason and update inventory where applicable.

## Utility/profit formula

Use:

```text
(sale price - cost price) × units sold
```

Keep this in Application/Domain logic. Do not let each report screen invent its own formula.

## Review checklist

- [ ] Cash and digital payment totals are separated.
- [ ] Reports use persisted sales and payments, not UI state.
- [ ] Refunds are auditable.
- [ ] Export behavior is available for required reports.
- [ ] Profit calculation is consistent across screens.

