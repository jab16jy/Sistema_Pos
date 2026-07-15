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

> [!NOTE]
> **Políticas de Devolución y Validación de Permisos:**
> - **Estado de Devolución:** Una venta (`Sale`) solo cambia su estado a `Returned` cuando se ha completado una devolución del **100% de la cantidad** de todos los ítems de la venta. Las devoluciones parciales no cambian el estado principal de la venta a `Returned` (se mantiene en su estado original `Completed`).
> - **Validación de Roles:** La anulación o devolución iniciada por el cajero se valida directamente en la sesión actual comprobando si su rol cuenta con el permiso `Invoices.Cancel`, evitando solicitar contraseñas en la interfaz del usuario.

## Actor

Administrador o Cajero autorizado (con permiso `Invoices.Cancel`).

## Pasos

```text
1. El usuario busca la venta (factura) en el historial de ventas.
2. Selecciona “Registrar Devolución” (parcial o total) o “Anular Venta”.
3. El sistema valida los permisos del rol en la sesión activa (requiere permiso `Invoices.Cancel`) en lugar de solicitar contraseña en la interfaz.
4. Si el rol del usuario actual no cuenta con el permiso, se rechaza la operación y se muestra un mensaje didáctico.
5. Si el permiso es válido, el usuario especifica la cantidad a devolver de cada ítem y escribe el motivo de la devolución.
6. El sistema procesa la devolución de forma atómica:
   a. Registra la transacción de devolución en SALE_RETURNS y SALE_RETURN_ITEMS.
   b. Crea un movimiento de entrada al inventario (INVENTORY_MOVEMENTS) por la cantidad de productos devueltos.
   c. Si la suma total acumulada de unidades devueltas es igual al 100% de la cantidad original de la venta, actualiza el estado de la venta a "Returned". De lo contrario, conserva su estado original.
7. El sistema calcula y muestra el monto exacto a reembolsar, registrando la fecha (ReturnedAt) y el cajero procesador (CashierId).
```

## Reglas de facilidad de uso

- **Claridad de Impacto:** Explicar claramente la consecuencia antes de confirmar (por ejemplo: "Se reembolsarán $20.000 y se reingresarán 2 unidades al inventario").
- **Visualización de Stock:** Mostrar explícitamente los productos que volverán al inventario y sus cantidades.
- **Acciones Claras:** Evitar botones ambiguos como “OK” o “Cancelar”; usar “Confirmar devolución” o “Cancelar operación”.

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

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[03_diseno_modelo_dominio]] · [[07_diseno_casos_uso]] · [[08_diseno_integracion_modulos]] · [[requerimientos]]
