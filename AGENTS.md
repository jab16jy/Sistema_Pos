# Agent Instructions — Sistema POS Local

Reglas universales para todos los agentes (Hermes, Claude Code, OpenCode, Codex, y cualquier agente futuro) que trabajen en este repositorio.

---

## 1. Memoria persistente del agente

La memoria principal de trabajo para este proyecto es la **memoria persistente del agente**, no Obsidian.

- **Engram es la fuente canónica** de memoria para este proyecto (`mem_save`, `mem_search`, `mem_context`).
- Si trabajás en sesión de Hermes, usa `memory()` + `session_search()` como equivalente — mismo rol, backend distinto.
- No uses el vault `docs md/` como memoria operativa del agente ni como sustituto de contexto persistente.

Reglas prácticas:
- Guarda de forma proactiva decisiones duraderas, convenciones, restricciones del usuario, bugs con causa raíz y hallazgos no obvios.
- Recupera contexto previo antes de pedir al usuario que repita decisiones ya tomadas.
- No guardes progreso temporal, logs de ejecución, PRs, commits ni estado efímero como si fueran memoria duradera.
- Cuando una decisión de proyecto deba ser visible para todos los agentes, refléjala también en `AGENTS.md` o en `openspec/`.

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

Si existe `.atl/skill-registry.md`, úsalo como caché local de resolución de skills para delegación y SDD. Ese archivo es auxiliar/local, no fuente de verdad del dominio.

---

## 5. Stack y convenciones

Ver skills del proyecto y specs en `openspec/specs/` para reglas de arquitectura, dominio e implementación.

Stack principal:
- C# / .NET 9
- WPF + MVVM
- Clean Architecture
- EF Core + SQLite
- ESC-POS

---

## 6. OpenSpec + SDD delegable

Este repositorio usa **OpenSpec** como artifact store de SDD.

- La configuración base vive en `openspec/config.yaml`.
- Las especificaciones vigentes viven en `openspec/specs/`.
- El trabajo activo vive en `openspec/changes/<change-name>/`.
- Los cambios archivados en `openspec/changes/archive/` son historial de auditoría y **no deben borrarse**.

Cuando un cambio sea no trivial:
- Sigue el flujo `explore → propose → spec → design → tasks → apply → verify → archive`.
- Mantén los artefactos SDD en **inglés**, alineados con `openspec/config.yaml`.
- Usa delegación para exploración amplia, implementaciones multiarchivo, pruebas, builds y revisiones adversariales.
- Antes de ejecutar `apply` o `verify`, revisa `openspec/config.yaml` para detectar reglas activas de testing, build y calidad.
- Respeta los límites de arquitectura del proyecto: la lógica de negocio no va en WPF y WPF no consulta SQLite directamente.

---

## 7. Cachés locales de sesión para agentes

Para reducir fricción entre sesiones y agentes, se permite mantener cachés locales en `.atl/` (directorio ignorado por git).

Usos esperados:
- `.atl/skill-registry.md` — índice local de skills de proyecto y skills compartidas para subagentes.
- `.atl/project-context.md` — contexto operativo local del repo (por ejemplo, rama base por defecto, artifact store SDD, comandos de verificación).

Reglas:
- Trata `.atl/` como caché regenerable, no como fuente canónica.
- Si una caché contradice `git`, `openspec/`, `AGENTS.md` o el código real, prevalece la fuente real.
- Actualiza la caché cuando cambien skills, rama base o convenciones operativas importantes.

### Caché de rama principal

Cuando un agente necesite planear PRs encadenados, comparar cambios, rebasear o definir una baseline de revisión:

- Detecta la rama principal desde git (`origin/HEAD`) y **cáchala para la sesión**.
- En este repositorio, la baseline actual esperada es **`main`**, salvo que git indique otra cosa.
- Reutiliza esa rama cacheada durante la sesión en vez de volver a preguntarla al usuario.
- Si el remoto cambia o el usuario lo corrige, invalida la caché y vuelve a detectarla.
