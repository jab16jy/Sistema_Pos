# 06 — Diseño de Wireframes e Interfaz Didáctica

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Diseñar pantallas preliminares que sean fáciles de entender, rápidas de operar y adecuadas para usuarios con poca experiencia tecnológica.

## Principios visuales

| Principio | Aplicación |
|---|---|
| Claridad | Botones grandes y textos directos. |
| Jerarquía | Lo más importante debe verse primero. |
| Consistencia | Todas las pantallas deben compartir estructura visual. |
| Aprendizaje rápido | Cada módulo debe explicar brevemente qué se hace allí. |
| Accesibilidad | Buen tamaño de letra, contraste y navegación con teclado. |

---

# Pantalla 1 — Login

```text
┌──────────────────────────────────────────────┐
│              SISTEMA POS LOCAL               │
│        Venta rápida, simple y segura         │
├──────────────────────────────────────────────┤
│ Usuario                                      │
│ [________________________________________]   │
│                                              │
│ Contraseña                                   │
│ [________________________________________]   │
│                                              │
│ [ Iniciar sesión ]                           │
│                                              │
│ Ayuda: Ingresa con el usuario asignado.      │
└──────────────────────────────────────────────┘
```

---

# Pantalla 2 — Menú principal

```text
┌────────────────────────────────────────────────────────────┐
│ POS LOCAL                              Usuario: Admin       │
├────────────────────────────────────────────────────────────┤
│ [Caja] [Productos] [Inventario] [Clientes] [Reportes]      │
│ [Proveedores] [Compras] [Facturas] [Utilidades] [Config.]  │
├────────────────────────────────────────────────────────────┤
│ Bienvenido. Selecciona una opción para comenzar.           │
│                                                            │
│ Acciones frecuentes:                                       │
│ F1 Caja  | F2 Productos | F3 Inventario | F4 Reportes      │
└────────────────────────────────────────────────────────────┘
```

---

# Pantalla 3 — Caja

```text
┌────────────────────────────────────────────────────────────┐
│ CAJA                                                       │
│ Escanea el código o escribe el nombre del producto         │
├────────────────────────────────────────────────────────────┤
│ Buscar producto: [____________________________________]    │
│ Sugerencias: Coca Cola 400ml | Pan tajado | Arroz 500g     │
├────────────────────────────────────────────────────────────┤
│ Producto           Cant.     Precio       Subtotal         │
│ Coca Cola 400ml    2         $3.000       $6.000           │
│ Pan tajado         1         $5.000       $5.000           │
├────────────────────────────────────────────────────────────┤
│ Subtotal: $11.000                                          │
│ Descuento: [ $0 ]                                          │
│ TOTAL A PAGAR:                         $11.000             │
├────────────────────────────────────────────────────────────┤
│ Método de pago: [Efectivo ▼]                               │
│ Recibido: [ $20.000 ]        Cambio: $9.000                │
│                                                            │
│ [Finalizar venta] [Dejar en espera] [Cancelar]             │
│                                                            │
│ Atajos: F5 Cobrar | F6 Cliente | F7 Espera | Esc Cancelar  │
└────────────────────────────────────────────────────────────┘
```

## Enfoque didáctico de Caja

- El campo de búsqueda debe estar activo automáticamente.
- El total debe verse grande.
- Los errores deben ser específicos: “No hay stock suficiente”.
- El usuario debe poder operar con teclado y escáner.

---

# Pantalla 4 — Productos

```text
┌────────────────────────────────────────────────────────────┐
│ PRODUCTOS                                                  │
│ Administra los productos que se venden en caja             │
├────────────────────────────────────────────────────────────┤
│ Buscar: [____________________] Categoría: [Todas ▼]        │
│ [Nuevo producto] [Importar CSV/Excel]                      │
├────────────────────────────────────────────────────────────┤
│ Código    Nombre        Categoría   Stock   Precio         │
│ 770001    Arroz 500g    Granos      25      $2.500         │
│ 770002    Aceite 1L     Aceites     4       $9.000         │
├────────────────────────────────────────────────────────────┤
│ Aviso: 1 producto está por agotarse. [Ver bajo stock]      │
└────────────────────────────────────────────────────────────┘
```

---

# Pantalla 5 — Inventario

```text
┌────────────────────────────────────────────────────────────┐
│ INVENTARIO                                                 │
│ Consulta cantidades, valor del inventario y movimientos    │
├────────────────────────────────────────────────────────────┤
│ Buscar producto: [________________] Categoría: [Todas ▼]   │
├────────────────────────────────────────────────────────────┤
│ Total productos: 320     Unidades: 5.430                   │
│ Valor inventario en costo: $8.500.000                      │
├────────────────────────────────────────────────────────────┤
│ Producto      Stock actual   Stock mínimo   Estado         │
│ Aceite 1L     4              10             Bajo stock     │
│ Arroz 500g    25             8              Normal         │
├────────────────────────────────────────────────────────────┤
│ Últimos movimientos:                                       │
│ - Salida por venta: Aceite 1L x2                           │
│ - Entrada por compra: Arroz 500g x20                       │
└────────────────────────────────────────────────────────────┘
```

---

# Pantalla 6 — Clientes

```text
┌────────────────────────────────────────────────────────────┐
│ CLIENTES                                                   │
│ Registra clientes, puntos e historial de compras           │
├────────────────────────────────────────────────────────────┤
│ Buscar por nombre o cédula: [_________________________]    │
│ [Nuevo cliente]                                            │
├────────────────────────────────────────────────────────────┤
│ Nombre            Cédula        Puntos       Última compra │
│ Ana Pérez         123456789     120          2026-06-10    │
└────────────────────────────────────────────────────────────┘
```

---

# Pantalla 7 — Reportes y cierre

```text
┌────────────────────────────────────────────────────────────┐
│ REPORTES Y CIERRE DE CAJA                                 │
│ Revisa ventas, pagos y efectivo físico                     │
├────────────────────────────────────────────────────────────┤
│ Rango: [Hoy ▼]  Desde: [fecha] Hasta: [fecha]              │
│ [Generar reporte] [Exportar PDF] [Exportar Excel]          │
├────────────────────────────────────────────────────────────┤
│ Total facturado:      $350.000                             │
│ Efectivo esperado:    $180.000                             │
│ Pagos digitales:      $170.000                             │
├────────────────────────────────────────────────────────────┤
│ Efectivo contado: [_____________]                          │
│ Diferencia: $0                                             │
│ [Cerrar caja]                                              │
└────────────────────────────────────────────────────────────┘
```

## Resultado esperado

Los wireframes permiten visualizar cómo será el sistema antes de programarlo. El diseño prioriza que el usuario entienda cada pantalla rápidamente y pueda realizar las tareas principales sin capacitación extensa.

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[01_diseno_integracion_tecnologica]] · [[05_diseno_flujos_funcionales]] · [[07_diseno_casos_uso]] · [[08_diseno_integracion_modulos]] · [[UI_STRATEGY]]
