# 08 — Diseño de Integración de Módulos

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Explicar cómo se conectan los módulos funcionales del sistema POS para trabajar como una sola aplicación integrada.

## Módulos considerados

```text
01. Caja
02. Productos
03. Inventario
04. Clientes
05. Puntos
06. Proveedores
07. Pagos / Compras
08. Informe de Ventas
09. Facturas
10. Utilidades
11. Código de Barras
12. Cotizaciones
13. Comunicación Digital
```

## Mapa general de integración

```text
Productos ───────┐
                 ↓
Código de Barras → Caja → Ventas → Facturas → Reportes
                 ↓        ↓         ↓          ↓
Clientes → Puntos         Inventario           Utilidades
                 ↑        ↑
Proveedores → Compras ────┘

Productos ──────┐
                ↓
Clientes ──────→ Cotizaciones ──→ Ventas (conversión)
                ↓
        Comunicación Digital ←── Facturas
                ↓
        DocumentSendLog (auditoría de envíos)
```

## Integración por módulo

| Módulo | Se conecta con | Explicación |
|---|---|---|
| Caja | Productos, Clientes, Puntos, Facturas, Inventario, Reportes | Es el centro operativo de ventas. |
| Productos | Inventario, Caja, Código de Barras, Utilidades | Todo producto registrado puede venderse, etiquetarse y analizarse. |
| Inventario | Productos, Compras, Ventas, Devoluciones | Se actualiza automáticamente con entradas y salidas. |
| Clientes | Caja, Puntos, Facturas | Permite asociar compras y beneficios. |
| Puntos | Clientes, Caja | Calcula acumulación y redención. |
| Proveedores | Compras, Productos | Relaciona mercancía con su proveedor. |
| Compras | Proveedores, Inventario, Productos | Aumenta stock y registra gasto. |
| Informe de Ventas | Ventas, Pagos, Caja | Resume ingresos y medios de pago. |
| Facturas | Ventas, Clientes, Devoluciones, Impresión | Permite consultar y reimprimir. |
| Utilidades | Productos, Ventas | Calcula ganancia bruta. |
| Código de Barras | Productos, Caja | Genera etiquetas para facilitar venta rápida. |
| Cotizaciones | Productos, Clientes, Ventas | Lee el catálogo y los datos del cliente; convierte a venta cuando se confirma. |
| Comunicación Digital | Facturas, Cotizaciones | Genera PDFs y los entrega por correo (SMTP) o WhatsApp (script externo). Registra cada envío en DocumentSendLog. |

## Flujo integrado de una venta

```text
1. El producto se registra en Productos.
2. Opcionalmente se genera código de barras.
3. El cajero escanea el producto en Caja.
4. Caja consulta precio y stock.
5. Si hay cliente, se asocia para puntos.
6. Se registra el pago.
7. Se genera factura.
8. Se descuenta inventario.
9. Se actualizan reportes.
10. Se calcula utilidad con base en costo y precio de venta.
```

## Flujo integrado de una compra

```text
1. Se registra proveedor.
2. Se entra al módulo Compras.
3. Se seleccionan productos comprados.
4. Se guarda la compra.
5. El inventario aumenta automáticamente.
6. El gasto queda disponible para reportes.
```

## Integración de los módulos 12 y 13

### Módulo 12 — Cotizaciones

- **Lee de Productos:** el catálogo proporciona los productos disponibles para cotizar. Los precios y nombres se almacenan como instantáneas en `QuoteItem` en el momento de la emisión, de modo que un cambio posterior de precio no altere la cotización ya emitida.
- **Lee de Clientes:** los datos de contacto (nombre, teléfono, correo) se pueden obtener del directorio de clientes para precompletar el formulario, aunque la cotización también puede emitirse sin un cliente registrado.
- **Escribe en Ventas:** cuando el vendedor convierte una cotización, el módulo crea un registro completo en la tabla `SALES` y actualiza el estado de la cotización a `Converted`, enlazando ambos registros mediante `converted_sale_id`.

### Módulo 13 — Comunicación Digital

- **Lee de Facturas:** obtiene los datos de la venta (ítems, totales, cliente) para generar el PDF correspondiente antes de enviarlo.
- **Lee de Cotizaciones:** obtiene los datos de la propuesta comercial para generar el PDF de cotización antes de enviarlo.
- **Escribe en DocumentSendLog:** registra cada intento de envío (canal, destinatario, fecha, hora y resultado) para trazabilidad y auditoría. Este registro es inmutable: no se edita ni elimina.
- **Sin integración con Meta API:** el envío por WhatsApp se delega a un script externo configurable. El sistema ejecuta el script (ruta definida en `SystemConfiguration`) y registra el resultado reportado por el proceso.

## Enfoque didáctico de integración

| Situación | Diseño recomendado |
|---|---|
| Producto nuevo | Después de crearlo, mostrar: “Ya puedes venderlo en Caja”. |
| Compra registrada | Mostrar: “Inventario actualizado automáticamente”. |
| Cliente asociado | Mostrar: “Esta venta sumará puntos al cliente”. |
| Factura generada | Mostrar opción: “Reimprimir desde Facturas”. |
| Stock bajo | Mostrar enlace directo: “Ver inventario”. |

## Resultado esperado

La integración de módulos permite que el POS funcione como una herramienta única y coherente, donde cada acción alimenta otras áreas del sistema sin que el usuario tenga que repetir información.
