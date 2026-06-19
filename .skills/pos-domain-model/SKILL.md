---
name: pos-domain-model
description: Project-specific domain modeling guidance for products, sales, inventory, clients, points, purchases, invoices, and cash behavior.
---

# POS Domain Model

Use this skill when modeling entities, value objects, invariants, and business rules.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/Exploracion/modulos.md`
- `md/fase_disenio_pos_md/03_diseno_modelo_dominio.md`
- `md/fase_disenio_pos_md/07_diseno_casos_uso.md`
- `md/fase_disenio_pos_md/08_diseno_integracion_modulos.md`

## Core modules

The system has 11 functional modules:

1. Caja
2. Productos
3. Inventario
4. Clientes
5. Puntos
6. Proveedores
7. Pagos / Compras
8. Informe de Ventas
9. Facturas
10. Utilidades
11. Código de Barras

## Core entities

Model these concepts explicitly:

- User
- Role
- Product
- Category
- InventoryMovement
- Sale
- SaleItem
- Payment
- Client
- PointsAccount or LoyaltyAccount
- Supplier
- Purchase
- PurchaseItem
- Invoice
- Refund or SaleCancellation
- CashClosing
- SystemConfiguration

## Value objects

Use value objects for values that need validation and meaning:

- Money
- Quantity
- Barcode
- Email
- Phone
- DocumentNumber / Identification
- DateRange
- PaymentBreakdown
- StockLevel

## Business rules

### Products and inventory

- Product must have name, sale price, cost price, category, and stock data.
- Barcode may be optional, but if present it must be valid for supported formats.
- Stock minimum should generate alerts.
- Sale decreases inventory.
- Purchase increases inventory.
- Refund or cancellation restores inventory when applicable.

### Sales

- A sale contains one or more items.
- Total is derived from items, quantities, discounts, and applicable point redemptions.
- Cash payment must validate amount received and calculate change.
- Digital payments count as income but not physical cash in drawer.
- Sale confirmation must atomically save sale, items, payment, invoice, and inventory movement.

### Clients and points

- Client can be associated with sale.
- Points can accumulate according to configurable rules.
- Points can be redeemed as discount if enabled and valid.
- Points behavior should be configurable, not hard-coded.

### Security and authorization

- Cashier can sell and consult.
- Administrator can configure, cancel/refund, manage users, and view administrative reports.
- Sale cancellation/refund requires administrator authorization and reason.

## Modeling warning

Do not build this as CRUD tables with a pretty UI. That is a common beginner mistake. A POS is a transaction system: the hard part is preserving business consistency when money, stock, invoice, and payment change together.

## Review checklist

- [ ] Business rules are centralized in Domain/Application, not duplicated across screens.
- [ ] Money calculations avoid primitive string/decimal scattering.
- [ ] Inventory changes are represented as movements.
- [ ] Cancellations/refunds are auditable.
- [ ] Payment model distinguishes cash from digital methods.

