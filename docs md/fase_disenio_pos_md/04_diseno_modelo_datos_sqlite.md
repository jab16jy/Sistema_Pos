# 04 — Diseño del Modelo de Datos SQLite

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Diseñar una base de datos local en SQLite que permita almacenar usuarios, productos, ventas, inventario, clientes, proveedores, compras, facturas, reportes y configuración del sistema.

## Criterios del diseño

- La base de datos debe funcionar sin internet.
- Las operaciones de caja deben ser rápidas.
- Las ventas deben guardarse de forma atómica.
- El inventario debe actualizarse automáticamente.
- La información debe permitir generar reportes claros.
- La estructura debe ser entendible para mantenimiento futuro.

## Tablas principales

### USERS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador del usuario. |
| Username | TEXT | Nombre de acceso. |
| PasswordHash | TEXT | Contraseña protegida con hash. |
| Role | TEXT | Administrador o Cajero. |
| IsActive | BOOLEAN | Indica si puede ingresar. |
| CreatedAt | DATETIME | Fecha de creación. |

### PRODUCTS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador del producto. |
| Barcode | TEXT | Código de barras o código interno. |
| Name | TEXT | Nombre del producto. |
| Description | TEXT | Descripción opcional. |
| CategoryId | FK | Categoría. |
| UnitOfMeasureId | FK | Unidad de medida. |
| CostPrice | DECIMAL | Precio de costo. |
| SalePrice | DECIMAL | Precio de venta. |
| CurrentStock | DECIMAL | Existencia actual. |
| MinimumStock | DECIMAL | Límite para alerta. |
| IsActive | BOOLEAN | Producto activo o inactivo. |

### SALES

> [!NOTE]
> **Campos de Totales Consolidados Dinámicos (Enfoque YAGNI/Ponytail):**
> Para evitar inconsistencias de redondeo y redundancia de datos, los campos consolidados como `Subtotal`, `Discount`, `Tax` y `Total` no se almacenan físicamente en la tabla `SALES`. En su lugar, el sistema los calcula dinámicamente en memoria sumando la información detallada en `SALE_ITEMS` y aplicando los descuentos correspondientes a nivel de aplicación.

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador de venta. |
| InvoiceNumber | TEXT | Número de factura. |
| CustomerId | FK NULL | Cliente asociado, si aplica. |
| UserId | FK | Cajero que realizó la venta. |
| Status | TEXT | Completed, Cancelled, OnHold. |
| CreatedAt | DATETIME | Fecha de venta. |

### SALE_ITEMS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador. |
| SaleId | FK | Venta relacionada. |
| ProductId | FK | Producto vendido. |
| Quantity | DECIMAL | Cantidad vendida. |
| UnitPrice | DECIMAL | Precio aplicado. |
| Discount | DECIMAL | Descuento por ítem. |
| Subtotal | DECIMAL | Resultado de cantidad × precio. |

### SALE_RETURNS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | TEXT (UUID) | Clave primaria. |
| OriginalSaleId | FK | Relación con la venta original (`SALES.Id`). |
| ReturnNumber | TEXT | Número de devolución único auto-generado (ej: `DEV-YYYY-NNNN`). |
| CashierId | FK | Cajero que procesó la devolución (`USERS.Id`). |
| ReturnedAt | TEXT (ISO 8601) | Fecha y hora de procesamiento de la devolución. |
| TotalRefundAmount | REAL | Monto total reembolsado. |
| Reason | TEXT | Razón o motivo de la devolución. |

* **Índice Recomendado:**
  `idx_sale_returns_original_sale_id` en `OriginalSaleId` con propiedad `IsUnique(false)`. Esto optimiza la búsqueda de múltiples devoluciones parciales permitidas para una sola venta.

### SALE_RETURN_ITEMS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | TEXT (UUID) | Clave primaria. |
| SaleReturnId | FK | Relación con la devolución (`SALE_RETURNS.Id`). |
| SaleItemId | FK | Relación con el ítem de la venta original (`SALE_ITEMS.Id`). |
| ProductId | FK | Producto devuelto (`PRODUCTS.Id`). |
| ReturnedQuantity | REAL | Cantidad devuelta de este ítem. |
| RefundAmount | REAL | Monto de reembolso asignado a este ítem. |

### PAYMENTS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador. |
| SaleId | FK | Venta relacionada. |
| PaymentMethod | TEXT | Efectivo, tarjeta, transferencia, mixto. |
| Amount | DECIMAL | Valor pagado. |
| ReceivedAmount | DECIMAL NULL | Monto recibido en efectivo. |
| ChangeAmount | DECIMAL NULL | Cambio devuelto. |
| BankOrPlatform | TEXT NULL | Nequi, Bancolombia, Daviplata, etc. |

### INVENTORY_MOVEMENTS

| Campo | Tipo | Descripción |
|---|---|---|
| Id | GUID/INTEGER | Identificador. |
| ProductId | FK | Producto afectado. |
| MovementType | TEXT | Entrada, salida, ajuste, devolución. |
| Quantity | DECIMAL | Cantidad movida. |
| Reason | TEXT | Motivo del movimiento. |
| ReferenceId | TEXT NULL | Venta, compra o devolución relacionada. |
| UserId | FK | Usuario responsable. |
| CreatedAt | DATETIME | Fecha del movimiento. |

### QUOTES

| Campo | Tipo | Descripción |
|---|---|---|
| id | TEXT (UUID) | Clave primaria. |
| quote_number | TEXT | Número consecutivo COT-YYYY-NNNN, único. |
| customer_id | TEXT | FK → CUSTOMERS, nullable. |
| customer_name | TEXT | Nombre del cliente al momento de la emisión. |
| customer_phone | TEXT | Teléfono del cliente, nullable. |
| customer_email | TEXT | Correo del cliente, nullable. |
| observations | TEXT | Observaciones adicionales, nullable. |
| validity_days | INTEGER | Días de vigencia de la cotización. |
| status | INTEGER | 1 = Draft, 2 = Active, 3 = Expired, 4 = Converted. |
| created_at | TEXT (ISO 8601) | Fecha y hora de creación. |
| expires_at | TEXT (ISO 8601) | Fecha de vencimiento de la oferta. |
| converted_sale_id | TEXT | FK → SALES, nullable. Se asigna cuando la cotización es convertida a venta. |

### QUOTE_ITEMS

| Campo | Tipo | Descripción |
|---|---|---|
| id | TEXT (UUID) | Clave primaria. |
| quote_id | TEXT | FK → QUOTES. |
| product_id | TEXT | FK → PRODUCTS. |
| product_name | TEXT | Nombre del producto al momento de la cotización (instantánea). |
| unit_price | REAL | Precio unitario al momento de la cotización (instantánea). |
| quantity | REAL | Cantidad cotizada. |
| discount | REAL | Porcentaje de descuento aplicado (0–100). |

### DOCUMENT_SEND_LOGS

| Campo | Tipo | Descripción |
|---|---|---|
| id | TEXT (UUID) | Clave primaria. |
| document_type | INTEGER | 1 = Factura, 2 = Cotización. |
| document_id | TEXT | ID de la factura o cotización enviada. |
| channel | INTEGER | 1 = Correo electrónico, 2 = WhatsApp. |
| recipient_contact | TEXT | Correo electrónico o número de teléfono del destinatario. |
| sent_at | TEXT (ISO 8601) | Fecha y hora del envío. |
| success | INTEGER | 1 = envío exitoso, 0 = fallo. |
| error_message | TEXT | Mensaje de error, nullable. Se popula únicamente en caso de fallo. |

## Relaciones principales

```text
User 1 ─── * Sale
User 1 ─── * InventoryMovement
Customer 1 ─── * Sale
Sale 1 ─── * SaleItem
Sale 1 ─── * Payment
Product 1 ─── * SaleItem
Product 1 ─── * InventoryMovement
Supplier 1 ─── * Purchase
Purchase 1 ─── * PurchaseItem
Product 1 ─── * PurchaseItem
Category 1 ─── * Product
Customer 0..1 ─── * Quote
Quote 1 ─── * QuoteItem
Product 1 ─── * QuoteItem
Quote 0..1 ─── 0..1 Sale      (converted_sale_id)
Sale 1 ─── * SaleReturn       (original_sale_id, IsUnique(false))
SaleReturn 1 ─── * SaleReturnItem
SaleItem 1 ─── * SaleReturnItem
```

## Diseño didáctico desde la base de datos

| Necesidad del usuario | Datos requeridos |
|---|---|
| Ver productos agotados | `Products.CurrentStock` y `Products.MinimumStock` |
| Consultar ventas del día | `Sales.CreatedAt`, `Payments.PaymentMethod`, suma de `Payments.Amount` o `SaleItems` |
| Reimprimir factura | `Sales`, `SaleItems`, `Payments`, `Customer` |
| Saber cuánto efectivo hay en caja | Pagos con método `Efectivo` |
| Ver utilidad | `Products.CostPrice`, `SaleItems.UnitPrice`, `SaleItems.Quantity` |
| Ver historial de un cliente | `CustomerId` en `Sales` |

## Recomendaciones técnicas

- Crear índices en `Products.Name`, `Products.Barcode`, `Sales.CreatedAt` y `Customers.Identification`.
- Usar transacciones para registrar ventas, pagos e inventario en una sola operación.
- Aplicar soft delete en productos, usuarios y clientes usando `IsActive`.
- Cifrar la base de datos si se maneja información sensible.
- Mantener backups automáticos o manuales accesibles desde configuración.

## Resultado esperado

El modelo SQLite permitirá que el POS funcione localmente, registre ventas de forma segura, actualice inventario automáticamente y genere reportes entendibles para el usuario final.

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[02_diseno_arquitectura_capas]] · [[03_diseno_modelo_dominio]]
