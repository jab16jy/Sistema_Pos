# exception-hardening

Usa esta skill cuando tengas que auditar o endurecer excepciones en Sistema POS.

## Quick path

1. Ejecuta `pwsh -File .\scripts\exception-audit.ps1`.
2. Prioriza `bare-catch`, `async void`, `fire-and-forget` y `GetAwaiter().GetResult()`.
3. Corrige primero sin romper MVVM ni Clean Architecture.
4. Agrega test de regresión en `tests/POS.Presentation.Shared.Tests/` cuando el cambio toque ViewModels compartidos.
5. Verifica con `dotnet test POS.sln --no-restore`.

## Qué revisar

| Riesgo | Qué buscar | Respuesta esperada |
|---|---|---|
| Catch silencioso | `catch { }` | Reemplazar por manejo explícito o documentar best-effort |
| Task no observada | `_ = AlgoAsync()` | Envolver con `RunSafe(...)` o comando asíncrono seguro |
| Evento UI frágil | `async void` | Mantener solo en eventos y proteger con `try/catch` |
| Bloqueo UI | `.GetAwaiter().GetResult()` / `.Result` | Migrar a flujo asíncrono seguro |
| Shell WPF | `Close`, `ShowDialog`, `ShutdownMode` | Validar orden de cierre/reapertura |

## Reglas del proyecto

- La UI WPF muestra mensajes recuperables; no metas lógica de negocio allí.
- `POS.Presentation.Shared` concentra ViewModels reutilizables.
- Si el error es opcional/mejor-esfuerzo, deja evidencia clara del motivo.
- Si una dependencia opcional falla al construir un módulo, no la silencies sin dejar señal operativa.

## Verificación

- `dotnet test POS.sln --no-restore`
- Si el cambio toca composición del shell o logout, revisa también el flujo manual de inicio/cierre de sesión.
