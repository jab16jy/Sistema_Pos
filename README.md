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

## Development environment

Build and run on **Windows** with the .NET Desktop SDK / Visual Studio (required for WPF).

Domain, Application, and Infrastructure tests run on any OS:

```bash
dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj
dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj
dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj
```

## Project-specific skills

Read `.skills/README.md` before implementation. The local skills encode the architecture, domain, persistence, UI, printing, reporting, testing, and didactic usability rules extracted from the project documentation.
