# 11 — Diseño del Módulo de Cotizaciones

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Propósito del módulo

El módulo de Cotizaciones permite generar propuestas comerciales para los clientes sin comprometer el inventario ni registrar una venta. Su objetivo es que el vendedor pueda presentar precios y condiciones, fijar una vigencia, y convertir la cotización en venta cuando el cliente confirme, todo desde la misma pantalla.

El módulo no descuenta stock en ningún momento previo a la conversión. Solo al convertir la cotización a venta se ejecutan las mismas operaciones que en una venta directa desde Caja.

---

## Modelo de dominio

### Entidad Quote (raíz de agregado)

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | `Guid` | Identificador único de la cotización. |
| `QuoteNumber` | `string` | Número consecutivo auto-generado. Formato: `COT-YYYY-NNNN`. No editable. |
| `CustomerId` | `Guid?` | FK hacia `Customer`. Nullable: se puede cotizar sin cliente registrado. |
| `CustomerName` | `string` | Nombre del cliente al momento de la emisión. |
| `CustomerPhone` | `string?` | Teléfono del cliente, nullable. |
| `CustomerEmail` | `string?` | Correo del cliente. Si está presente, se valida con el value object `Email`. |
| `Observations` | `string?` | Notas u observaciones adicionales, nullable. |
| `ValidityDays` | `int` | Días de vigencia desde la fecha de creación. |
| `Status` | `QuoteStatus` | Estado de la cotización: `Draft`, `Active`, `Expired` o `Converted`. |
| `CreatedAt` | `DateTime` | Fecha y hora de creación. |
| `ExpiresAt` | `DateTime` | Fecha de vencimiento. Calculada como `CreatedAt + ValidityDays`. |
| `Items` | `IReadOnlyCollection<QuoteItem>` | Líneas de producto cotizadas. |
| `Total` | `decimal` | Total calculado a partir de los subtotales de cada ítem. |

### Entidad QuoteItem (hijo del agregado)

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | `Guid` | Identificador único del ítem. |
| `QuoteId` | `Guid` | FK → `Quote`. |
| `ProductId` | `Guid` | FK → `Product`. |
| `ProductName` | `string` | Nombre del producto al momento de crear la cotización (instantánea). |
| `UnitPrice` | `Money` | Precio unitario al momento de crear la cotización (instantánea). |
| `Quantity` | `Quantity` | Cantidad cotizada. |
| `Discount` | `decimal` | Porcentaje de descuento aplicado al ítem (0–100 %). |

**Cálculo del subtotal:**

```csharp
public Money Subtotal() =>
    new Money(UnitPrice.Amount * Quantity.Value * (1 - Discount / 100m));
```

---

## Reglas de negocio

| Regla | Descripción |
|---|---|
| Sin afectación de inventario | La creación, modificación o eliminación de una cotización no descuenta ni reserva stock en ningún momento. |
| Numeración consecutiva | El número `COT-YYYY-NNNN` se genera automáticamente al confirmar la cotización. No es editable por el usuario. |
| Cálculo de vencimiento | `ExpiresAt = CreatedAt + ValidityDays`. El sistema marca automáticamente la cotización como `Expired` cuando la fecha actual supera `ExpiresAt`. |
| Bloqueo tras conversión | Una cotización en estado `Converted` no puede ser modificada ni cancelada. Cualquier ajuste debe hacerse sobre la venta generada. |
| Precio e instantánea | `ProductName` y `UnitPrice` en `QuoteItem` se fijan al momento de crear el ítem y no se actualizan si el catálogo cambia posteriormente. |
| Conversión condicionada | Solo se puede convertir una cotización en estado `Active` o `Draft`. El estado `Expired` y `Converted` bloquean la conversión. |

---

## Flujo funcional

### Flujo de creación

```text
1. El vendedor entra a Cotizaciones → Nueva cotización.
2. Busca y agrega productos desde el catálogo (nombre o código de barras).
3. Define cantidad y descuento por ítem.
4. Ingresa los datos del cliente (nombre, teléfono, correo, observaciones).
5. Define los días de vigencia.
6. Confirma la cotización.
7. El sistema genera el número consecutivo COT-YYYY-NNNN.
8. El sistema calcula ExpiresAt y guarda el registro sin tocar el inventario.
9. El sistema ofrece imprimir o enviar el PDF.
```

### Flujo de conversión a venta

```text
1. El vendedor busca la cotización desde el historial.
2. El sistema verifica el estado (debe ser Active o Draft).
3. El sistema verifica disponibilidad de stock por cada ítem.
4. Si hay insuficiencia de stock, el sistema informa qué productos no pueden despacharse.
5. El vendedor selecciona el método de pago.
6. El vendedor confirma la conversión.
7. El sistema crea el registro de venta con los ítems de la cotización.
8. El sistema descuenta el inventario.
9. El sistema actualiza el estado de la cotización a Converted y registra converted_sale_id.
10. El sistema ofrece imprimir la factura.
```

### Flujo de consulta del historial

```text
1. El usuario entra a Cotizaciones → Historial.
2. Aplica filtros: rango de fechas, nombre del cliente, estado.
3. El sistema devuelve la lista paginada.
4. El usuario selecciona una cotización para ver el detalle.
5. Desde el detalle puede reimprimir, reenviar o convertir.
```

---

## Casos de uso relacionados

Los siguientes casos de uso del documento `07_diseno_casos_uso.md` cubren este módulo:

- **Caso de uso 8** — Crear cotización
- **Caso de uso 9** — Convertir cotización a venta
- **Caso de uso 10** — Consultar historial de cotizaciones

---

## Integración con otros módulos

| Módulo relacionado | Tipo de integración | Descripción |
|---|---|---|
| **Productos** | Lectura | El catálogo proporciona los productos para armar la cotización. El precio y nombre se almacenan como instantánea en `QuoteItem`. |
| **Clientes** | Lectura | Los datos de contacto del cliente pueden precompletarse desde el directorio. La cotización también acepta clientes no registrados. |
| **Ventas** | Escritura | La conversión crea un registro completo en `SALES` y enlaza la cotización mediante `converted_sale_id`. |
| **Comunicación Digital** | Lectura | El módulo de Comunicación Digital lee la cotización para generar y enviar el PDF al cliente. |

---

## Esquema de datos SQLite

Las tablas `QUOTES` y `QUOTE_ITEMS` están definidas en el documento `04_diseno_modelo_datos_sqlite.md`.

**Índices recomendados:**

```sql
CREATE INDEX idx_quotes_status      ON QUOTES (status);
CREATE INDEX idx_quotes_created_at  ON QUOTES (created_at);
CREATE INDEX idx_quotes_customer_id ON QUOTES (customer_id);
CREATE INDEX idx_quote_items_quote  ON QUOTE_ITEMS (quote_id);
```

---

## Interfaces de repositorio

```csharp
public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(Guid id);
    Task<Quote?> GetByNumberAsync(string quoteNumber);
    Task<IReadOnlyList<Quote>> GetHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerName,
        QuoteStatus? status,
        int page,
        int pageSize);
    Task<string> GenerateNextNumberAsync(int year);   // Retorna el siguiente COT-YYYY-NNNN
    Task AddAsync(Quote quote);
    Task UpdateAsync(Quote quote);
}
```

---

## Casos de uso de aplicación

### CreateQuoteUseCase

```csharp
// Application/UseCases/Quotes/CreateQuoteUseCase.cs
public sealed class CreateQuoteUseCase
{
    // Dependencias: IQuoteRepository, IProductRepository
    // Entrada: CreateQuoteRequest (ítems, datos del cliente, validityDays)
    // Salida: QuoteDto (incluye quoteNumber generado)
    // Reglas: genera número consecutivo, calcula ExpiresAt, no afecta inventario
}
```

### ConvertQuoteToSaleUseCase

```csharp
// Application/UseCases/Quotes/ConvertQuoteToSaleUseCase.cs
public sealed class ConvertQuoteToSaleUseCase
{
    // Dependencias: IQuoteRepository, ISaleRepository, IProductRepository
    // Entrada: ConvertQuoteRequest (quoteId, paymentMethod)
    // Salida: SaleDto
    // Reglas: verifica estado, verifica stock, crea Sale, actualiza Quote.Status = Converted
}
```

### GetQuoteHistoryUseCase

```csharp
// Application/UseCases/Quotes/GetQuoteHistoryUseCase.cs
public sealed class GetQuoteHistoryUseCase
{
    // Dependencias: IQuoteRepository
    // Entrada: GetQuoteHistoryRequest (filtros + paginación)
    // Salida: PagedResult<QuoteListItemDto>
}
```

---

## Diseño didáctico del módulo

| Situación | Respuesta del sistema |
|---|---|
| Cotización vencida | "Esta cotización venció el {fecha}. Puedes crear una nueva desde el historial." |
| Cotización ya convertida | "Esta cotización ya fue convertida a venta el {fecha}." |
| Stock insuficiente al convertir | "No hay suficiente stock de {producto}. Ajusta la cantidad o actualiza el inventario antes de continuar." |
| Cotización guardada | "Cotización {COT-YYYY-NNNN} guardada. Puedes enviarla al cliente o convertirla en venta cuando confirme." |

## Resultado esperado

Al finalizar la implementación de este módulo, el vendedor podrá generar propuestas comerciales formales, controlar su vigencia, consultarlas en historial y convertirlas a venta sin necesidad de volver a ingresar los productos ni los datos del cliente.

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[03_diseno_modelo_dominio]] · [[05_diseno_flujos_funcionales]] · [[08_diseno_integracion_modulos]] · [[12_diseno_comunicacion_digital]]
