# 07 — Diseño de Casos de Uso

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir las acciones principales que realizarán los usuarios del sistema POS, identificando actores, precondiciones, flujo principal y resultado esperado.

## Actores del sistema

| Actor | Descripción |
|---|---|
| Cajero | Usuario encargado de vender, cobrar, consultar productos y emitir recibos. |
| Administrador | Usuario con permisos para configurar, anular, crear usuarios, ver reportes y gestionar inventario. |
| Sistema | Ejecuta validaciones, guarda datos, descuenta inventario, imprime y genera reportes. |

---

# Caso de uso 1 — Iniciar sesión

| Elemento | Descripción |
|---|---|
| Actor | Administrador o Cajero |
| Objetivo | Permitir acceso seguro al sistema. |
| Precondición | El usuario debe estar registrado y activo. |
| Resultado | El usuario ingresa al menú según su rol. |

```text
1. El usuario abre la aplicación.
2. Ingresa usuario y contraseña.
3. El sistema valida credenciales.
4. El sistema registra la sesión.
5. El sistema muestra el menú principal.
```

---

# Caso de uso 2 — Registrar venta

| Elemento | Descripción |
|---|---|
| Actor | Cajero |
| Objetivo | Vender productos y registrar el pago. |
| Precondición | El cajero inició sesión y existen productos activos. |
| Resultado | La venta queda guardada, el inventario se actualiza y se imprime recibo. |

```text
1. El cajero entra al módulo Caja.
2. Busca o escanea productos.
3. Agrega productos al carrito.
4. Selecciona cliente si aplica.
5. Selecciona método de pago.
6. Ingresa monto recibido si es efectivo.
7. El sistema calcula cambio.
8. El cajero confirma la venta.
9. El sistema guarda la venta.
10. El sistema descuenta inventario.
11. El sistema imprime recibo.
```

## Excepciones

```text
- Producto no encontrado.
- Stock insuficiente.
- Pago menor al total.
- Error de impresora.
```

## Mensajes didácticos

- “Producto agregado al carrito.”
- “Solo hay 2 unidades disponibles.”
- “La venta fue guardada. Puedes imprimir el recibo nuevamente desde Facturas.”

---

# Caso de uso 3 — Crear producto

| Elemento | Descripción |
|---|---|
| Actor | Administrador |
| Objetivo | Agregar un nuevo producto al catálogo. |
| Resultado | El producto queda disponible para venderse en caja. |

```text
1. El administrador entra a Productos.
2. Presiona Nuevo producto.
3. Ingresa los datos obligatorios.
4. Escanea o genera código de barras si aplica.
5. Guarda el producto.
6. El sistema muestra confirmación.
```

---

# Caso de uso 4 — Registrar compra a proveedor

```text
1. El administrador entra a Compras.
2. Selecciona proveedor.
3. Agrega productos comprados.
4. Ingresa cantidades y costos.
5. El sistema calcula total.
6. El administrador confirma.
7. El sistema guarda compra y movimientos de inventario.
```

---

# Caso de uso 5 — Anular venta

```text
1. El usuario busca la factura.
2. Selecciona Anular.
3. Ingresa motivo.
4. El sistema solicita contraseña de administrador.
5. El sistema confirma la anulación.
6. El sistema actualiza inventario y registra auditoría.
```

---

# Caso de uso 6 — Cerrar caja

```text
1. El usuario abre Cierre de caja.
2. El sistema muestra efectivo esperado y pagos digitales.
3. El usuario ingresa efectivo contado.
4. El sistema calcula diferencia.
5. El usuario confirma.
6. El sistema guarda el cierre.
```

---

# Caso de uso 7 — Generar reporte

```text
1. El administrador entra a Reportes.
2. Selecciona tipo de reporte.
3. Elige fecha o rango.
4. Presiona Generar.
5. El sistema muestra resultados.
6. El usuario exporta si lo necesita.
```

## Resultado esperado

Estos casos de uso sirven como guía para programar los módulos y para validar que el sistema sea fácil de operar desde la perspectiva del usuario final.
