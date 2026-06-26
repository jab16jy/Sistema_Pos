---
name: jornada
description: Genera un resumen de la jornada de trabajo del día, lo escribe en el vault Obsidian en docs md/jornada/ y muestra el resultado en el chat. Combina Engram y Git como fuentes.
triggers:
  - jornada
  - jornada de hoy
  - jornada del módulo
  - qué cambios hicimos
  - qué cambios hicimos ayer
  - resumen de cambios
  - bitácora de la app
---

# Skill: Jornada

Genera el reporte de jornada de trabajo del proyecto Sistema POS Local, lo escribe como nota en el vault Obsidian (`docs md/jornada/`) y muestra el resultado en el chat.

Responde **siempre en español**.

---

## Cuándo activar

Activa esta skill cuando el usuario escriba cualquiera de:
- `jornada`, `jornada de hoy`, `jornada del módulo <X>`
- `qué cambios hicimos`, `qué cambios hicimos ayer`
- `resumen de cambios`, `bitácora de la app`

---

## Paso 1 — Recopilar información

### Engram (memoria persistente)

```
mem_context(project: "sistema_pos")
mem_search(query: "decisión cambio bug descubrimiento", project: "sistema_pos")
```

Si el usuario pide "ayer" o un módulo específico:
- `mem_search(query: "<módulo>", project: "sistema_pos")`
- Filtra por fecha si el resultado incluye timestamp.

### Git

```bash
# Commits del día actual
git log --since="today 00:00" --until="tomorrow 00:00" --oneline --no-merges

# Si pide "ayer":
git log --since="yesterday 00:00" --until="today 00:00" --oneline --no-merges

# Archivos modificados respecto al commit anterior
git diff --name-status HEAD~1 HEAD

# Estado actual (cambios no commiteados)
git status --short

# Diff estadístico del día
git diff --stat HEAD~1 HEAD 2>/dev/null || git diff --cached --stat
```

---

## Paso 2 — Construir el contenido del reporte

Construye el reporte con esta estructura. Si una sección no tiene datos, escribe `Sin evidencia suficiente.`

```
### Resumen ejecutivo
[1-3 oraciones sobre qué se hizo en la jornada]

### Cambios en la app
- <cambio funcional o técnico>: `<archivo o módulo>`

### Archivos y módulos afectados
- `<path/al/archivo>` — <qué cambió>

### Decisiones relevantes
- <decisión> — Por qué: <motivo>

### Bugs
**Corregidos:**
- <bug> (root cause: <causa>)

**Pendientes:**
- <bug conocido>

### Próximos pasos
- [ ] <tarea pendiente>
```

---

## Paso 3 — Escribir la nota en el vault Obsidian

**Ubicación obligatoria:** `docs md/jornada/<nombre-del-archivo>.md`

### Nombre del archivo

| Caso | Nombre del archivo |
|---|---|
| Jornada del día actual | `YYYY-MM-DD.md` |
| Jornada de ayer | `YYYY-MM-DD.md` (fecha de ayer) |
| Jornada de módulo específico | `YYYY-MM-DD <modulo>.md` |

Ejemplo: `2026-06-26.md`, `2026-06-26 ventas.md`

### Frontmatter obligatorio (compatible con Dataview)

```yaml
---
fecha: YYYY-MM-DD
tipo: jornada
modulo: <nombre del módulo o "general">
tags:
  - jornada
  - pos
  - <modulo-si-aplica>
---
```

### Estructura completa de la nota

```markdown
---
fecha: YYYY-MM-DD
tipo: jornada
modulo: general
tags:
  - jornada
  - pos
---

# Jornada — YYYY-MM-DD
[Si es módulo: # Jornada — Módulo <X> — YYYY-MM-DD]

### Resumen ejecutivo
...

### Cambios en la app
...

### Archivos y módulos afectados
...

### Decisiones relevantes
...

### Bugs
**Corregidos:**
...

**Pendientes:**
...

### Próximos pasos
...

---

*Generado por skill jornada — fuentes: Engram + Git*
```

### Si la carpeta `docs md/jornada/` no existe

Créala antes de escribir la nota. No es un error, es el primer uso de la skill.

### Si ya existe una nota para esa fecha

Pregunta al usuario si desea sobreescribirla o agregarla como una segunda entrada en el mismo archivo.

---

## Paso 4 — Respuesta en el chat

Después de escribir el archivo, muestra en el chat:

```
Jornada guardada en `docs md/jornada/<archivo>.md`.

## Jornada — <fecha>

[contenido del reporte]
```

---

## Comportamiento cuando no hay evidencia

Si Engram no tiene observaciones del período Y Git no muestra commits relevantes, de todas formas escribe la nota en el vault con este contenido:

```markdown
---
fecha: YYYY-MM-DD
tipo: jornada
modulo: general
tags:
  - jornada
  - pos
---

# Jornada — YYYY-MM-DD

Sin evidencia suficiente en Engram ni en Git para este período.

- Engram: no se encontraron observaciones para la fecha solicitada.
- Git: no hay commits en el rango indicado.
```

Y en el chat:
```
Jornada guardada en `docs md/jornada/<archivo>.md`.
Sin evidencia suficiente en Engram ni en Git para este período.
¿Querés revisar un rango diferente o un módulo específico?
```

No inventes cambios. No asumas. Solo reporta lo que existe en las fuentes.

---

## Reglas generales

- Idioma: español siempre.
- Tono: directo y profesional, sin emojis.
- El frontmatter de la nota debe estar siempre presente — es la base para queries Dataview futuras.
- No borres notas existentes de `docs md/jornada/` — pregunta antes de sobreescribir.
- El tag `jornada` es obligatorio en todas las notas generadas por esta skill.
- Si el módulo es conocido (ventas, inventario, clientes, etc.), agréga su tag correspondiente.
