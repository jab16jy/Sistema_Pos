# Proposal: Customer Suppliers Reporting Foundation

## Proposal question round
Automatic mode prevented a live question round. Assumptions to confirm later: receivables stay separate from customer master data; purchases must update stock and inventory traceability atomically; reporting prioritizes operational accuracy before advanced analytics.

## Intent
Create clean RF-04/RF-05/RF-06 foundations for customers, receivables, suppliers, purchases, and reporting/cash closing without pushing business rules into UI code.

## Scope
### In Scope
- Customer master data, search/filter, sale linkage, and history foundation.
- Receivables for credit balances, debt payments, and pending-balance reporting inputs.
- Supplier master, purchase registration, stock increase, and inventory-movement traceability.
- Reporting/cash-closing contracts with mandatory physical-cash vs digital-payment separation.
- Shared ViewModel foundations plus Avalonia prototype wiring.

### Out of Scope
- Advanced BI dashboards/charts beyond operational reporting foundations.
- WPF-specific production UI work.
- Broad cashier rewrites beyond required contract alignment.
- Implementation, verification, rollout, or migration execution.

## Capabilities
### New Capabilities
- `customer-management`: customer records, search, filters, and customer-linked history.
- `customer-receivables`: credit balances, debt payments, balance tracking, receivables report inputs.
- `supplier-purchases-flow`: supplier management, purchase registration, stock increase, inventory-movement audit trail.
- `reporting-cash-closing`: operational reports, cash-closing workflow, export seams, cash-vs-digital totals.

### Modified Capabilities
- `cashier-sales-flow`: align sale/payment contracts only where credit settlement or reporting totals require shared behavior.

## Approach
Use Application use cases with explicit repository/query ports, persisted reporting queries, reusable state in `POS.Presentation.Shared`, and Avalonia-only wiring in `POS.Presentation.Avalonia`. Keep Clean Architecture boundaries intact.

## Affected Areas
| Area | Impact | Description |
|------|--------|-------------|
| `src/POS.Domain` | Modified | Customer, supplier, purchase, receivable, closing rules |
| `src/POS.Application` | Modified | Use cases, ports, report/read-model contracts |
| `src/POS.Infrastructure` | Modified | EF Core repositories, transactions, report/export services |
| `src/POS.Presentation.Shared` | Modified | Shared ViewModels and state |
| `src/POS.Presentation.Avalonia` | Modified | Prototype screens/navigation |
| `openspec/specs/` | New/Modified | New capabilities plus narrow cashier delta |

## Risks
| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Customer/receivable scope blur | High | Separate capabilities/contracts |
| Reporting on in-memory queries | Medium | Require persisted query layer |
| Missing inventory audit trail | High | Make purchase stock/movement atomic |

## Rollback Plan
Rollback by capability slice: hide new navigation, keep current cashier/dashboard paths, and revert new contracts behind unchanged existing flows.

## Dependencies
- Existing sale/payment aggregates and dashboard summary foundation.

## Success Criteria
- [ ] Specs map cleanly to the five capabilities above.
- [ ] Design preserves Clean Architecture and Avalonia/shared ViewModel boundaries.
- [ ] Reporting and cash closing explicitly separate physical cash from digital payments.
- [ ] Planning stops cleanly after proposal/spec/design/tasks.
