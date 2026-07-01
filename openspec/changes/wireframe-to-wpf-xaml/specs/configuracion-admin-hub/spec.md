# Configuración Admin Hub Specification

## Purpose

Defines the redesign of `SettingsView` from a flat settings form into a 3-subtab administrative hub ("Centro de control" / "Negocio" / "Impresión y envíos") with a KPI strip and quick-link grid to the other backoffice modules, matching the wireframe's role as the umbrella entry point reached via the shell's `ADM` funcbar item.

## Requirements

### Requirement: Hub has three subtabs

The Configuración view MUST expose exactly three subtabs: "Centro de control" (overview), "Negocio" (business data), and "Impresión y envíos" (integrations), matching the wireframe's `subtab-bar`.

#### Scenario: Default subtab is Centro de control

- GIVEN the operator navigates to Configuración via `ADM`
- WHEN the view renders
- THEN the "Centro de control" subtab MUST be active by default

### Requirement: Centro de control shows a KPI strip and quick-link grid to 7 modules

The overview subtab MUST show a 4-item KPI strip (usuarios activos, turno actual, último backup, canales listos) and a quick-link grid linking to: Productos, Inventario, Compras, Reportes, Turnos, Lealtad, and Barcode, each with an icon, title, and one-line description.

#### Scenario: Quick-link navigates to the target module

- GIVEN the "Centro de control" subtab is active
- WHEN the operator clicks the "Inventario" quick-link
- THEN the shell MUST navigate to the Inventario module view

#### Scenario: Non-wireframed modules appear as non-linked placeholders

- GIVEN "Usuarios y permisos", "Auditoría operativa", and "Respaldos" have no dedicated wireframe
- WHEN the quick-link grid renders
- THEN these three entries MUST be displayed as static (non-navigable) placeholders, not as active links

### Requirement: Negocio subtab edits business identity and shows commercial policies

The "Negocio" subtab MUST provide editable fields for business name, tax ID, address, and currency, a "Guardar" action, and a read-only policies list (IVA por defecto, ventas a crédito, devoluciones sin admin, backup automático).

#### Scenario: Saving business data persists changes

- GIVEN the operator edits the business name field
- WHEN the operator activates "Guardar"
- THEN the updated business name MUST be persisted
- AND receipts MUST reflect the new name (per existing `system-settings` business-information behavior)

### Requirement: Impresión y envíos subtab groups printer, email, and WhatsApp config

The "Impresión y envíos" subtab MUST provide three grouped panels: Impresión (command/port, receipt template), Email/SMTP (host, sender account, port, test-send action), and WhatsApp/Script (script path, working directory, status, test-script action).

#### Scenario: Testing the WhatsApp script surfaces status without blocking operation

- GIVEN the WhatsApp script path is configured
- WHEN the operator activates "Probar script"
- THEN the system MUST report success or failure without crashing
- AND a failure MUST NOT prevent other Configuración actions from working

### Requirement: Alerts and checklist surface operational risks

The overview subtab MUST show an "Alertas" panel (free-text operational warnings) and a "Checklist de operación segura" list with pill-style status per item (printer configured, SMTP configured, WhatsApp script status, backup path status).

#### Scenario: Checklist reflects unresolved configuration

- GIVEN the WhatsApp script status is "Requiere revisión"
- WHEN the checklist renders
- THEN the corresponding row MUST show a warning-styled pill, not a success pill
