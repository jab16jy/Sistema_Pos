# Esquema SQLite para catálogo importado desde Excel

Este documento define el esquema mínimo y correcto para cargar el archivo `Gestión de productos y servicios-20260211103730.xlsx` en SQLite sin perder datos, sin romper relaciones y sin mentirle al dominio con defaults incorrectos.

## Respuesta corta

Para que el POS funcione con este Excel, la base de datos necesita estas piezas:

1. **Catálogo principal** que soporte **productos y servicios** en la misma tabla.
2. **Tablas maestras** para impuestos, unidades, categorías y subcategorías.
3. **Trazabilidad de importación** para guardar la fila cruda del Excel y su normalización.
4. **Movimientos de inventario** para reconciliar el stock importado, incluso cuando llegue negativo.

## Fuente analizada

- Archivo: `docs md/docs/Gestión de productos y servicios-20260211103730.xlsx`
- Hoja detectada: `Gestión de productos y servici`
- Filas de datos útiles: **826**
- Columnas del origen:
  - `Tipo`
  - `Código`
  - `Nombre`
  - `Unidad`
  - `Precios`
  - `Impuestos`
  - `Stock`
  - `Estado`

## Hallazgos que afectan el diseño

| Hallazgo | Impacto en BD |
|---|---|
| `Código` tiene 826 valores distintos | Debe ser **texto** y **único**. No puede modelarse como entero. |
| Hay códigos alfanuméricos como `54T`, `80-983`, `9/11`, `productogenericonube` | El código debe aceptar letras, guiones y slash. |
| `Nombre` tiene duplicados (34 repeticiones) | `Name` **no puede** ser único. |
| Hay **824 productos** y **2 servicios** | La tabla principal debe soportar ambos tipos. |
| `Unidad` viene vacía en 570 filas | Se necesita normalización y una unidad por defecto. |
| `Impuestos` trae `IVA 19%`, `IVA 0%` y vacío | Se necesita catálogo de impuestos y un valor `Sin impuesto`. |
| Hay 259 filas con stock negativo; mínimo detectado `-31253` | El esquema no puede asumir stock no negativo en la importación. |
| El Excel no trae costo, categoría, subcategoría ni proveedor | Esos datos deben ser nullable o resolverse con defaults explícitos, no inventados implícitamente. |

## Mapeo de columnas del Excel a la BD

| Excel | Destino | Regla |
|---|---|---|
| `Tipo` | `PRODUCTS.ItemType` | `Producto` -> `Product`; `Servicio` -> `Service` |
| `Tipo` | `PRODUCTS.TracksInventory` | `Product` -> `1`; `Service` -> `0` |
| `Código` | `PRODUCTS.SourceCode` | Obligatorio y único |
| `Código` | `PRODUCTS.Barcode` | Nullable; para productos físicos puede sembrarse igual a `SourceCode` |
| `Nombre` | `PRODUCTS.Name` | Obligatorio; no único |
| `Unidad` | `PRODUCTS.UnitOfMeasureId` | Resolver contra `UNIT_OF_MEASURES`; si viene vacío usar `unidad` |
| `Precios` | `PRODUCTS.SalePrice` | Obligatorio; `NUMERIC(14,2)` |
| `Impuestos` | `PRODUCTS.TaxProfileId` | Resolver contra `TAX_PROFILES`; vacío -> `NO_TAX` |
| `Stock` | `PRODUCTS.CurrentStock` | Guardar el valor firmado tal como llega |
| `Estado` | `PRODUCTS.IsActive` | `Active` -> `1` |

## Esquema propuesto

## 1) `TAX_PROFILES`

Catálogo normalizado de impuestos.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `Code` | `TEXT` | No | Único. Ej: `IVA_19`, `IVA_0`, `NO_TAX` |
| `Name` | `TEXT` | No | Ej: `IVA 19%` |
| `Rate` | `NUMERIC` | No | `CHECK (Rate >= 0 AND Rate <= 100)` |
| `IsActive` | `INTEGER` | No | `0/1`, default `1` |

## 2) `UNIT_OF_MEASURES`

Catálogo de unidades.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `Name` | `TEXT` | No | Ej: `unidad` |
| `Abbreviation` | `TEXT` | No | Ej: `und` |
| `IsActive` | `INTEGER` | No | default `1` |

## 3) `CATEGORIES`

Como el Excel no trae categorías, el diseño debe permitir una categoría por defecto.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `Name` | `TEXT` | No | Único dentro del catálogo activo |
| `IsSystem` | `INTEGER` | No | default `0` |
| `IsActive` | `INTEGER` | No | default `1` |

## 4) `SUBCATEGORIES`

Opcional. Útil cuando luego se refine el catálogo.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `CategoryId` | `TEXT` | No | FK -> `CATEGORIES.Id` |
| `Name` | `TEXT` | No | No repetir dentro de la categoría |
| `IsActive` | `INTEGER` | No | default `1` |

## 5) `PRODUCTS`

Tabla principal del catálogo. Aunque hoy se llame `PRODUCTS`, DEBE soportar productos y servicios porque el Excel mezcla ambos.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `ItemType` | `TEXT` | No | `CHECK (ItemType IN ('Product', 'Service'))` |
| `SourceCode` | `TEXT` | No | Único. Código del Excel; no numérico obligatorio |
| `Barcode` | `TEXT` | Sí | Único cuando exista |
| `Name` | `TEXT` | No | Indexado para búsqueda |
| `Description` | `TEXT` | Sí | Texto libre |
| `CategoryId` | `TEXT` | No | FK -> `CATEGORIES.Id` |
| `SubCategoryId` | `TEXT` | Sí | FK -> `SUBCATEGORIES.Id` |
| `UnitOfMeasureId` | `TEXT` | Sí | FK -> `UNIT_OF_MEASURES.Id` |
| `TaxProfileId` | `TEXT` | No | FK -> `TAX_PROFILES.Id` |
| `SalePrice` | `NUMERIC` | No | `CHECK (SalePrice >= 0)` |
| `CostPrice` | `NUMERIC` | Sí | Debe permitir `NULL` porque el Excel no lo trae |
| `CostStatus` | `TEXT` | No | `CHECK (CostStatus IN ('Pending', 'Defined'))`, default `Pending` |
| `CurrentStock` | `INTEGER` | No | Debe aceptar negativos en la importación |
| `MinimumStock` | `INTEGER` | No | default `0`, `CHECK (MinimumStock >= 0)` |
| `TracksInventory` | `INTEGER` | No | `1` productos físicos, `0` servicios |
| `IsActive` | `INTEGER` | No | default `1` |
| `CreatedAt` | `TEXT` | No | ISO 8601 |
| `UpdatedAt` | `TEXT` | No | ISO 8601 |

### Reglas críticas de `PRODUCTS`

- `SourceCode` es la clave de negocio del archivo origen.
- `Barcode` no debe reemplazar `SourceCode`; son responsabilidades distintas.
- `CurrentStock` **no** puede tener `CHECK (CurrentStock >= 0)` al nivel de BD si se va a importar este Excel tal como está.
- `CostPrice` no debe forzarse a `0` si el origen no lo trae; eso rompe utilidades y auditoría.

## 6) `INVENTORY_MOVEMENTS`

El stock importado debe quedar respaldado por movimientos, no solo por un saldo final.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `ProductId` | `TEXT` | No | FK -> `PRODUCTS.Id` |
| `MovementType` | `TEXT` | No | Ej: `ImportSeed`, `Purchase`, `Sale`, `Adjustment`, `Return` |
| `Quantity` | `INTEGER` | No | Puede ser positiva o negativa |
| `Reason` | `TEXT` | No | Explica el movimiento |
| `ReferenceId` | `TEXT` | Sí | Batch, compra, venta, devolución, etc. |
| `CreatedAt` | `TEXT` | No | ISO 8601 |

## 7) `PRODUCT_IMPORT_BATCHES`

Traza cada importación de Excel/CSV.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `SourceFileName` | `TEXT` | No | Nombre del archivo origen |
| `SourceHash` | `TEXT` | No | Evita reimportes silenciosos |
| `ImportedAt` | `TEXT` | No | ISO 8601 |
| `ImportedBy` | `TEXT` | Sí | Usuario/responsable |
| `Notes` | `TEXT` | Sí | Observaciones |

## 8) `PRODUCT_IMPORT_LINES`

Guarda la fila cruda del origen y su resultado de normalización.

| Columna | Tipo SQLite | Nulo | Regla |
|---|---|---|---|
| `Id` | `TEXT` | No | PK UUID |
| `BatchId` | `TEXT` | No | FK -> `PRODUCT_IMPORT_BATCHES.Id` |
| `RowNumber` | `INTEGER` | No | Número de fila del Excel |
| `RawType` | `TEXT` | Sí | Valor original |
| `RawCode` | `TEXT` | Sí | Valor original |
| `RawName` | `TEXT` | Sí | Valor original |
| `RawUnit` | `TEXT` | Sí | Valor original |
| `RawPrice` | `TEXT` | Sí | Valor original |
| `RawTax` | `TEXT` | Sí | Valor original |
| `RawStock` | `TEXT` | Sí | Valor original |
| `RawState` | `TEXT` | Sí | Valor original |
| `ResolvedProductId` | `TEXT` | Sí | FK -> `PRODUCTS.Id` |
| `NormalizationStatus` | `TEXT` | No | `Imported`, `Skipped`, `Error` |
| `ErrorMessage` | `TEXT` | Sí | Si la línea falla |

## Relaciones obligatorias

```text
CATEGORIES 1 ─── * SUBCATEGORIES
CATEGORIES 1 ─── * PRODUCTS
SUBCATEGORIES 0..1 ─── * PRODUCTS
UNIT_OF_MEASURES 0..1 ─── * PRODUCTS
TAX_PROFILES 1 ─── * PRODUCTS
PRODUCTS 1 ─── * INVENTORY_MOVEMENTS
PRODUCT_IMPORT_BATCHES 1 ─── * PRODUCT_IMPORT_LINES
PRODUCTS 0..1 ─── * PRODUCT_IMPORT_LINES
```

## Datos semilla mínimos

Antes de importar el Excel, la BD debería crear estos registros:

### `TAX_PROFILES`

- `IVA_19` -> `IVA 19%` -> `19.00`
- `IVA_0` -> `IVA 0%` -> `0.00`
- `NO_TAX` -> `Sin impuesto` -> `0.00`

### `UNIT_OF_MEASURES`

- `unidad` / `und`

### `CATEGORIES`

- `Sin categoría`

## Reglas de importación

1. Si `Unidad` viene vacía, usar la unidad semilla `unidad`.
2. Si `Impuestos` viene vacío, mapear a `NO_TAX`.
3. Si `Tipo = Servicio`, crear el registro con `TracksInventory = 0`.
4. Si `Tipo = Producto`, crear el registro con `TracksInventory = 1`.
5. Si `Stock` es negativo, preservarlo y registrar un movimiento `ImportSeed` con ese saldo inicial.
6. Si `Barcode` aún no está diferenciado del código fuente, sembrarlo inicialmente con `SourceCode` solo en productos físicos.
7. `CostPrice` queda en `NULL` hasta que el negocio cargue el costo real.

## Índices mínimos

- `UX_PRODUCTS_SourceCode`
- `UX_PRODUCTS_Barcode` (filtrado por no null)
- `IX_PRODUCTS_Name`
- `IX_PRODUCTS_CategoryId`
- `IX_PRODUCTS_SubCategoryId`
- `IX_PRODUCTS_TaxProfileId`
- `IX_INVENTORY_MOVEMENTS_ProductId_CreatedAt`
- `UX_PRODUCT_IMPORT_BATCHES_SourceHash`
- `IX_PRODUCT_IMPORT_LINES_BatchId_RowNumber`

## Decisiones no negociables

- **No** modelar `Código` como entero.
- **No** marcar `Name` como único.
- **No** obligar `CostPrice = 0` para salir del paso.
- **No** perder la fila original del Excel; debe existir staging/import trace.
- **No** bloquear la importación solo porque el stock venga negativo; primero se preserva, luego se sanea con negocio.

## Resultado esperado

Con este esquema, el POS puede:

- cargar el catálogo real del Excel,
- distinguir productos de servicios,
- buscar por código o barcode,
- vender desde Caja,
- mantener trazabilidad del origen,
- y evolucionar después sin romper datos ni relaciones.

---

## Documentos relacionados

[[04_diseno_modelo_datos_sqlite]] · [[03_diseno_modelo_dominio]] · [[modulos]]
