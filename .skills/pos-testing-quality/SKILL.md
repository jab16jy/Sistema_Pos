---
name: pos-testing-quality
description: Project-specific testing and quality guidance for the local POS system.
---

# POS Testing and Quality

Use this skill when designing, writing, or reviewing tests.

## Source documentation

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/09_diseno_protocolo_pruebas.md`
- `md/fase_disenio_pos_md/10_diseno_entregable_final.md`

## Test categories

The project documentation requires:

1. Unit tests
2. Integration tests
3. Functional tests
4. Didactic usability tests
5. Performance tests
6. Security tests
7. Installation tests

## Unit test targets

Test business rules such as:

- Calculate sale total.
- Calculate cash change.
- Validate sufficient stock.
- Apply discount.
- Accumulate points.
- Redeem points.
- Calculate utility/profit.
- Validate payment breakdown.

## Integration test targets

Test full persistence-backed flows:

- Complete sale saves sale, items, payment, invoice, and inventory movement.
- Purchase from supplier increases stock.
- Daily report queries real SQLite data.
- Invoice can be found and reprinted.
- Backup copies database correctly.

## Functional scenarios

Cover:

- Cash sale with change.
- Digital payment sale.
- Mixed payment.
- Hold and resume sale if implemented.
- Invoice reprint.
- Sale cancellation with administrator authorization.
- Low stock alert.
- PDF and Excel report export.

## Performance targets

From project documentation:

- Application startup under 5 seconds.
- Product search in Caja under 1 second.
- Inventory query with 10,000 products under 2 seconds.
- Complete sale under 1 second in normal operation.

## Security tests

Validate:

- Passwords are hashed.
- Cashier cannot cancel sale without administrator authorization.
- Database protection/encryption or equivalent mechanism is applied according to final technical decision.
- Session entry/exit is registered.

## Test record format

Use this format for manual/functional validation:

```text
ID de prueba:
Módulo:
Escenario:
Pasos ejecutados:
Resultado esperado:
Resultado obtenido:
Estado: Aprobado / Fallido
Observaciones:
```

## Review checklist

- [ ] Domain/Application rules are unit tested without WPF.
- [ ] EF Core + SQLite flows are integration tested.
- [ ] Cashier-critical flows are covered end-to-end or functionally.
- [ ] Security cases cover role restrictions and password storage.
- [ ] Performance targets are measured, not assumed.

