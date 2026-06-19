---
name: pos-efcore-sqlite
description: Project-specific EF Core and SQLite guidance for local/offline POS persistence.
---

# POS EF Core SQLite

Use this skill when implementing persistence, DbContext, mappings, repositories, migrations, transactions, backups, and local data behavior.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/01_diseno_integracion_tecnologica.md`
- `md/fase_disenio_pos_md/04_diseno_modelo_datos_sqlite.md`
- `md/fase_disenio_pos_md/09_diseno_protocolo_pruebas.md`

## Persistence requirements

- Database is local SQLite.
- The POS must work offline.
- EF Core is the ORM.
- Infrastructure owns DbContext, migrations, repository implementations, and backup/restore.
- Presentation must not access SQLite directly.

## Main tables from design

Minimum persistence model includes:

- USERS
- PRODUCTS
- SALES
- SALE_ITEMS
- PAYMENTS
- INVENTORY_MOVEMENTS

Additional modules imply:

- CLIENTS
- SUPPLIERS
- PURCHASES
- PURCHASE_ITEMS
- INVOICES
- POINTS_RULES
- POINTS_TRANSACTIONS
- CASH_CLOSINGS
- SYSTEM_CONFIGURATION

## Transaction rules

Sales and purchases must be atomic.

For a sale, save together:

- Sale header
- Sale items
- Payments
- Invoice/factura reference
- Inventory movements
- Client points changes, if applicable

For a purchase, save together:

- Purchase header
- Purchase items
- Inventory movements increasing stock
- Supplier reference

If one part fails, the full operation must fail. Money and inventory must not drift.

## Repository rules

- Repository interfaces belong in Application or Domain contracts depending on project convention.
- Implementations belong in Infrastructure.
- Do not expose EF Core tracking behavior to ViewModels.
- Avoid returning database entities directly to WPF if that couples UI to persistence.

## SQLite-specific concerns

- Plan indexes for product search by name and barcode.
- Keep product lookup fast for cashier flow.
- Consider local backup and restore as first-class features.
- Security documentation requires protected password storage and database protection using encryption or equivalent mechanism.

## Test requirements

Integration tests should validate:

- EF Core + SQLite mappings.
- Full sale flow persists sale, payment, invoice, and inventory movement.
- Purchase increases stock.
- Reports query real persisted data.
- Backup copies database correctly.

## Review checklist

- [ ] Sales and purchases use transactions.
- [ ] Product search has appropriate indexing strategy.
- [ ] Passwords are never stored as plain text.
- [ ] Backup/restore path is explicit and testable.
- [ ] SQLite details stay inside Infrastructure.

