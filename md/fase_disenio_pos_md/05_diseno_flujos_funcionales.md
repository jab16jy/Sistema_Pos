# 05 — Diseño de Flujos Funcionales

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir los recorridos principales que seguirá el usuario dentro del sistema POS. Cada flujo debe ser simple, guiado y con mensajes claros para que el usuario aprenda rápidamente.

---

# Flujo 1 — Inicio de sesión

## Actor

Administrador o Cajero.

## Pasos

```text
1. El usuario abre el sistema.
2. El sistema muestra la pantalla de inicio de sesión.
3. El usuario ingresa usuario y contraseña.
4. El sistema valida las credenciales.
5. El sistema identifica el rol.
6. El sistema abre el menú principal.
```

## Mensajes didácticos

- “Ingresa tu usuario y contraseña para continuar.”
- “Usuario o contraseña incorrectos. Revisa e intenta nuevamente.”
- “Bienvenido. Selecciona un módulo para comenzar.”

---

# Flujo 2 — Venta en caja

## Actor

Cajero.

## Pasos

```text
1. El cajero entra al módulo Caja.
2. El sistema muestra el campo “Escanea o busca un producto”.
3. El cajero escanea o escribe el nombre del producto.
4. El sistema muestra coincidencias.
5. El cajero selecciona producto y cantidad.
6. El sistema agrega el producto al carrito.
7. El sistema calcula subtotal, descuento, impuesto y total.
8. El cajero selecciona método de pago.
9. Si es efectivo, ingresa monto recibido.
10. El sistema calcula el cambio.
11. El cajero presiona “Finalizar venta”.
12. El sistema guarda la venta, descuenta inventario e imprime recibo.
```

## Ayudas visuales recomendadas

- Campo principal grande: **“Escanea o escribe el producto”**.
- Total visible en tamaño grande.
- Botón principal resaltado: **“Finalizar venta”**.
- Mensaje final: **“Venta completada correctamente”**.

---

# Flujo 3 — Registrar producto

## Actor

Administrador.

## Pasos

```text
1. El administrador entra al módulo Productos.
2. Presiona “Nuevo producto”.
3. Ingresa nombre, categoría, costo, precio de venta, stock y stock mínimo.
4. Opcionalmente escanea o genera código de barras.
5. El sistema valida los datos.
6. El sistema guarda el producto.
7. El producto aparece en el listado.
```

## Mensajes didácticos

- “El nombre del producto es obligatorio.”
- “El precio de venta debe ser mayor que cero.”
- “Producto guardado. Ya puede venderse en caja.”

---

# Flujo 4 — Registrar compra a proveedor

## Actor

Administrador.

## Pasos

```text
1. El administrador entra al módulo Compras.
2. Selecciona proveedor.
3. Agrega productos comprados.
4. Ingresa cantidad y costo.
5. El sistema calcula el total de compra.
6. El administrador confirma.
7. El sistema registra la compra.
8. El sistema aumenta el stock.
9. El movimiento queda en historial de inventario.
```

## Enfoque didáctico

La pantalla debe mostrar una guía tipo:

```text
Paso 1: Selecciona proveedor
Paso 2: Agrega productos
Paso 3: Confirma la compra
```

---

# Flujo 5 — Cierre de caja

## Actor

Administrador o Cajero autorizado.

## Pasos

```text
1. El usuario entra a Reportes o Cierre de Caja.
2. El sistema calcula ventas del día.
3. El sistema separa efectivo y pagos digitales.
4. El usuario ingresa efectivo contado físicamente.
5. El sistema calcula diferencia.
6. El usuario confirma cierre.
7. El sistema guarda el cierre y genera reporte.
```

## Mensajes didácticos

- “Cuenta el efectivo físico y escríbelo aquí.”
- “El sistema esperaba $150.000 en efectivo.”
- “Diferencia detectada: $5.000. Revisa antes de cerrar.”

---

# Flujo 6 — Devolución o anulación

## Actor

Administrador.

## Pasos

```text
1. El usuario busca la factura.
2. Selecciona “Anular” o “Devolver producto”.
3. El sistema solicita motivo.
4. El sistema solicita contraseña de administrador.
5. El sistema revierte inventario si corresponde.
6. La factura cambia de estado.
7. El sistema guarda trazabilidad.
```

## Reglas de facilidad de uso

- Explicar claramente la consecuencia antes de confirmar.
- Mostrar productos que volverán al inventario.
- Evitar botones ambiguos como “OK”; usar “Confirmar devolución”.

---

# Flujo 7 — Generar reporte

## Actor

Administrador.

## Pasos

```text
1. El administrador entra a Reportes.
2. Selecciona tipo de reporte.
3. Escoge fecha o rango de fechas.
4. Presiona “Generar reporte”.
5. El sistema muestra resumen visual.
6. El usuario puede exportar a PDF o Excel.
```

## Diseño didáctico

- Usar tarjetas: “Total vendido”, “Efectivo en caja”, “Pagos digitales”, “Productos más vendidos”.
- Usar filtros simples: Hoy, Esta semana, Este mes, Personalizado.

---

# Flujo 8 — Backup y restauración

## Actor

Administrador.

## Pasos

```text
1. El administrador entra a Configuración.
2. Selecciona “Copia de seguridad”.
3. El sistema muestra dónde se guardará el archivo.
4. El usuario confirma.
5. El sistema crea el backup.
6. Se muestra mensaje de éxito.
```

## Mensajes didácticos

- “La copia de seguridad protege tus ventas, productos e inventario.”
- “Guarda este archivo en un lugar seguro.”

## Resultado esperado

Estos flujos permiten diseñar un sistema donde el usuario sabe siempre dónde está, qué debe hacer y qué sucederá después de cada acción.
