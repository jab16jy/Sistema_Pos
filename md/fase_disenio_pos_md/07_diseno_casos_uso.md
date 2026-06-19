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

---

# Caso de uso 8 — Crear cotización

| Elemento | Descripción |
|---|---|
| Actor | Vendedor / Cajero |
| Objetivo | Generar una propuesta comercial para un cliente sin afectar el inventario ni registrar una venta. |
| Precondición | El catálogo de productos está disponible y el usuario ha iniciado sesión. |
| Resultado | La cotización queda guardada con número consecutivo COT-YYYY-NNNN y no descuenta stock. |

```text
1. El vendedor entra al módulo Cotizaciones.
2. Presiona Nueva cotización.
3. Busca y agrega productos desde el catálogo.
4. Define la cantidad y el descuento de cada ítem.
5. Ingresa los datos del cliente: nombre, teléfono, correo y observaciones.
6. Define la vigencia de la oferta en días.
7. Confirma la cotización.
8. El sistema genera el número consecutivo COT-YYYY-NNNN.
9. El sistema guarda la cotización sin descontar stock.
10. El sistema ofrece opciones de impresión o envío en PDF.
```

---

# Caso de uso 9 — Convertir cotización a venta

| Elemento | Descripción |
|---|---|
| Actor | Vendedor |
| Objetivo | Transformar una cotización aceptada por el cliente en una venta registrada. |
| Precondición | La cotización existe y su estado es Active o Draft. No puede estar en estado Expired ni Converted. |
| Resultado | Se crea un registro de venta, se descuenta el inventario y la cotización pasa a estado Converted. |

```text
1. El vendedor busca y selecciona la cotización desde el historial.
2. El sistema verifica que el estado no sea Expired ni Converted.
3. El sistema verifica la disponibilidad de stock de cada ítem.
4. Si hay stock insuficiente, el sistema informa qué productos no tienen existencia suficiente.
5. El vendedor selecciona el método de pago.
6. El vendedor confirma la conversión.
7. El sistema crea el registro de venta con los datos de la cotización.
8. El sistema descuenta el inventario.
9. El sistema actualiza el estado de la cotización a Converted.
10. El sistema asigna el ID de la venta creada en el campo converted_sale_id.
```

---

# Caso de uso 10 — Consultar historial de cotizaciones

| Elemento | Descripción |
|---|---|
| Actor | Vendedor / Administrador |
| Objetivo | Buscar y revisar cotizaciones emitidas con filtros flexibles. |
| Precondición | Existen cotizaciones registradas en el sistema. |
| Resultado | El sistema muestra la lista paginada de cotizaciones que coinciden con los filtros aplicados. |

```text
1. El usuario entra al módulo Cotizaciones.
2. Selecciona la opción Historial.
3. Aplica filtros opcionales: rango de fechas, nombre del cliente, estado (Draft, Active, Expired, Converted).
4. El sistema muestra la lista paginada de resultados.
5. El usuario selecciona una cotización para ver su detalle completo.
6. El usuario puede imprimir o reenviar la cotización desde el detalle.
```

---

# Caso de uso 11 — Enviar documento por correo electrónico

| Elemento | Descripción |
|---|---|
| Actor | Sistema / Vendedor |
| Objetivo | Enviar una factura o cotización al correo electrónico del cliente en formato PDF. |
| Precondición | El documento existe, el cliente tiene correo registrado y la configuración SMTP está activa. |
| Resultado | El documento se envía al correo del cliente y el envío queda registrado en DocumentSendLog. |

```text
1. El usuario selecciona una factura o cotización.
2. El usuario elige la opción Enviar por correo.
3. El sistema verifica que el correo de destino sea válido.
4. El sistema genera el documento en formato PDF mediante PdfService.
5. El sistema envía el PDF adjunto mediante SMTP usando la configuración del negocio.
6. El sistema registra el resultado del envío en DocumentSendLog (canal: Email, éxito o error).
7. El sistema muestra confirmación del envío al usuario.
```

---

# Caso de uso 12 — Enviar documento por WhatsApp

| Elemento | Descripción |
|---|---|
| Actor | Sistema / Vendedor |
| Objetivo | Enviar una factura o cotización al número de WhatsApp del cliente. |
| Precondición | El documento existe, el cliente tiene teléfono registrado, el script externo está configurado y accesible. |
| Resultado | El documento se entrega al script externo para su envío y el intento queda registrado en DocumentSendLog. |

```text
1. El usuario selecciona una factura o cotización.
2. El usuario elige la opción Enviar por WhatsApp.
3. El sistema verifica que el número de teléfono de destino sea válido.
4. El sistema genera el documento en formato PDF mediante PdfService.
5. El sistema guarda el PDF en una ruta temporal.
6. El sistema ejecuta el script externo configurado (Process.Start), pasándole el número de teléfono y la ruta del PDF.
7. El sistema registra el resultado del intento en DocumentSendLog (canal: WhatsApp, éxito o error).
8. El sistema muestra el resultado al usuario.
```

## Resultado esperado

Estos casos de uso sirven como guía para programar los módulos y para validar que el sistema sea fácil de operar desde la perspectiva del usuario final.
