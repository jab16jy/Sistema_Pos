Enterprise Point of Sale (POS) desktop application built with .NET and WPF following Clean Architecture and Domain-Driven Design principles.

Features:
- Product, inventory, customer and supplier management
- Sales, purchases, quotations and returns
- Cash register opening/closing and cash reconciliation
- Multi-user authentication with roles and permissions
- Complete audit trail for every business operation
- Reporting dashboard and business analytics
- Offline-first architecture with local persistence
- Barcode scanner, receipt printer and cash drawer support
- Modular architecture for future ERP expansion
- AI-assisted development using SDD documentation, Agent.md, SkillRegistry and memory-driven workflows

Focus:
Maintainable, scalable, testable enterprise software with MVVM, dependency injection, repository pattern, CQRS where appropriate, and modern Material Design UI.

Future ideas (not scheduled):
- Direct ESC/POS printing (QZ Tray / Star CloudPRNT style): talk to USB/network thermal printers directly instead of relying on a Windows-installed printer driver. Removes the "add the printer in Windows first" setup step for the end customer. Bigger than an installer change — it's a new communication layer alongside the existing InstalledPrinterProvider (winspool.drv) path.
