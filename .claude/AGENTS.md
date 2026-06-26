# Claude Code — Instrucciones del proyecto Sistema POS Local

Estas reglas extienden `AGENTS.md` (raíz del proyecto) con detalles específicos para Claude Code.

---

## Memoria persistente

Usa las herramientas MCP de Engram (`mem_save`, `mem_search`, `mem_context`, `mem_session_summary`) según el protocolo definido en el CLAUDE.md global del usuario. Esto es obligatorio y siempre está activo.

---

## Puerta de documentación

Antes de cualquier trabajo de implementación, verifica si existen docs relevantes en `docs md/`. Si existen, léelos antes de escribir código. Las carpetas clave:

- `docs md/Exploracion/` — Requerimientos y módulos del sistema
- `docs md/fase_disenio_pos_md/` — Diseño técnico por capa y módulo

---

## Skill jornada

La skill `jornada` es obligatoria en este proyecto. Actívala cuando el usuario diga:

- `jornada` / `jornada de hoy`
- `jornada del módulo <X>`
- `qué cambios hicimos` / `qué cambios hicimos ayer`
- `resumen de cambios`
- `bitácora de la app`

La skill está en `.skills/jornada/SKILL.md`. Léela antes de ejecutar.

---

## Obsidian: solo bajo pedido explícito

No consultes, modifiques ni crees archivos en `docs md/` como parte del flujo normal de trabajo, a menos que el usuario lo pida de forma explícita. El vault es documentación de referencia, no un sistema operativo del agente.
