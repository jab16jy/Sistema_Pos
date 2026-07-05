---
name: pos-sqlite-schema-design
description: Use when designing or changing SQLite/EF Core tables, columns, keys, nullability, indexes, and relationships for the Sistema POS catalog and import flows.
---

# POS SQLite Schema Design

Use this skill when creating or modifying the SQLite schema for Sistema POS, especially around product catalog imports, inventory, taxes, units, and relational integrity.

## Source documents

- `docs md/docs/SQLITE_CATALOG_SCHEMA_FROM_EXCEL.md`
- `docs md/fase_disenio_pos_md/04_diseno_modelo_datos_sqlite.md`
- `docs md/fase_disenio_pos_md/08_diseno_integracion_modulos.md`
- `.skills/pos-efcore-sqlite/SKILL.md`

## Design priorities

1. Preserve the real source data.
2. Keep the schema relational and queryable.
3. Avoid defaults that corrupt reporting.
4. Support both products and services in Caja.
5. Keep import traceability.

## Mandatory tables for the catalog-import problem

- `TAX_PROFILES`
- `UNIT_OF_MEASURES`
- `CATEGORIES`
- `SUBCATEGORIES`
- `PRODUCTS`
- `INVENTORY_MOVEMENTS`
- `PRODUCT_IMPORT_BATCHES`
- `PRODUCT_IMPORT_LINES`

## Mandatory rules

### `PRODUCTS`

- Must support `ItemType` = `Product | Service`.
- Must keep `SourceCode` as unique text.
- Must not require `Barcode`.
- Must allow `CostPrice` to be null when the source lacks cost.
- Must allow imported signed stock values.
- Must distinguish `TracksInventory` from `ItemType` at the data level.

### Relationships

- `CategoryId` is required.
- `SubCategoryId` is optional.
- `UnitOfMeasureId` is optional at schema level but should resolve to seeded `unidad` during import when possible.
- `TaxProfileId` is required.
- Every imported stock baseline must be reconcilable through `INVENTORY_MOVEMENTS`.

### Import traceability

- Every import batch needs a durable identifier and source hash.
- Every raw row must remain queryable after normalization.
- A normalized product must be traceable back to its source row.

## Constraints and indexing

Always check these:

- unique index on `PRODUCTS.SourceCode`
- nullable unique index on `PRODUCTS.Barcode`
- index on `PRODUCTS.Name`
- indexes on foreign keys
- import batch uniqueness by `SourceHash`

## EF Core guidance

- Keep schema decisions inside Infrastructure.
- Model code-like fields as `string`, not numeric primitives.
- Prefer explicit configuration classes over convention for important constraints.
- Do not hide nullability problems with silent conversions.

## Anti-patterns

Do not do any of these:

- store source rows without import metadata,
- turn blank tax into `0` without a named tax profile,
- force `CostPrice = 0` just to satisfy a non-null column,
- enforce non-negative stock in the database when the import source already violates it,
- use product name as a unique key.

## Review checklist

- [ ] Keys, FKs, nullability, and indexes were declared explicitly.
- [ ] The schema supports both products and services.
- [ ] Import staging exists.
- [ ] Cost data is not falsified.
- [ ] Negative imported stock is preserved safely.
- [ ] The resulting schema still fits Clean Architecture boundaries.
