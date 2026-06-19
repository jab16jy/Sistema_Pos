---
name: pos-clean-architecture
description: Project-specific Clean Architecture guidance for the local POS system using Domain, Application, Infrastructure, and Presentation layers.
---

# POS Clean Architecture

Use this skill when creating, reviewing, or refactoring the system structure, dependencies, use cases, repositories, services, or cross-layer communication.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/01_diseno_integracion_tecnologica.md`
- `md/fase_disenio_pos_md/02_diseno_arquitectura_capas.md`
- `md/fase_disenio_pos_md/08_diseno_integracion_modulos.md`

## Required architecture

The system must be organized into:

```text
POS.Domain
POS.Application
POS.Infrastructure
POS.Presentation
```

## Dependency rule

Dependencies point inward:

```text
Presentation -> Application -> Domain
Infrastructure -> Application/Domain contracts
Domain -> no outer layer dependencies
```

Do not break this. A POS is like a building: if you let the paint decide where columns go, the structure collapses. UI is paint. Domain and application rules are the columns.

## Layer responsibilities

### Domain

Owns business meaning and invariants:

- Products
- Inventory movements
- Sales
- Sale items
- Payments
- Clients
- Points
- Suppliers
- Purchases
- Invoices
- Users and roles
- Cash closing concepts

Domain must not depend on WPF, EF Core, SQLite, file system, printing, or report libraries.

### Application

Owns use cases and orchestration:

- Authenticate user
- Create product
- Register sale
- Register purchase
- Cancel or refund sale
- Close cash register
- Generate report
- Backup database

Use cases coordinate repositories and services through interfaces.

### Infrastructure

Owns technical implementation:

- EF Core DbContext
- SQLite database access
- Repository implementations
- Transactions
- ESC/POS printing
- PDF/Excel reporting
- Barcode generation library integration
- Backup and restore implementation
- Password hashing implementation

### Presentation

Owns WPF and MVVM:

- Views
- ViewModels
- Commands
- Navigation
- User messages
- Visual states
- Keyboard/scanner-friendly interaction

Presentation calls Application use cases. It must not query SQLite directly.

## Integration rules

- WPF sends actions to ViewModels.
- ViewModels call Application use cases.
- Use cases enforce workflow and call Domain behavior.
- Repositories persist through EF Core and SQLite.
- Printing/reporting are invoked through Application interfaces implemented in Infrastructure.

## Review checklist

- [ ] Domain has no UI or persistence dependency.
- [ ] Application defines ports/interfaces for repositories and external services.
- [ ] Infrastructure implements ports and does not leak EF entities into Presentation.
- [ ] Presentation uses commands and bindings instead of database calls.
- [ ] Sales, inventory changes, and payments are handled as one business operation.
- [ ] Critical operations have authorization and audit behavior where required.

