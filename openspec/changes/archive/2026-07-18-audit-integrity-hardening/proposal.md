# Proposal: Harden Audit and Financial Integrity

## Motivation

Confirmed audit findings allow financial, identity, and persistence records to diverge: returns do not persist refund disbursements, caller-supplied refund amounts can alter accounting, partial-return loyalty corrections are incomplete, and high-risk commands can use an actor other than the session actor recorded in the audit trail. Product updates, quote numbering, repository commits, and SQLite schema rollout also lack the protections required for concurrent or legacy data.

This change hardens only those confirmed boundaries. It prioritizes financial consistency first, then canonical authorization and actor identity, EF Core concurrency, and safe SQLite migrations.

## Scope

### In Scope

- Derive return amounts from immutable completed-sale data; persist classified refund settlements; reconcile receivables; and include return outflows and returned sales correctly in operational reporting.
- Apply cumulative, delta-based, idempotent loyalty compensation across multiple partial returns.
- Use the authenticated, active session user as the canonical actor and enforce existing action permissions in the Application layer for sales, returns, receivable payments, stock adjustments, purchases, shifts, and cash closing.
- Restrict shift start and close operations to the session owner; no administrator override is added.
- Add optimistic concurrency protection for products and return a retryable conflict without replaying financial operations.
- Add audit-critical, non-polymorphic operational foreign keys only after preflight validation; add a duplicate-username preflight before the existing unique-index rollout.
- Move user persistence commits to the Application-owned unit of work and allocate quote numbers through a transactional, year-scoped counter.

### Out of Scope

- General-ledger accounting, payment-gateway integration, automatic financial-command retries, or a reporting rewrite beyond return effects.
- A global RBAC redesign, new administrator override permissions, or a UI redesign beyond adapting changed Application contracts.
- Foreign keys for polymorphic references, automatic repair of orphaned records, automatic username renaming, or case-insensitive username semantics.
- A shared document-sequence framework or hardening invoice numbering; only quote numbering is included.

## Capabilities

### Modified Capabilities

- `cashier-sales-flow`: make returns eligible only from completed sales and preserve the financial effect of a classified refund.
- `customer-receivables`: reconcile return reductions against derived amounts and persist only valid customer/sale links.
- `loyalty-points`: compensate cumulatively and idempotently for repeated partial returns.
- `inventory-management`: protect stock-affecting product updates with optimistic concurrency and durable operational references.
- `reporting-cash-closing`: derive billed sales, cash, digital collections, and return outflows from persisted records without conflating physical cash and digital value.
- `user-auth`: make the active authenticated session the canonical authorization and audit actor for targeted high-risk commands.
- `supplier-purchases-flow`: require Application-layer authorization for purchase registration and preserve its atomic stock outcome.
- `quotations-ui-flow`: allocate distinct, non-reusable quote numbers safely under concurrent creation.
- `audit-trail`: ensure targeted mutations record the same canonical actor authorized by the Application layer.

## Delivery Strategy

Implementation MUST use the following review units. Each unit, including its focused tests and migration where applicable, MUST remain below 400 changed lines. If a unit exceeds the budget, it MUST be split before review rather than merged into an oversized PR. Each unit MUST leave the solution buildable and its focused tests passing.

| Order | Review unit | Outcome | Rollback boundary |
|---|---|---|---|
| 1 | Return contract and derivation | Remove caller-controlled refund money; derive return totals from immutable sale lines and accept only completed sales. | Revert the contract and its callers together before production rollout. |
| 2 | Return settlement and reporting | Persist one classified return settlement; reconcile receivables; report dated cash/digital outflows and historical gross billing. | Revert Application/report behavior; restore the pre-deployment database backup if reconciliation is wrong. |
| 3 | Cumulative loyalty compensation | Append only the unpaid compensation delta across partial returns. | Revert behavior only after written compensation entries exist; do not delete ledger evidence. |
| 4 | Canonical actor and shift ownership | Enforce existing permissions and session-derived active actor for targeted commands; restrict shift lifecycle to its owner. | Revert the Application contract and both presentation consumers as one unit; never restore a caller-supplied actor fallback. |
| 5 | Product concurrency | Add a product version token, centralized version advancement, and recoverable conflict translation. | Revert token mapping and handling only after confirming no concurrent writer relies on the migration. |
| 6 | Referential and migration preflight | Validate duplicate usernames and operational orphans before applying additive indexes/foreign keys. | Abort before schema mutation on preflight failure; if applied, restore the verified database backup. |
| 7 | Unit-of-work ownership | Make user repositories stage changes only and commit user-plus-role work through `IUnitOfWork`. | Revert the use case and repository change together. |
| 8 | Quote counter allocation | Add the year-scoped transactional counter seeded from existing quote numbers. | Revert application behavior and the additive counter migration only before counters are used; preserve issued quote numbers. |

Units 1 and 2 are intentionally separate so financial input integrity can be reviewed before persistence and reporting effects. Units 3 through 8 are independent after the final return contract is established. Work-unit commits MUST contain the behavior, focused tests, and migration/preflight changes for that unit; commits are candidate chained PRs and MUST use conventional commit messages.

## Migration and Rollback Policy

1. Before any schema-changing migration, create and verify a restorable database backup.
2. Run read-only preflights for duplicate usernames and each new audit-critical foreign-key relationship. Diagnostics MUST identify the offending identifiers and MUST fail before schema mutation.
3. Do not delete orphaned data, rename usernames, or silently repair records. Operators repair data explicitly, then rerun the preflight.
4. Migrations MUST be additive where possible. Once a settlement, loyalty correction, quote number, or other audit evidence has been written, rollback MUST preserve that evidence; use application rollback or a separately audited data migration instead of destructive `Down` behavior.
5. For a product concurrency conflict, reload state and return a retryable result. The system MUST NOT automatically replay a sale, return, payment, purchase, stock adjustment, shift, or cash-closing command.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Historical return reporting changes totals | Separate gross billed sales from dated refund outflows; test ranges spanning an original sale and its later return. |
| Legacy data prevents constraints | Preflight and abort before migration; keep an operator-visible remediation list and verified backup. |
| Contract changes break WPF or Avalonia/shared callers | Update both consumers in the same review unit and retain focused Application contract tests. |
| Retried commands duplicate accounting entries | Use deterministic derivation and persisted links/constraints so return settlement and loyalty compensation remain idempotent. |
| Concurrent stock commands lose updates | Use EF Core optimistic concurrency and a recoverable conflict; do not retry financial commands automatically. |

## Success Criteria

- A partial or full return cannot change its monetary result through a caller-supplied refund amount, and every cash or digital refund has one persisted, classified outflow.
- Operational reporting separates gross billing, cash, digital value, receivable settlements, and return outflows; expected drawer cash reflects only physical cash effects.
- Multiple valid partial returns result in the same total loyalty correction as a full return, without duplicate compensation.
- Targeted high-risk mutations reject unauthenticated, inactive, or unauthorized sessions, and their audit actor matches the authorized session actor.
- A cashier cannot start or close another cashier's shift; the owning active cashier can close their open shift.
- Concurrent SQLite product updates produce a recoverable conflict rather than silently losing the first persisted stock change.
- Duplicate usernames or orphaned critical operational references halt a migration before schema mutation, while valid legacy data migrates and new orphaned records are rejected.
- User use cases commit atomically through `IUnitOfWork`, and concurrent quote creation produces distinct year-scoped numbers without reuse after deletion.
- Focused unit/SQLite integration tests pass for every review unit, followed by `dotnet test POS.sln --no-restore`, `dotnet build POS.sln --no-restore`, and `dotnet format POS.sln --verify-no-changes`.

## Next Phase

`sdd-spec` must define RFC 2119 requirements and Given/When/Then scenarios for the modified capabilities, preserving the delivery and rollback boundaries in this proposal.
