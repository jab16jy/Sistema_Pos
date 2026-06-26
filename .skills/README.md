# Project Skills — Sistema POS Local

These project-local skills are derived from the documentation under `docs md/` (the project Obsidian vault).

Use them as implementation guidance when building the POS. They are intentionally project-specific: the goal is not to replace official framework documentation, but to keep agents aligned with the system architecture, domain rules, UX constraints, and quality expectations already defined for this repository.

## Recommended skills

1. [`pos-clean-architecture`](./pos-clean-architecture/SKILL.md)
   - Use when creating or reviewing project structure, dependencies, layer boundaries, use cases, repositories, services, or domain rules.

2. [`pos-wpf-mvvm`](./pos-wpf-mvvm/SKILL.md)
   - Use when implementing WPF views, ViewModels, commands, bindings, navigation, and user-facing desktop flows.

3. [`pos-domain-model`](./pos-domain-model/SKILL.md)
   - Use when modeling entities, value objects, invariants, sale rules, inventory rules, client points, purchases, invoices, and cash closing.

4. [`pos-efcore-sqlite`](./pos-efcore-sqlite/SKILL.md)
   - Use when implementing persistence, EF Core mappings, SQLite transactions, repositories, migrations, backups, and local/offline data behavior.

5. [`pos-printing-barcodes`](./pos-printing-barcodes/SKILL.md)
   - Use when implementing ESC/POS receipt printing, barcode scanning, barcode generation, and label printing.

6. [`pos-reporting-cash`](./pos-reporting-cash/SKILL.md)
   - Use when implementing reports, cash closing, utilities/profit calculations, exports, invoices, and sales summaries.

7. [`pos-testing-quality`](./pos-testing-quality/SKILL.md)
   - Use when writing unit, integration, functional, usability, performance, security, and installation tests.

8. [`pos-didactic-usability`](./pos-didactic-usability/SKILL.md)
   - Use when designing screens, messages, error states, confirmations, onboarding flows, and keyboard/scanner-friendly interactions.

9. [`jornada`](./jornada/SKILL.md)
    - Use when the user asks for a work session summary: "jornada", "jornada de hoy", "qué cambios hicimos", "resumen de cambios", "bitácora de la app", "jornada del módulo X", "qué cambios hicimos ayer".

## Non-negotiable project principles

- The application is a local desktop POS for small businesses.
- Target stack from documentation: C#/.NET 9, WPF, MVVM, Clean Architecture, EF Core, SQLite, ESC/POS, PDF/Excel reporting.
- Operation must work offline.
- Business rules must not depend on WPF.
- WPF must not query SQLite directly.
- Infrastructure owns EF Core, SQLite, printing, reporting, backup, and external services.
- The UI must be didactic: short text, clear actions, helpful messages, prevention of common operator mistakes.
- Sales and inventory updates must be atomic.
- Cash reports must distinguish physical cash from digital payments.

