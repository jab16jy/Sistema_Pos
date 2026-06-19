---
name: pos-avalonia-prototype
description: "Trigger: Avalonia, prototype UI, Linux desktop, MangoWM, future WPF. Build cross-platform POS UI without business logic."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Use this skill when editing the Avalonia prototype UI or shared presentation layer for the POS.

## Hard Rules

- Avalonia is the executable Linux/MangoWM prototype UI, not the business layer.
- Keep business rules in `POS.Domain` and use cases in `POS.Application`.
- Put reusable ViewModels, commands, and UI state in `POS.Presentation.Shared`.
- Put Avalonia-specific XAML, styles, windows, app startup, and platform behavior in `POS.Presentation.Avalonia`.
- Do not call SQLite, EF Core, repositories, printing, reports, or backup services directly from Avalonia views.
- Preserve the option to build a future `POS.Presentation.Wpf` using the shared ViewModels.

## Decision Gates

| Situation | Action |
| --- | --- |
| Needs to run on Linux/MangoWM now | Implement in Avalonia. |
| Must be reusable by future WPF | Move it to `POS.Presentation.Shared`. |
| Requires domain calculation or persistence | Add or call an Application use case. |
| Requires Avalonia styling/control behavior | Keep it in `POS.Presentation.Avalonia`. |

## Execution Steps

1. Load `pos-clean-architecture`, `pos-didactic-usability`, and this skill.
2. Design screens around documented POS tasks, not database tables.
3. Bind Avalonia views to shared ViewModels.
4. Add tests for shared ViewModel state and command behavior.
5. Verify with `dotnet build src/POS.Presentation.Avalonia/POS.Presentation.Avalonia.csproj` and shared tests.

## Output Contract

Report whether the UI remains a prototype, what is runnable on Linux, and what still belongs to WPF/future production UI.

## References

- `md/fase_disenio_pos_md/06_diseno_wireframes_interfaz.md`
- `md/fase_disenio_pos_md/05_diseno_flujos_funcionales.md`
- `README.md`
