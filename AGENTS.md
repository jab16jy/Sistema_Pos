# Agent Instructions — Sistema POS Local

Reglas universales para todos los agentes (Claude Code, OpenCode, Codex, y cualquier agente futuro) que trabajen en este repositorio.

---

## 1. Memoria persistente: Engram

La fuente de memoria del agente es **Engram**. No uses Obsidian como base de conocimiento del agente ni como fuente principal del proyecto.

- Llama `mem_save` proactivamente tras decisiones, bugs corregidos, descubrimientos y convenciones.
- Llama `mem_search` al comenzar trabajo que puede haberse hecho antes.
- Llama `mem_context` al inicio de sesión para recuperar contexto.
- Llama `mem_session_summary` antes de cerrar sesión.

---

## 2. Documentación del proyecto: `docs md/`

`docs md/` es el vault de Obsidian vinculado a este proyecto. Contiene decisiones de arquitectura, archivos de diseño, notas de exploración y requerimientos.

**Regla no negociable:** Antes de realizar cambios importantes en el código, revisa los archivos relevantes en `docs md/` para entender contexto, decisiones y restricciones.

Carpetas clave:
- `docs md/docs/` — Inicio del proyecto, estrategia de UI
- `docs md/Exploracion/` — Requerimientos, módulos, matriz de dominio
- `docs md/fase_disenio_pos_md/` — Arquitectura, modelo de datos, flujos, wireframes, protocolo de pruebas

---

## 3. Reglas del vault Obsidian

**Interactúa con el vault solo cuando el usuario lo pida explícitamente** (ej: "crea una nota en Obsidian", "agrega un wikilink", "actualiza el vault").

Cuando trabajes con el vault:
- Respeta la estructura de carpetas existente, los templates, los tags y los esquemas de frontmatter.
- NO borres, muevas ni renombres archivos del vault sin instrucción explícita.
- Conoce y respeta estos plugins cuando aplique:
  - **Templater**: sintaxis de templates y variables
  - **QuickAdd**: flujos de captura y macros
  - **Dataview**: sintaxis de queries para vistas dinámicas
  - **Tasks**: sintaxis de tareas y campos de fecha
- Ubicación del vault: `docs md/` (raíz del proyecto)

> El objetivo futuro es usar Templater, QuickAdd y Dataview para mejorar la documentación del proyecto desde el agente, bajo petición explícita del usuario.

---

## 4. Skills del proyecto

Las skills locales del proyecto están en `.skills/`. Lee `.skills/README.md` para el índice.

Para la skill `jornada` (resumen de jornada de trabajo), ve `.skills/jornada/SKILL.md`.

---

## 5. Stack y convenciones

Ver skills del proyecto y specs en `openspec/specs/` para reglas de arquitectura, dominio e implementación. Stack principal: C# / .NET 9 / WPF / MVVM / Clean Architecture / EF Core / SQLite / ESC-POS.
