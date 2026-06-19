---
name: pos-printing-barcodes
description: Project-specific guidance for ESC/POS thermal printing, barcode scanner support, barcode generation, and product labels.
---

# POS Printing and Barcodes

Use this skill when implementing receipt printing, printer abstractions, barcode scanning, barcode generation, or label printing.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/Exploracion/modulos.md`
- `md/fase_disenio_pos_md/01_diseno_integracion_tecnologica.md`
- `md/fase_disenio_pos_md/05_diseno_flujos_funcionales.md`
- `md/fase_disenio_pos_md/08_diseno_integracion_modulos.md`

## Printing requirements

- Receipt/factura printing uses ESC/POS for thermal printers.
- Printing implementation belongs in Infrastructure.
- Application should depend on an abstraction such as a print service interface.
- The sale flow prints after the sale is saved.
- Invoices must support reprinting.

## Barcode requirements

The system supports:

- Product lookup by barcode in Caja.
- USB/HID scanner input.
- Barcode assignment when registering products.
- Barcode generation for products without manufacturer barcode.
- Label preview and printing.
- EAN-13, Code 128, internal codes, and optionally QR according to requirements.

## Critical flow

Product registration:

```text
Create product -> scan/generate barcode -> save product -> product can be sold in Caja
```

Sale:

```text
Scan barcode -> find product -> validate stock -> add to cart -> confirm sale -> print receipt
```

## Reliability rules

- A printer error must not silently lose the sale.
- If printing fails after sale save, show a clear message and allow reprint from Facturas.
- Barcode scanner input should not require fragile UI focus behavior where possible.
- Generated internal barcodes must be unique.

## Review checklist

- [ ] Printing is behind an Application-facing interface.
- [ ] Failed printing provides recovery through invoice reprint.
- [ ] Barcode uniqueness is enforced.
- [ ] Scanner flow works in the cashier screen.
- [ ] Label printing includes product name and barcode.

