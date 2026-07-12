# Proposal: Supplier Purchasing Refactor

## Intent

Replace the current supplier-purchase flow with an auditable receipt workflow driven by the active supplier catalog, with reliable inventory and cost updates.

## Scope

### In Scope

- Register purchases only against an active supplier and active supplier-product catalog entry.
- Expand `ProductSupplier` with purchase-unit metadata, conversion to inventory units, current/reference cost, and the data required to snapshot those values on purchase lines.
- Persist a complete purchase header and immutable lines, including supplier, document/reference, dates, status, operator audit data, subtotal, VAT mode/rate/amount, and total.
- Provide purchase history with supplier, status, document, and date filters, backed by persistence indexes appropriate for those queries.
- Confirm a purchase, stock receipt, inventory movements, historical line snapshots, and `Product.CostPrice` update in one transaction.
- Update `Product.CostPrice` only at confirmation using weighted-average cost over received inventory units; retain each purchase line's actual cost snapshot.
- Treat VAT as additional to the line subtotal and persist the calculated header VAT fields as the source of truth for history and display.
- Introduce `SupplierQuotation` as a supplier-commercial aggregate separate from customer `Quote`. Accepting it may preload a purchase draft only; it MUST NOT receive stock or create inventory movements.
- Deliver a data-compatible migration that preserves existing supplier, purchase, line, and stock-history records without inferring or reinterpreting legacy quantities, units, costs, or totals.

### Out of Scope

- Changes to customer quotation, sale conversion, or customer communication behavior.
- Supplier payment, accounts-payable, invoice settlement, returns, or supplier credit-note workflows.
- Retrospective recalculation of product costs or migration of legacy purchase values into new unit conversions.
- Replacing the application-wide audit-trail subsystem or redesigning unrelated catalog and inventory screens.

## Affected Capabilities

- **Modified** `supplier-purchases-flow`: audited purchase header, line, history, and confirmation behavior.
- **Modified** `inventory-management`: supplier receipt movements become transactionally tied to purchase confirmation.
- **Modified** `product-catalog`: supplier-specific purchasing units, conversion, costs, and immutable snapshots.
- **New** `supplier-quotations`: independent supplier quotations that can preload, but never confirm, a purchase.

## Clean Architecture Boundaries

| Layer | Responsibility |
|---|---|
| Domain | Purchase, purchase lines, `ProductSupplier`, and `SupplierQuotation` invariants; weighted-average cost calculation inputs. |
| Application | Validate active catalog selection, orchestrate draft/confirmation/history workflows, and define transactional ports. |
| Infrastructure | EF Core mappings, indexes, data-compatible migrations, repositories, and SQLite transaction implementation. |
| Presentation | Collect inputs, show calculated persisted results and history; no stock, cost, or tax business logic. |

## Risks and Rollback

The main risks are financial-history integrity, incorrect unit conversion, and partial stock updates. Confirmation will be a single transaction and historical values will be immutable snapshots. The migration will add compatible structures and preserve legacy values as recorded; it will not derive new meanings from old data.

Before deployment, back up affected SQLite databases and validate the migration against representative production copies. If release rollback is required before new data is entered, revert the application and migration. After new purchases or quotations exist, restore the verified pre-release backup or use a forward corrective migration; do not run a destructive downgrade that could discard new operational records.

## Review Acceptance Criteria

- A confirmed purchase is traceable from its header and immutable lines to its stock movements and confirming operator.
- A failed confirmation leaves no persisted purchase, stock, movement, or cost update.
- Purchase history filters remain performant through explicit persistence indexes.
- VAT totals shown after confirmation come from the stored purchase header.
- Accepted supplier quotations only create editable purchase drafts and never alter stock.
- Existing records survive migration with their original stored values and semantics unchanged.
