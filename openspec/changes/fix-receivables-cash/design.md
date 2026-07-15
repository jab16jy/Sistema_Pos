# Design

`ReceivableSettlement` gains `PaymentMethod` and optional `BankOrPlatform`; existing rows are backfilled as cash. `Receivable` gains a cancellation flag and integer concurrency token. Domain methods own settlement, return adjustment, and cancellation invariants.

The application layer passes settlement tender data, performs payment-platform validation, and coordinates receivable lifecycle changes from cancellation/return use cases. Infrastructure queries classify settlement rows by tender and joins them to the sale cashier for shift summaries.

Invoice DTOs carry the receivable balance and settlement rows without exposing EF entities. Presentation adds the minimum bindings for selecting an abono tender/platform and displaying invoice debt history.

SQLite migration columns use safe defaults (`Cash`, active receivable, version zero) so existing databases remain readable.
