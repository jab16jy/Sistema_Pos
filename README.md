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
- PDF/Excel reporting

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
build_output\POS.Presentation.exe
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
