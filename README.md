# Sistema POS Local

Local desktop POS for small businesses, designed from the project documentation in `md/`.

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
  POS.Presentation/
tests/
  POS.Domain.Tests/
  POS.Application.Tests/
```

## Current environment note

This repository is currently on EndeavourOS/Arch with .NET SDK `9.0.118` installed from the official Arch repositories.

Verified commands:

```bash
dotnet --info
dotnet restore POS.sln
dotnet build src/POS.Infrastructure/POS.Infrastructure.csproj --no-restore
dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore
dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore
```

Important: the Arch source-built .NET SDK does not include `Microsoft.NET.Sdk.WindowsDesktop`, so the WPF project (`POS.Presentation`) does not compile on this Linux environment. Build and run the WPF desktop app on Windows with the .NET Desktop SDK/Visual Studio, or use this Linux environment for Domain/Application/Infrastructure development and tests.

## Project-specific skills

Read `.skills/README.md` before implementation. The local skills encode the architecture, domain, persistence, UI, printing, reporting, testing, and didactic usability rules extracted from the Markdown documentation.


## Avalonia prototype UI

The runnable Linux/MangoWM prototype lives in `src/POS.Presentation.Avalonia` and uses shared ViewModels from `src/POS.Presentation.Shared`.

Run it with:

```bash
dotnet run --project src/POS.Presentation.Avalonia/POS.Presentation.Avalonia.csproj
```

The WPF project remains as the Windows-oriented final UI option. See `docs/UI_STRATEGY.md` for the rules that prevent business logic from leaking into the prototype UI.
