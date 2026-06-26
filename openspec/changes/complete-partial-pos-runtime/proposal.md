# Proposal: Complete Partial POS Runtime

## Problem

Several POS modules are visible as implemented, but important runtime paths still behave like prototypes or stubs. Quote sending can fail during WPF dependency construction, WhatsApp assumes a Unix shell, report and print services throw `NotImplementedException`, and product lookup still relies on prototype data in cashier/quote flows.

## Goals

- Make WPF startup able to construct communication services reliably.
- Replace Unix-only WhatsApp launching with a Windows-compatible configurable process handoff.
- Replace report/print stubs with recoverable infrastructure behavior suitable for the current application.
- Use persisted product lookup in implemented WPF flows.
- Preserve Clean Architecture boundaries and keep UI free of SQLite/report/print details.
- Add tests before implementation where coverage exists.

## Non-Goals

- Build the full Settings/SystemConfiguration UI.
- Integrate directly with the Meta WhatsApp Business API.
- Complete the planned Products, Inventory, Facturas, or Configuración modules.
- Deliver full BI reporting, charts, or every ESC/POS printer profile.

## Capabilities

### New Capabilities

- `runtime-readiness` — infrastructure runtime behavior for communication, report, print, and persisted lookup readiness.

### Modified Capabilities

- `cashier-sales-flow` — cashier should use persisted product lookup when infrastructure is available.
- `quotations-ui-flow` — quote send should use configured/recoverable communication services.
- `reporting-cash-closing` — reporting should provide export-ready bytes or a clear unsupported result, not crash from a stub.

## Approach

Introduce typed infrastructure options registered by WPF, implement Windows-compatible script launching, add an EF-backed product lookup service, replace throwing report/print stubs with bounded current-scope behavior, and update composition root wiring. Tests should cover DI construction, lookup behavior, communication failure recovery, and non-throwing service outcomes.

## Rollback Plan

Revert the infrastructure wiring and service implementation commits. Because this change should not require a database migration, rollback is limited to code and tests.
