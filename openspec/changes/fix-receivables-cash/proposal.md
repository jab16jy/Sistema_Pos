# Fix receivable, cash, and invoice consistency

## Why

Receivable settlements currently lose their tender type, shift summaries omit them, and sale lifecycle changes can leave debt active. Payment-plan validation and customer purchase totals also accept inconsistent data.

## Scope

- Persist settlement tender and transfer platform.
- Include settlement cash/digital totals in operational and shift closing calculations.
- Prevent over/under-covered settlement plans.
- Close or reduce receivables when sales are cancelled or returned.
- Expose receivable balance and settlements in invoice details.
- Keep customer history auditable while excluding non-completed sales from purchase KPIs.
- Add optimistic concurrency protection to receivables.

## Rollback

The migration provides a reversible `Down` path. Legacy settlements default to cash, preserving existing cash reports while new records are classified correctly.
