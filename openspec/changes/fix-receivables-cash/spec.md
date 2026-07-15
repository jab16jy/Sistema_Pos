# Requirements

## Settlement tenders

- **MUST** persist the payment method for every receivable settlement.
- **MUST** persist the transfer platform when the method is transfer.
- **MUST** classify settlement cash and digital totals separately.

## Consistency

- **MUST** reject a settlement plan whose collected amount plus deferred amount differs from the sale total.
- **MUST** reject a deferred sale when its receivable repository is unavailable.
- **MUST** reject stale concurrent receivable updates rather than silently overwriting a newer balance.

## Sale lifecycle

- Cancelling a sale **MUST** close its outstanding receivable.
- A full return **MUST** close its outstanding receivable; a partial return **MUST** reduce it by the returned amount, capped at the outstanding balance.
- A cancelled receivable **MUST NOT** accept new settlements.

## Reporting and display

- Shift and cash-register reports **MUST** include receivable settlements in their cash/digital breakdown.
- Invoice detail **MUST** expose the current receivable balance and settlement history.
- Customer purchase KPIs **MUST** count only completed sales, while the history may retain other states for audit.
