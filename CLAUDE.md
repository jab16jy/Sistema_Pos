# Sistema POS — Claude Code / OpenCode Instructions

Este archivo complementa las instrucciones globales de Engram + SDD.
Se usa junto con `~/.claude/CLAUDE.md` (global) y `AGENTS.md` (portable).

---

## 1. Memoria persistente

**Engram es la fuente canónica de memoria para este proyecto.**
El protocolo Engram completo está en `~/.claude/CLAUDE.md`.

Reglas de memoria para SISTEMA_POS:
- **Cada sesión** comienza con `mem_context` para recuperar contexto previo.
- **Cada tarea** verifica si requiere `mem_search` antes de preguntar al usuario.
- **Cada decisión**, bug corregido, descubrimiento o convención → `mem_save` proactivo.
- **Cada cierre de sesión** con trabajo sustancial → `mem_session_summary`.
- Usa `mem_suggest_topic_key` cuando no estés seguro del `topic_key`.
- Para este proyecto, los `topic_key` predecibles son:
  - `sistema_pos/architecture/{decision}` — decisiones de arquitectura
  - `sistema_pos/bugfix/{desc}` — bugs corregidos con causa raíz
  - `sistema_pos/convention/{desc}` — convenciones establecidas
  - `sistema_pos/discovery/{desc}` — hallazgos no obvios

---

## 2. SDD + OpenSpec

Este repositorio usa **OpenSpec** como artifact store de SDD.

- Configuración base: `openspec/config.yaml`
- Especificaciones vigentes: `openspec/specs/`
- Cambio activo actual: `openspec/changes/wireframe-to-wpf-xaml/`
- Cambios archivados en `openspec/changes/archive/` — **no borrar**

Flujo SDD: `explore → propose → spec → design → tasks → apply → verify → archive`

Cuando el artifact store del SDD sea `engram` (por defecto), los artefactos se escriben tanto en Engram como en `openspec/`. Los `topic_key` de Engram siguen el formato `sdd/{change-name}/{artifact}`.

---

## 3. Proyecto

**Stack:**
- C# / .NET 9 / WPF / MVVM / Clean Architecture / EF Core / SQLite / ESC-POS

**Skills locales del proyecto:** `.skills/` (ver `.skills/README.md`)

**Documentación:** `docs md/` (vault Obsidian)
- `docs md/docs/` — inicio del proyecto, estrategia de UI
- `docs md/Exploracion/` — requerimientos, módulos, matriz de dominio
- `docs md/fase_disenio_pos_md/` — arquitectura, modelo de datos, flujos, wireframes

**Regla no negociable:** antes de cambios importantes, revisa `docs md/` primero.
**Interactúa con el vault Obsidian solo cuando el usuario lo pida.**

---

## 4. Caché local `.atl/`

El directorio `.atl/` está ignorado por git y contiene cachés regenerables:
- `.atl/skill-registry.md` — índice local de skills para delegación y SDD
- `.atl/project-context.md` — contexto operativo local (rama base, comandos, defaults)

Trata `.atl/` como **caché regenerable**, no como fuente canónica.

---

## 5. Rama base

- La rama principal remota es `main` (`origin/HEAD`).
- Cáchala para la sesión al planear PRs encadenados o definir baseline de revisión.
- Si el remoto cambia, invalida la caché y vuelve a detectar.

---

## 6. Convenciones operativas

- Artefactos SDD en **inglés**.
- Commits siguen **Conventional Commits**: `type(scope): desc`
- Límite de revisión: **400 líneas** por PR (los cambios mayores van encadenados)
- Test runner: `dotnet test POS.sln --no-restore`
- Build: `dotnet build POS.sln --no-restore`
- Formato: `dotnet format POS.sln --verify-no-changes`
