# Proposal: New POS Modules (Loyalty, Profit/Utilities, Barcode/Labels) + Module Gaps

## Intent

The local retail POS lacks three commercially expected capabilities — customer loyalty/points, profit/utilities analytics, and barcode/label generation — and carries correctness gaps in existing modules: full-only returns, no category management UI, and no payment-platform reporting. Owners cannot see product margins, staff cannot print internal barcodes/labels, and there is no loyalty program to retain customers. Most underlying data already exists in the database, making these additions low-risk and high-value.

## Scope

### In Scope
- 3 new modules: Loyalty/Points, Profit/Utilities, Barcode/Labels
- Item-level partial returns in invoices
- Category CRUD UI (entity already persisted)
- Payment-platform validation list + reporting breakdown
- Reports: top-sellers, period comparison, gross margin

### Out of Scope
- Communication message templates (deferred, low priority)
- Direct ESC/POS label printing (use QuestPDF PDF + existing print path)
- Loyalty tiers/automatic point expiry beyond configurable rules
- Multi-store, online sync, electronic invoicing

## Capabilities

### New Capabilities
- `loyalty-points`: loyalty accounts, configurable earn/redeem rules, point transactions, accumulation on sale, redemption at checkout, customer balance view
- `profit-analytics`: `IProfitQueryService` — gross margin per product/period, top-selling products, period comparison, payment-platform breakdown
- `barcode-labels`: barcode image generation (EAN-13 / Code 128) + bulk product label printing

### Modified Capabilities
- `invoice-management`: item-level partial returns — one return record per sale holding a `SaleReturnItem` list
- `product-catalog`: Category repository + CRUD use cases + UI; rename table to `CATEGORIES`
- `cashier-sales-flow`: loyalty redemption step + payment-platform validation list at checkout
- `reporting-cash-closing`: DB-side aggregation rewrite; surface profit/top-seller/comparison/breakdown data

## Approach

Each feature follows the established Clean Architecture flow (Domain → Application port → Infrastructure → Presentation MVVM) and existing navigation pattern — no architectural change. Key decisions: loyalty balance is computed from transactions (single source of truth, no denormalized balance to drift); one partial-return record per sale holds a `SaleReturnItem` list (relaxes the current unique index); new `IProfitQueryService` uses EF `GroupBy` pushed to SQLite, replacing the in-memory loading in `OperationalReportQueryService`; ZXing.Net (SkiaSharp binding) added to `POS.Infrastructure` generates barcode images, rendered to label sheets via QuestPDF. Migrations are grouped: (A) `CATEGORIES` rename + loyalty tables, (B) `SaleReturnItem`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `POS.Domain` | New/Modified | Loyalty entities; `SaleReturnItem` |
| `POS.Application` | New | Loyalty/profit/barcode/category ports + use cases |
| `POS.Infrastructure` | New/Modified | EF configs, 2 migrations, query services, ZXing.Net |
| `POS.Presentation` | New/Modified | Loyalty/Profit/Barcode VMs+Views, Category UI, reports |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| ZXing.Net .NET 9 compatibility | Med | Verify package builds before implementation |
| Partial-return unique index blocks design | Med | Single return container + items; migrate index |
| In-memory report perf at scale | Med | Rewrite with DB-side EF `GroupBy` |
| Category table rename migration | Low | `ToTable("CATEGORIES")` + reversible migration |

## Rollback Plan

Every feature is additive and isolated. Revert per-feature commits and reverse the corresponding EF migration (`dotnet ef database update <previous>`). No existing-module behavior changes without its own migration, so reverting that migration restores prior schema and behavior.

## Dependencies

- ZXing.Net (SkiaSharp binding) NuGet package added to `POS.Infrastructure`

## Success Criteria

- [ ] Points accumulate on sale and redeem as a checkout discount per configurable rule
- [ ] Profit/utilities report shows margin, top-sellers, period comparison, and platform breakdown
- [ ] Barcodes generate and labels print for products
- [ ] Item-level partial returns restore the correct stock quantities
- [ ] Category CRUD UI manages categories and products bind to them
- [ ] No regression in existing modules; full test suite passes
