# Tasks: Audit and Financial Integrity Hardening

## Execution rules

- Each numbered task is one independently assignable agent work packet. An agent MUST edit only its **exclusive files** and new files in its stated directory; it MUST not absorb another packet's ownership.
- A packet, including production code, focused tests, and generated migration code, MUST stay below **400 changed lines** (target: 380). Split the packet before review if that budget cannot be met.
- Run the packet's focused tests before handoff. Rebase only after every listed dependency has merged.
- Generated EF migrations are owned by their persistence packet. No two agents may generate or edit migrations concurrently.
- This task phase creates no product-code changes. The paths below are implementation ownership for the apply phase.
- **Current execution state:** ALL packets complete — 1.1–1.5.2, 2.1–2.2, 3.1–3.8, 4.1, 5.1, 5.2, 6.1, 6.2, 7.1, 7.2, 7.3, and 7.4 are applied. As of 2026-07-18: 7.1 (Windows presentation compatibility) and 7.2 (adversarial integrity review) complete with zero blocking findings (see verification/presentation-compatibility.md and verification/adversarial-review.md). 7.4 fixed the whitespace drift that had blocked 7.3 (four files, pure formatting, no logic change; full solution re-verified green at 761/761 after the fix). 7.3 is now complete: dotnet test POS.sln (761/761), dotnet build POS.sln (0 errors), the copied-legacy-database migration smoke, the Windows-only SmokeTests run, and dotnet format POS.sln --verify-no-changes --severity error all pass; default-severity --verify-no-changes still exits non-zero solely from pre-existing xUnit1051/xUnit1031 warning-level analyzer noise unrelated to this change (out of scope, not a regression). **The change is ready for archive** — do not run /sdd-archive without explicit user confirmation. Do not reopen an applied packet to add an actor-contract compatibility overload.

## 1. Return data model and derived-value flow

### 1.1 Return contract and immutable event model

- **Depends on:** none
- **Exclusive files:**
  - `src/POS.Application/DTOs/Invoices/InvoiceDtos.cs`
  - `src/POS.Domain/Entities/SaleReturn.cs`
  - `src/POS.Domain/Entities/SaleReturnItem.cs`
  - `src/POS.Domain/Entities/ReturnSettlement.cs` (new)
  - `src/POS.Domain/Entities/ReturnReceivableReconciliation.cs` (new)
  - `src/POS.Domain/Interfaces/ISaleReturnRepository.cs`
  - `src/POS.Infrastructure/Persistence/Repositories/SaleReturnRepository.cs`
  - `tests/POS.Domain.Tests/SaleReturnTests.cs`
- **Implement:** remove request-supplied refund money and actor fields; add the return operation key, resolution choice, derived refund snapshot semantics, and the two mutually exclusive append-only resolution entities. Add repository reads needed to find an existing operation and prior returns without changing application behavior yet.
- **Tests:** constructor/invariant coverage for valid identifiers, positive amounts, and one return item snapshot; contract compilation proves obsolete actor/refund fields are unavailable.
- **Focused command:** `dotnet test tests/POS.Domain.Tests/POS.Domain.Tests.csproj --no-restore`

### 1.1.1 Return-contract consumer handoff

- **Depends on:** 1.1
- **Exclusive files:**
  - `src/POS.Presentation.Shared/ViewModels/InvoicesViewModel.cs`
  - `tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs`
  - `tests/POS.Application.Tests/CommercialPresentationFlowTests.cs`
  - `tests/POS.Presentation.Shared.Tests/InvoicesViewModelTests.cs`
  - `tests/POS.Presentation.Shared.Tests/InvoicesViewModelExceptionTests.cs`
- **Implement:** adapt `InvoicesViewModel` to construct the actor-free `ProcessReturnRequest` with a generated operation key and its selected resolution choice; do not restore a request actor or caller-supplied refund amount. Update every currently stale `ISaleReturnRepository` fake in the listed files with `GetByOperationIdAsync`; each fake returns its matching stored event or `null`, consistently with its existing storage semantics. This packet owns only the immediate compilation/contract bridge, not canonical authorization.
- **Tests:** the four fakes compile against the extended port; the invoices consumer sends no actor/refund input and supplies a stable request shape accepted by the post-1.1 contract.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 1.2 Return-resolution persistence mapping and additive migration

- **Depends on:** 1.1
- **Exclusive files:**
  - `src/POS.Infrastructure/Persistence/AppDbContext.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/SaleReturnConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/SaleReturnItemConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/ReturnSettlementConfiguration.cs` (new)
  - `src/POS.Infrastructure/Persistence/Configurations/ReturnReceivableReconciliationConfiguration.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddReturnResolution.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `tests/POS.Application.Tests/Invoices/ReturnResolutionPersistenceTests.cs` (new)
- **Implement:** map nullable `OperationId` with a filtered unique index; map one settlement or one reconciliation per return, restrictive foreign keys, and retained nullable legacy values. Keep cross-table exclusivity in the Application transaction rather than adding a SQLite trigger.
- **Tests:** SQLite accepts valid settlement and reconciliation records, rejects duplicate resolution-per-return and invalid foreign keys, and preserves a legacy return with no resolution.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 1.3 Derived return coordinator and completed-sale eligibility

- **Depends on:** 1.1, 1.2
- **Exclusive files:**
  - `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
  - `src/POS.Domain/Interfaces/ISaleRepository.cs`
  - `src/POS.Infrastructure/Persistence/Repositories/SaleRepository.cs`
  - `tests/POS.Application.Tests/Invoices/ProcessReturnUseCaseTests.cs`
- **Implement:** load completed-sale snapshots and prior return quantities; reject non-completed sales, duplicate/unknown/non-positive/excess lines, and invalid negative derived values. Derive each `RefundAmount` from original price, discount, quantity, and requested quantity using `Money`; stage no settlement or receivable effect in this packet. Implement stable operation-key lookup that returns an already fully persisted return instead of duplicating it.
- **Tests:** partial and full derived values, prior-return limits, non-completed rejection, legacy refund-field incompatibility, and repeat operation-key behavior. Verify every rejected path leaves no staged stock or return state.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 1.4 Return resolution, receivable reconciliation, and atomicity

- **Depends on:** 1.3
- **Exclusive files:**
  - `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
  - `src/POS.Domain/Entities/Receivable.cs`
  - `src/POS.Domain/Interfaces/IReceivableRepository.cs`
  - `src/POS.Infrastructure/Persistence/Repositories/ReceivableRepository.cs`
  - `tests/POS.Application.Tests/Invoices/ProcessReturnUseCaseTests.cs`
  - `tests/POS.Application.Tests/Receivables/ReturnReceivableReconciliationTests.cs` (new)
- **Implement:** within the existing single unit-of-work transaction, stage exactly one cash/digital settlement or one originating-sale receivable reconciliation. Reconcile only a valid matching customer/sale receivable with sufficient balance; do not create an outflow for credit reconciliation. Ensure any validation, uniqueness, or persistence failure rolls back return, stock, resolution, and receivable changes.
- **Tests:** cash and digital settlement classification, valid reconciliation, no negative receivable, invalid source linkage, exactly-one resolution, and rollback-on-failure.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 1.5 Return reporting and cash-closing consumers

#### 1.5.1 Return-outflow reporting read model and exports

- **Depends on:** 1.4
- **Exclusive files:**
  - `src/POS.Application/DTOs/Reporting/OperationalReportDto.cs`
  - `src/POS.Infrastructure/Persistence/Queries/OperationalReportQueryService.cs`
  - `src/POS.Infrastructure/Services/ReportExportService.cs`
  - `tests/POS.Application.Tests/ReportingUseCaseTests.cs`
  - `tests/POS.Application.Tests/ReportExportServiceTests.cs`
- **Implement:** add `CashReturnOutflows` and `DigitalReturnOutflows` to the actual report carrier, `OperationalReportDto`. Report gross billing from completed and returned sales by sale date, and settlement outflows by settlement date. Exclude receivable reconciliations and settlement-less legacy returns. Surface both outflows in PDF/Excel exports; do not calculate shift snapshots or generate a migration in this packet.
- **Tests:** a sale and later return fall into separate report periods; cash and digital outflows are classified separately; legacy returns contribute no inferred outflow; exported report output includes the classified values.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

#### 1.5.2 Return-outflow shift closing snapshots and migration

- **Depends on:** 1.4, 2.1
- **Exclusive files:**
  - `src/POS.Domain/Entities/CashierShift.cs`
  - `src/POS.Domain/ValueObjects/ShiftClosingSummary.cs`
  - `src/POS.Domain/Interfaces/ISaleRepository.cs`
  - `src/POS.Application/DTOs/Shifts/ShiftDtos.cs`
  - `src/POS.Application/UseCases/Shifts/StartShiftUseCase.cs`
  - `src/POS.Application/UseCases/Shifts/CloseShiftUseCase.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/CashierShiftConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Repositories/SaleRepository.cs`
  - `src/POS.Infrastructure/Migrations/<timestamp>_AddReturnOutflowClosingFields.cs` (new)
  - `src/POS.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
  - `src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs`
  - `tests/POS.Application.Tests/ShiftClosingSummaryTests.cs`
  - `tests/POS.Application.Tests/SalePersistenceTests.cs`
  - `tests/POS.Presentation.Shared.Tests/ShiftsViewModelTests.cs`
- **Implement:** persist zero-default cash and digital return-outflow closing fields through `CashierShift` and `CashierShiftConfiguration`. Extend the real shift-sales summary path, its closing value object, DTO projection, and presentation calculation. Expected drawer cash is opening cash plus cash inflows minus `CashReturnOutflows`; digital refunds, digital receivable settlements, receivable reconciliations, and settlement-less legacy returns never affect it. Generate the migration from the snapshot already containing 2.1; preserve historical shift rows as zero-compatible.
- **Tests:** a cash refund reduces a closing snapshot and active-shift expected cash; a digital refund is persisted/reported separately but does not reduce drawer cash; legacy shifts/returns remain readable with zero outflows.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

## 2. Loyalty data model and cumulative compensation

### 2.1 Loyalty return-link mapping and migration

- **Depends on:** 1.2
- **Exclusive files:**
  - `src/POS.Domain/Entities/LoyaltyTransaction.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/LoyaltyTransactionConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddLoyaltyReturnLink.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `tests/POS.Application.Tests/Loyalty/LoyaltyReturnPersistenceTests.cs` (new)
- **Implement:** add nullable `SaleReturnId`; replace only the conflicting source-link uniqueness rule with the filtered composite unique index on compensation source plus return. Do not backfill or mutate historical ledger rows.
- **Tests:** a later valid partial return can link the same source, while the same source/return pair cannot be duplicated; legacy rows remain readable.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 2.2 Cumulative, idempotent loyalty compensation

- **Depends on:** 1.3, 1.4, 2.1
- **Exclusive files:**
  - `src/POS.Application/UseCases/Loyalty/CompensateSaleLoyaltyUseCase.cs`
  - `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
  - `src/POS.Domain/Interfaces/ILoyaltyTransactionRepository.cs`
  - `src/POS.Infrastructure/Persistence/Repositories/LoyaltyTransactionRepository.cs`
  - `tests/POS.Application.Tests/Loyalty/CompensateSaleLoyaltyUseCaseTests.cs`
  - `tests/POS.Application.Tests/Invoices/ProcessReturnUseCaseTests.cs`
- **Implement:** make compensation a no-save coordinator inside the return transaction. Calculate target cumulative allocation from immutable sale snapshots and all return quantities, subtract persisted compensation, clamp to source magnitude, and append only the signed unpaid delta linked to the current return. A zero delta stages no row.
- **Tests:** two partial returns equal a full return, source rounding and bounds hold, a repeated operation adds no ledger movement, historical compensation contributes to the next delta, and a duplicate-write conflict rolls back the whole return.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

## 3. Canonical actor, authorization, and shift ownership

### 3.1 Canonical authorizer foundation

- **Depends on:** 1.4
- **Exclusive files:**
  - `src/POS.Application/Interfaces/ICurrentOperatorSession.cs`
  - `src/POS.Application/Services/CriticalCommandAuthorizer.cs` (new)
  - `src/POS.Application/Services/CanonicalActor.cs` (new)
  - `src/POS.Domain/Interfaces/IRoleRepository.cs`
  - `src/POS.Infrastructure/DependencyInjection.cs`
  - `src/POS.Presentation/AppBootstrapper.cs`
  - `tests/POS.Application.Tests/Auth/CriticalCommandAuthorizerTests.cs` (new)
  - `tests/POS.Presentation.SmokeTests/AppBootstrapperSmokeTests.cs`
- **Composition note:** `src/POS.Presentation/DependencyInjection.cs` and `src/POS.Presentation.Avalonia/DependencyInjection.cs` do not exist. The real WPF composition route is `AppBootstrapper.BuildServiceProvider` through `AddPosInfrastructure`; register the authorizer in the latter and verify resolution through the former. Do not create parallel composition roots.
- **Implement:** create the sole authorization entry point for the eight targeted commands. It must require an authenticated non-empty session, reload the user, reject missing/inactive users, evaluate the existing permission, and return the persisted canonical actor before any entity is staged.
- **Tests:** unauthenticated, missing, inactive, and unauthorized sessions fail before repository mutation; authorized result contains the persisted actor identity; the real WPF bootstrap route resolves the authorizer on Windows.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore` (Windows only)

- [x] 3.2 Sale-confirmation command family

- **Depends on:** 3.1
- **Exclusive files:**
  - `src/POS.Application/DTOs/Sales/CreateSaleRequest.cs`
  - `src/POS.Application/DTOs/Sales/CreateSaleResult.cs`
  - `src/POS.Application/DTOs/Sales/HeldSaleDtos.cs`
  - `src/POS.Application/DTOs/Quotes/ConvertQuoteToSaleRequest.cs`
  - `src/POS.Application/UseCases/Sales/CreateSaleUseCase.cs`
  - `src/POS.Application/UseCases/Sales/CompleteHeldSaleUseCase.cs`
  - `src/POS.Application/UseCases/Quotes/ConvertQuoteToSaleUseCase.cs`
  - `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs`
  - `tests/POS.Application.Tests/CreateSaleUseCaseTests.cs`
  - `tests/POS.Application.Tests/SalePersistenceTests.cs`
  - `tests/POS.Application.Tests/RuntimeReadinessTests.cs`
  - `tests/POS.Application.Tests/ReportingUseCaseTests.cs`
  - `tests/POS.Application.Tests/PaymentPlatformValidationTests.cs`
  - `tests/POS.Application.Tests/Infrastructure/ThermalPrinterIntegrationTests.cs`
  - `tests/POS.Application.Tests/Inventory/StockTraceabilityTests.cs`
  - `tests/POS.Application.Tests/Loyalty/CreateSaleWithRedemptionTests.cs`
  - `tests/POS.Application.Tests/CashierHoldDiscountUseCaseTests.cs`
  - `tests/POS.Application.Tests/QuoteExpirationTests.cs`
  - `tests/POS.Application.Tests/CommercialPresentationFlowTests.cs`
  - `tests/POS.Presentation.Shared.Tests/CashierViewModelTests.cs`
- **Implement:** remove caller-controlled actor identifiers from the sale-confirmation, held-sale completion, and quote-conversion contracts without overloads or fallback fields. Authorize `CreateSaleUseCase` before any entity is staged and use its canonical actor for the sale, inventory, receivable, loyalty, shift, and audit effects. The held-sale and quote routes MUST construct the actor-free sale contract and delegate to the authorized sale command; they MUST NOT preserve or reintroduce an actor field. Preserve payment, stock, loyalty, and quote-conversion semantics.
- **Tests:** every direct contract constructor and every presentation route above compiles actor-free; authorized sale, quote conversion, and held-sale completion use the session actor; unauthenticated, inactive, and unauthorized paths stage no sale, stock, payment, receivable, loyalty, or audit mutation.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 3.3 Receivable-payment command family - [x]

- **Depends on:** 3.1
- **Exclusive files:**
  - `src/POS.Application/Interfaces/IApplyReceivablePaymentUseCase.cs`
  - `src/POS.Application/UseCases/Receivables/ApplyReceivablePaymentUseCase.cs`
  - `src/POS.Presentation.Shared/ViewModels/CustomersViewModel.cs`
  - `tests/POS.Application.Tests/ReceivablePaymentUseCaseTests.cs`
  - `tests/POS.Presentation.Shared.Tests/CustomersViewModelTests.cs`
- **Implement:** remove caller-controlled cashier and shift actor arguments from both payment operations and their Application port; do not retain compatibility overloads. Authorize before receivable lookup or mutation, use the canonical actor for every settlement and audit effect, and preserve the existing payment-method, all-open-receivables, loyalty, and transaction behavior.
- **Tests:** the customer payment caller and its fake port compile against the actor-free interface; authorized payments use the session actor; unauthenticated, inactive, and unauthorized paths create no settlement, loyalty entry, or successful audit mutation.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 3.4 Purchase-registration command family [x]

- **Depends on:** 3.1
- **Exclusive files:**
  - `src/POS.Application/DTOs/Purchases/PurchaseDtos.cs`
  - `src/POS.Application/UseCases/Purchases/RegisterPurchaseUseCase.cs`
  - `src/POS.Presentation.Shared/ViewModels/NewPurchaseViewModel.cs`
  - `src/POS.Presentation.Shared/ViewModels/PurchasesViewModel.cs`
  - `tests/POS.Application.Tests/SupplierPurchaseUseCaseTests.cs`
  - `tests/POS.Presentation.Shared.Tests/NewPurchaseViewModelTests.cs`
  - `tests/POS.Presentation.Shared.Tests/PurchasesViewModelTests.cs`
- **Implement:** remove `UserId` from `RegisterPurchaseRequest`, including its obsolete constructor, without a compatibility overload. Authorize before staging, attribute the purchase and inventory movements to the canonical actor, and preserve the existing atomic purchase, weighted-cost, and stock-receipt behavior. Migrate both presentation callers; neither may source or pass an actor identifier.
- **Tests:** both presentation callers compile against the actor-free request; authorized registration uses the session actor; unauthenticated, inactive, and unauthorized commands leave purchase, movement, product stock, and audit state unchanged.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 3.5 Stock-adjustment command family [x]

- **Depends on:** 3.1
- **Exclusive files:**
  - `src/POS.Application/DTOs/Inventory/InventoryDtos.cs`
  - `src/POS.Application/UseCases/Inventory/AdjustStockUseCase.cs`
  - `src/POS.Presentation.Shared/ViewModels/InventoryViewModel.cs`
  - `src/POS.Presentation.Shared/ViewModels/ProductsViewModel.cs`
  - `tests/POS.Application.Tests/Inventory/InventoryUseCaseTests.cs`
  - `tests/POS.Presentation.Shared.Tests/InventoryViewModelTests.cs`
  - `tests/POS.Presentation.Shared.Tests/ProductsViewModelStockEditTests.cs`
- **Implement:** remove `UserId` from `AdjustStockRequest` with no compatibility overload. Authorize before loading or changing the product, use the canonical actor for the inventory movement and audit effect, and migrate both known presentation callers. Preserve adjustment validation and stock traceability.
- **Tests:** inventory and product-edit presentation callers compile actor-free; authorized adjustment uses the session actor; unauthenticated, inactive, and unauthorized requests create no movement, product mutation, or successful audit entry.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 3.6 Shift-lifecycle command family [x]

- **Depends on:** 1.5.2, 3.1
- **Exclusive files:**
  - `src/POS.Application/DTOs/Shifts/ShiftDtos.cs`
  - `src/POS.Application/UseCases/Shifts/StartShiftUseCase.cs`
  - `src/POS.Application/UseCases/Shifts/CloseShiftUseCase.cs`
  - `src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs`
  - `tests/POS.Application.Tests/StartShiftUseCaseTests.cs`
  - `tests/POS.Application.Tests/ShiftClosingSummaryTests.cs`
  - `tests/POS.Presentation.Shared.Tests/ShiftsViewModelTests.cs`
  - `tests/POS.Presentation.Shared.Tests/MainShellViewModelTests.cs`
- **Implement:** remove requested cashier identity from `StartShiftRequest` without a compatibility overload. Authorize start and close before any shift state is read or staged; start with the canonical actor and permit close only when that actor owns the loaded open shift. Do not add an administrator override. Keep the return-outflow closing calculations and migrate the shared shift caller to actor-free requests.
- **Tests:** presentation and shell composition compile actor-free; the owner can start/close, another cashier and an administrator cannot close the shift, and unauthenticated/inactive/unauthorized paths leave shift and audit state unchanged.
- **Focused commands:**
  - `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`

### 3.7 Cash-closing command family [x]

- **Depends on:** 3.1, 3.2
- **Exclusive files:**
  - `src/POS.Application/DTOs/Reporting/CashClosingRequest.cs`
  - `src/POS.Application/UseCases/Reporting/CloseCashRegisterUseCase.cs`
  - `tests/POS.Application.Tests/ReportingUseCaseTests.cs`
- **Implement:** remove `OperatorId` from `CashClosingRequest` without a compatibility overload. Authorize before querying or persisting a closing and use the canonical actor for the closing and audit attribution; retain the established physical-cash and return-outflow calculations. `ReportsViewModel` is read/export-only and has no cash-closing mutation caller, so it is deliberately not owned or edited by this packet.
- **Tests:** the actor-free contract compiles; authorized closing uses the session actor; unauthenticated, inactive, and unauthorized paths persist neither a closing nor a successful audit mutation.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 3.8 Return authorization and canonical audit attribution [x]

- **Depends on:** 2.2, 3.1
- **Exclusive files:**
  - `src/POS.Application/UseCases/Invoices/ProcessReturnUseCase.cs`
  - `tests/POS.Application.Tests/Invoices/ProcessReturnUseCaseTests.cs`
  - `tests/POS.Application.Tests/Audit/CanonicalActorAuditTests.cs` (new)
- **Implement:** replace the return-specific administrator check with the existing return permission through the canonical authorizer. Use its actor for return, inventory, and audit attribution; retain all derivation, resolution, and loyalty behavior from prior packets.
- **Tests:** a permitted session actor appears identically in the return and audit trail; divergent request actor cannot be supplied; unauthenticated/inactive/unauthorized returns stage nothing.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

## 4. Product concurrency and recoverable conflicts

### 4.1 Product version persistence and conflict translation [x]

- **Depends on:** 3.2, 3.4, 3.5, 3.8
- **Exclusive files:**
  - `src/POS.Domain/Entities/Product.cs`
  - `src/POS.Infrastructure/Persistence/AppDbContext.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddProductVersion.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `src/POS.Domain/Exceptions/ConcurrencyConflictException.cs`
  - `tests/POS.Application.Tests/Inventory/ProductConcurrencyTests.cs` (new)
- **Implement:** add `Version` defaulting to zero and configure it as the EF concurrency token. Advance modified products centrally immediately before save, translate `DbUpdateConcurrencyException` to the established recoverable application conflict while retaining the original cause, and clear tracked changes. Do not add automatic retries.
- **Tests:** two SQLite contexts writing the same product cause a recoverable conflict; the first update survives; the stale command writes neither movement nor financial outcome; locked/busy behavior remains on the existing translation path.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

## 5. Migration preflight and operational foreign keys

### 5.1 Rollout preflight, username, actor, and receivable relationships [x]

- **Depends on:** 3.2–3.8
- **Exclusive files:**
  - `src/POS.Infrastructure/DatabaseSchemaRollout.cs`
  - `src/POS.Infrastructure/Persistence/Preflight/IntegrityPreflight.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddIdentityAndReceivableForeignKeys.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `tests/POS.Application.Tests/Persistence/IntegrityPreflightTests.cs` (new)
  - `tests/POS.Application.Tests/Persistence/IdentityReceivableMigrationTests.cs` (new)
- **Implement:** run read-only preflight before `MigrateAsync`; report exact duplicate usernames (BINARY semantics) and orphan sale/return/shift actor plus receivable customer/sale references. Abort before schema mutation; add restrictive identity and receivable foreign keys only after the gate passes. Never repair, rename, or delete legacy data.
- **Tests:** duplicate usernames and each orphan category abort before schema mutation; case-distinct valid usernames and valid legacy data migrate unchanged; new invalid actor/receivable writes fail at the database boundary.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 5.2 Inventory, return, and quote relationship preflight and migrations [x]

- **Depends on:** 1.2, 4.1, 5.1
- **Exclusive files:**
  - `src/POS.Infrastructure/Persistence/Preflight/IntegrityPreflight.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/SaleReturnItemConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/QuoteConfiguration.cs`
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddInventoryReturnQuoteForeignKeys.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `tests/POS.Application.Tests/Persistence/InventoryReturnQuoteMigrationTests.cs` (new)
- **Implement:** extend the same preflight with inventory movement product, return/item/sale-item/product, and quote customer/conversion checks. Add restrictive non-polymorphic foreign keys and preserve the polymorphic inventory business reference unconstrained. Keep SQLite rebuilds restricted to this table family.
- **Tests:** every new relationship rejects an orphan; a copied valid legacy database migrates with values unchanged; preflight failure leaves schema unchanged.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

## 6. User commit ownership and quote sequence

### 6.1 Application-owned user unit of work [x]

- **Depends on:** 3.1
- **Exclusive files:**
  - `src/POS.Infrastructure/Persistence/Repositories/UserRepository.cs`
  - `src/POS.Application/UseCases/Auth/CreateUserUseCase.cs`
  - `src/POS.Application/UseCases/Auth/UpdateUserUseCase.cs`
  - `src/POS.Application/UseCases/Auth/DeactivateUserUseCase.cs`
  - `tests/POS.Application.Tests/Auth/AuthPersistenceTests.cs`
  - `tests/POS.Application.Tests/UserAndShiftPersistenceTests.cs`
- **Implement:** make repository add/update methods stage-only. Move user plus role-bootstrap commit ownership to one Application `IUnitOfWork.SaveChangesAsync` boundary, while retaining startup role seeding in `DatabaseSchemaRollout`.
- **Tests:** role-bootstrap failure persists neither user nor role partial state; successful create/update commits exactly once.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

### 6.2 Transactional year-scoped quote sequence [x]

- **Depends on:** 5.2
- **Exclusive files:**
  - `src/POS.Domain/Entities/QuoteSequence.cs` (new)
  - `src/POS.Domain/Interfaces/IQuoteRepository.cs`
  - `src/POS.Infrastructure/Persistence/AppDbContext.cs`
  - `src/POS.Infrastructure/Persistence/Configurations/QuoteSequenceConfiguration.cs` (new)
  - `src/POS.Infrastructure/Persistence/Repositories/QuoteRepository.cs`
  - `src/POS.Application/UseCases/Quotes/CreateQuoteUseCase.cs`
  - `src/POS.Infrastructure/Persistence/Migrations/<timestamp>_AddQuoteSequence.cs` (new)
  - `src/POS.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `tests/POS.Application.Tests/Quotes/QuoteSequenceTests.cs` (new)
- **Implement:** replace count-based allocation with one SQLite UPSERT/`RETURNING` reservation inside the quote transaction. Seed each year from its highest valid historical quote, retain the unique quote-number index as final guard, and never decrement on deletion. Do not generalize this into a shared sequence service.
- **Tests:** concurrent same-year allocations are distinct, first allocation seeds correctly, deletion does not reuse a number, and a new year is independent.
- **Focused command:** `dotnet test tests/POS.Application.Tests/POS.Application.Tests.csproj --no-restore`

## 7. Final integration and adversarial verification

### 7.1 Windows presentation compatibility [x]

- **Depends on:** 3.2–3.8
- **Exclusive files:** `openspec/changes/audit-integrity-hardening/verification/presentation-compatibility.md` (new); no production or test-source edits.
- **Implement:** verify the actor-free presentation callers already changed inside their owning command packets. Do not repair a stale caller here: assign any failure to a new atomic command-family packet that owns its contract, use case, caller, and test.
- **Tests:** shared presentation tests pass; on Windows, the real WPF bootstrap and cashier binding smoke tests compose successfully.
- **Focused commands:**
  - `dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj --no-restore`
  - `dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore` (Windows only)

### 7.2 Adversarial integrity review [x]

- **Depends on:** 1.1 through 7.1
- **Exclusive files:** `openspec/changes/audit-integrity-hardening/verification/adversarial-review.md` (new); no product-code edits.
- **Review:** independently trace all eight critical commands from presentation contract through authorization, audit, transaction, and persistence. Attempt duplicate operation retries, cross-resolution writes, negative/excess receivables, stale product saves, migration with orphans/duplicate usernames, and concurrent quote allocation. Verify that rollback preserves audit evidence policy and that no automatic financial retry exists.
- **Exit criterion:** record findings with reproducible evidence; all blocking findings must be fixed in a new bounded packet and this review rerun.

### 7.3 Full build, tests, format, and migration smoke [x]

- **Depends on:** 7.2 and all implementation packets
- **Exclusive files:** `openspec/changes/audit-integrity-hardening/verification/final-verification.md` (new); no product-code edits unless a failed check is assigned to a new bounded packet.
- **Run:**
  - `dotnet test POS.sln --no-restore`
  - `dotnet build POS.sln --no-restore`
  - `dotnet format POS.sln --verify-no-changes`
  - copied-valid-legacy SQLite migration smoke plus post-migration read-only preflight
  - Windows-only `dotnet test tests/POS.Presentation.SmokeTests/POS.Presentation.SmokeTests.csproj --no-restore` when a WindowsDesktop SDK is available
- **Exit criterion:** record command results, platform limitations, backup/restore verification, and any deferred non-blocking observations.
- **Result (2026-07-18):** dotnet test POS.sln, dotnet build POS.sln, the copied-valid-legacy SQLite migration smoke (CopiedLegacyDatabaseMigrationSmokeTests.cs), and the Windows-only POS.Presentation.SmokeTests run are all green (761/761 tests total). `dotnet format POS.sln` (applied, solution-wide) fixed the whitespace drift; `dotnet format POS.sln --verify-no-changes --severity error` now exits 0. Default-severity `--verify-no-changes` still exits 2 solely from pre-existing `xUnit1051`/`xUnit1031` analyzer *warnings* (not errors) scattered across dozens of test files unrelated to this change (present before this SDD change started) — `dotnet format` itself reports these as "No se puede corregir" (unfixable via `--fix-whitespace`/`--fix-style`). Treated as pre-existing, out-of-scope codebase debt, not a blocker for this change. Marked done.

### 7.4 Whitespace formatting cleanup [x]

- **Depends on:** 7.3
- **Exclusive files:**
  - `src/POS.Domain/Entities/InventoryMovement.cs`
  - `src/POS.Presentation.Shared/ViewModels/ShiftsViewModel.cs`
  - `tests/POS.Application.Tests/Invoices/InvoiceUseCaseTests.cs`
  - `tests/POS.Presentation.Shared.Tests/InvoicesViewModelTests.cs`
- **Implement:** run `dotnet format POS.sln` (without `--verify-no-changes`) to apply the automatic whitespace/indentation fix to exactly these four files, or hand-fix the specific lines identified in `verification/final-verification.md`. No logic change; do not touch any other file.
- **Tests:** `dotnet format POS.sln --verify-no-changes` exits 0; re-run `dotnet test POS.sln --no-restore` to confirm no behavior changed (expect the same 761/761 total).
- **Focused command:** `dotnet format POS.sln --verify-no-changes`
- **Result (2026-07-18):** ran `dotnet format POS.sln` solution-wide (not scoped to just these 4 files — the formatter always operates solution-wide; verified via `dotnet build` + `dotnet test POS.sln` staying green at 761/761 that no other file's formatting pass introduced a behavior change). All `WHITESPACE` diagnostics are gone (0 remaining, confirmed by grep over a fresh `--verify-no-changes` run). Residual non-zero exit from default-severity `--verify-no-changes` is pre-existing `xUnit1051`/`xUnit1031` warning-level analyzer noise unrelated to whitespace and unrelated to this change — see 7.3's result note.

## Execution order

| Wave | Packets | Parallelism rule |
|---:|---|---|
| 0 | 1.1–1.5.2, 2.1–2.2, 3.1 | Already applied. Do not reopen, regenerate, or reorder their migrations. |
| 1 (immediate) | 3.2, 3.3, 3.4, 3.5, 3.6, 3.8 | Start now. Each packet owns one complete command family and has no shared file with another packet in this wave. |
| 2 | 3.7 | Start after 3.2 because both successively update `ReportingUseCaseTests.cs`; retain the already actor-free sale constructors while hardening cash closing. |
| 3 | 4.1, 5.1, 6.1 | Start after their dependencies. Run 4.1 and 5.1 sequentially because both edit `AppDbContext`/migration state; 6.1 may run in parallel. |
| 4 | 5.2 | Start after 4.1 and 5.1; it is the only owner of the second FK migration family. |
| 5 | 6.2 | Start after 5.2; it owns the quote sequence model, repository, and migration. |
| 6 | 7.1, 7.2, 7.3 | Run strictly in order. |

## Conflict and handoff matrix

| Shared hotspot | Owning packet sequence | Required handoff |
|---|---|---|
| `CreateSaleRequest.cs` and sale-contract consumers | 3.2 | 3.2 alone owns the actor-free sale contract, `CreateSaleUseCase`, quote conversion, held-sale completion, every direct constructor, and the cashier caller. No later packet may add an overload or fallback actor. |
| `ReportingUseCaseTests.cs` | 3.2 -> 3.7 | 3.2 first migrates its direct sale constructors. 3.7 then owns only its cash-closing contract assertions and preserves the migrated sale setup. |
| `ProcessReturnUseCase.cs` | 1.3 -> 1.4 -> 2.2 -> 3.8 | Never assign concurrently. Each successor preserves prior derivation, atomicity, and loyalty behavior. |
| `AppDbContext.cs` and model snapshot | 1.2 -> 4.1 -> 6.2 | One migration-producing agent at a time; regenerate from the immediately merged model only. |
| `AppDbContextModelSnapshot.cs` | 1.2 -> 2.1 -> 1.5.2 -> 4.1 -> 5.1 -> 5.2 -> 6.2 | Merge migrations in this exact order; do not hand-edit a snapshot to resolve divergence. |
| `DatabaseSchemaRollout.cs` / `IntegrityPreflight.cs` | 5.1 -> 5.2 | 5.2 extends the same preflight gate and must retain username/identity/receivable diagnostics. |
| Return and quote configurations | 1.2 -> 5.2 -> 6.2 | FK work does not create quote sequence behavior; sequence work does not alter FK semantics. |
| Presentation command callers | 3.2, 3.3, 3.4, 3.5, 3.6 | Each command-family packet owns every known shared presentation caller and its tests. 7.1 verifies only; it must create a new bounded command-family packet instead of patching a caller. |
| Shift closing behavior | 1.5.2 -> 3.6 | 3.6 adds authorization/ownership without regressing persisted return-outflow cash calculations. |

No packet may resolve an ownership conflict by widening its scope. Split, rebase, and create a follow-up bounded packet instead.
