---
name: pos-catalog-source-analysis
description: Use when analyzing Excel/CSV/catalog exports to infer SQLite columns, defaults, normalization rules, and import-safe mappings for Sistema POS.
---

# POS Catalog Source Analysis

Use this skill when the input is a spreadsheet, CSV, or exported product catalog and you need to convert it into a trustworthy SQLite design.

## Source documents

- `docs md/docs/Gestión de productos y servicios-20260211103730.xlsx`
- `docs md/docs/SQLITE_CATALOG_SCHEMA_FROM_EXCEL.md`
- `docs md/Exploracion/modulos.md`
- `docs md/fase_disenio_pos_md/04_diseno_modelo_datos_sqlite.md`

## Core rule

Do **not** jump from columns to tables blindly. First validate the source quality:

1. Which columns are present?
2. Which values are missing?
3. Which values are duplicated?
4. Which values are not numeric even if they look like codes?
5. Which fields are business keys versus just labels?

## Known realities of this project source

- `Código` is the business key and must be stored as **TEXT**, not integer.
- `Nombre` is not unique.
- The source mixes **Producto** and **Servicio**.
- `Unidad` is frequently blank and must be normalized.
- `Impuestos` may be blank and must map to `NO_TAX`.
- Stock can be negative and must be preserved on import.
- The source does **not** provide category, subcategory, supplier, or cost price.

## Required output from source analysis

When finishing the analysis, always produce these decisions explicitly:

| Question | Required answer |
|---|---|
| Business key | Which source column is unique enough to identify rows? |
| Text vs numeric | Which columns must remain text even if they contain digits? |
| Nullability | Which missing values are allowed and which need seeded defaults? |
| Type split | Does the source mix products, services, variants, or bundles? |
| Inventory behavior | Which rows track stock and which do not? |
| Tax behavior | Which tax profiles are needed? |
| Import trace | How will the raw row be stored for auditability? |

## Normalization rules for this POS

- `Tipo` -> `ItemType` + `TracksInventory`
- `Código` -> `SourceCode`
- `Nombre` -> `Name`
- blank `Unidad` -> seeded `unidad`
- blank `Impuestos` -> seeded `NO_TAX`
- `Precios` -> `SalePrice`
- `Stock` -> signed `CurrentStock`
- `Estado` -> `IsActive`

## Modeling warning

If the source is incomplete, do **not** fake completeness.

For this project that means:

- do not invent cost price,
- do not infer categories from product names unless the user asks,
- do not discard negative stock,
- do not collapse products and services into the same behavior without an explicit `ItemType`.

## Checklist

- [ ] Source columns were enumerated exactly.
- [ ] Distinct business keys were validated.
- [ ] Duplicate names were treated as allowed.
- [ ] Blank values were normalized with explicit rules.
- [ ] Raw import staging was included.
- [ ] Product/service split was preserved.

## Expected downstream handoff

This skill should feed `pos-sqlite-schema-design` with:

- column mapping,
- nullability rules,
- seed data needs,
- and import constraints.
