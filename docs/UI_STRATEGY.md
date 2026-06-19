# UI Strategy — Avalonia Prototype, WPF Option

## Decision

Use Avalonia as the runnable prototype UI on Linux/MangoWM while preserving the option to create a WPF UI later if the client requires it.

## Why

The original design documentation specifies WPF, but the current development environment is Linux. WPF requires WindowsDesktop SDK support and does not build/run here. Avalonia lets the team validate desktop flows, keyboard/scanner-oriented UX, and MVVM structure now without changing Domain, Application, or Infrastructure.

## Architecture

```text
POS.Presentation.Avalonia -> POS.Presentation.Shared -> POS.Application -> POS.Domain
POS.Infrastructure -> POS.Application/POS.Domain contracts
```

Rules:

- Avalonia views are UI only.
- Shared ViewModels contain presentation state, commands, and user-facing messages.
- Business rules stay in Domain/Application.
- Persistence, printing, reporting, and backup stay in Infrastructure.
- A future WPF project should reuse `POS.Presentation.Shared` instead of copying UI logic.

## Current status

The Avalonia prototype opens the main POS shell and a Caja-oriented visual flow. It intentionally does not persist sales, update inventory, print receipts, or call SQLite yet.

Run on Linux:

```bash
dotnet run --project src/POS.Presentation.Avalonia/POS.Presentation.Avalonia.csproj
```

Validate shared presentation logic:

```bash
dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj
```
