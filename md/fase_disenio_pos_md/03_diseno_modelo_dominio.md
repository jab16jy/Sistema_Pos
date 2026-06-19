# 03 — Diseño del Modelo de Dominio

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir las entidades, value objects y reglas de negocio que representan el funcionamiento real de un comercio pequeño dentro del sistema POS.

## Entidades principales

| Entidad | Propósito |
|---|---|
| `User` | Representa a los usuarios del sistema: administrador o cajero. |
| `Product` | Representa un producto vendido por el negocio. |
| `Category` | Agrupa productos para facilitar búsqueda y reportes. |
| `Customer` | Representa a los clientes registrados. |
| `Supplier` | Representa a los proveedores. |
| `Sale` | Representa una venta realizada. |
| `SaleItem` | Representa cada producto dentro de una venta. |
| `Payment` | Representa los pagos de una venta. |
| `Purchase` | Representa una compra a proveedor. |
| `PurchaseItem` | Representa los productos comprados. |
| `InventoryMovement` | Registra entradas, salidas y ajustes de inventario. |
| `CashClosing` | Registra el cierre de caja. |
| `Invoice` | Representa la factura o recibo emitido. |
| `LoyaltyTransaction` | Registra acumulación o redención de puntos. |
| `SystemConfiguration` | Guarda datos del negocio, impuestos, moneda e impresora. |

## Value Objects propuestos

| Value Object | Uso |
|---|---|
| `Money` | Evita manejar valores negativos o inconsistentes. |
| `Barcode` | Valida códigos EAN-13, Code 128 o códigos internos. |
| `Quantity` | Controla cantidades y evita números inválidos. |
| `Email` | Valida correos de clientes, proveedores o usuarios. |
| `PhoneNumber` | Normaliza teléfonos. |
| `TaxId` | Representa NIT, RUT o identificación fiscal. |
| `InvoiceNumber` | Controla numeración de facturas. |

## Reglas de negocio principales

### Productos e inventario

- Un producto debe tener nombre, precio de venta y categoría.
- El precio de venta no debe ser menor que cero.
- El stock no puede ser negativo.
- Cuando se vende un producto, el stock disminuye.
- Cuando se registra una compra, el stock aumenta.
- Si el stock actual es menor al stock mínimo, el sistema debe marcarlo como **bajo stock**.

### Ventas

- Una venta debe tener al menos un producto.
- El total de la venta se calcula a partir de los subtotales, descuentos e impuestos.
- Una venta solo puede finalizar si el pago cubre el total.
- En pagos en efectivo, el sistema calcula el cambio automáticamente.
- Una venta anulada debe registrar motivo, fecha y usuario que autorizó.

### Clientes y puntos

- Una venta puede estar asociada a un cliente.
- Si el programa de puntos está activo, la venta suma puntos al cliente.
- Los puntos pueden redimirse como descuento si el saldo es suficiente.
- Toda acumulación o redención debe quedar registrada.

## Diseño didáctico desde el dominio

| Situación | Respuesta didáctica del sistema |
|---|---|
| El cajero intenta vender sin productos | “Agrega al menos un producto para continuar.” |
| No hay stock suficiente | “Solo hay 3 unidades disponibles de este producto.” |
| El pago es menor al total | “El monto recibido no cubre el total de la venta.” |
| Producto bajo mínimo | “Este producto está por agotarse. Revisa inventario.” |
| Acción requiere administrador | “Esta acción necesita autorización del administrador.” |

## Ejemplo de entidad conceptual: Product

```csharp
public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Barcode { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal MinimumStock { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsLowStock() => CurrentStock <= MinimumStock;
}
```

## Resultado esperado

El modelo de dominio debe reflejar el negocio real de forma clara. Esto permite que el sistema sea confiable y que la interfaz pueda explicar los errores o pasos al usuario con mensajes fáciles de entender.
