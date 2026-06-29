# Sistema POS Local

Local desktop POS for small businesses. Documentation lives in `docs md/`.

## Target stack

- C# / .NET 9
- WPF
- MVVM
- Clean Architecture
- EF Core 9
- SQLite
- ESC/POS thermal printing
- PDF/Excel reporting (QuestPDF + ClosedXML)
- ZXing.Net barcode generation

## Modules (15)

| # | Module | Role |
|---|--------|------|
| 1 | Dashboard | All |
| 2 | Cashier (POS) | Cashier + Admin |
| 3 | Customers | Cashier + Admin |
| 4 | Quotations | Cashier + Admin |
| 5 | Invoices | Cashier + Admin |
| 6 | Products | Admin |
| 7 | Inventory | Admin |
| 8 | Reports | Admin |
| 9 | Settings | Admin |
| 10 | Categories | Admin |
| 11 | Loyalty | Admin |
| 12 | Barcode Labels | Admin |
| 13 | Profit Analytics | Admin |
| 14 | User Management | Admin |
| 15 | Audit Log | Admin |
| — | Cashier Shifts | Admin |

> Cashier role sees modules 1–5 only. Admin sees all.

## Key features

- **Authentication** — PBKDF2/SHA256 login; default admin/admin123 seeded on first run
- **Role-based access** — Admin sees all modules; Cashier is restricted to sales flow
- **IVA 19 %** — applied in domain layer on products and sale items
- **ESC/POS printing** — auto-detects COM port, IP:PORT, USB, or falls back to process command
- **PDF/Excel export** — operational and profit reports via QuestPDF + ClosedXML
- **CSV product import** — row-level validation with error reporting
- **Loyalty program** — points earned/redeemed per sale; tiered levels
- **Barcode label printing** — ZXing.Net generation + label print flow
- **Cash closing** — date-range filters and closing report in Reports module
- **Audit log** — all significant operations recorded and viewable
- **Cashier shifts** — start/close workflow with shift summary
- **User management** — CRUD for users with role assignment; logout loop
- **Manual backup** — one-click SQLite backup from Settings

## Architecture

```text
Presentation -> Application -> Domain
Infrastructure -> Application/Domain contracts
Domain -> no outer dependency
```

Project layout:

```text
src/
  POS.Domain/
  POS.Application/
  POS.Infrastructure/
  POS.Presentation.Shared/
  POS.Presentation/
tests/
  POS.Domain.Tests/
  POS.Application.Tests/
  POS.Presentation.Shared.Tests/
```

## Running the pre-built app

A framework-dependent build for Windows is included in `build_output/`.

Requirements:

- Windows 10/11 (x64)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) — `Microsoft.WindowsDesktop.App 9.0`

Launch the app by double-clicking `POS.Presentation.exe` inside `build_output/`, or from a terminal:

```cmd
 dotnet build src/POS.Presentation/POS.Presentation.csproj -o build_output
.\build_output\POS.Presentation.exe
```

The database file (`pos.db`) and any local configuration are created in the same directory on first run.

## Development environment

Build and run on **Windows** with the .NET Desktop SDK / Visual Studio (required for WPF).

Domain, Application, and Infrastructure tests run on any OS:

```bash
dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj
dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj
dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj
```

## Runtime options

The WPF app reads optional local settings from environment variables:

- `POS_SMTP_HOST`, `POS_SMTP_PORT`, `POS_SMTP_FROM`, `POS_SMTP_USER`, `POS_SMTP_PASSWORD`, `POS_SMTP_SSL`
- `POS_WHATSAPP_SCRIPT`, `POS_WHATSAPP_WORKDIR`
- `POS_PRINTER_COMMAND`, `POS_PRINTER_WORKDIR`

If these values are missing, document sending or printing fails with a recoverable operator message instead of breaking startup or losing the sale.

## Project-specific skills

Read `.skills/README.md` before implementation. The local skills encode the architecture, domain, persistence, UI, printing, reporting, testing, and didactic usability rules extracted from the project documentation.
