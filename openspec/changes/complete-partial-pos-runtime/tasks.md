# Tasks: Complete Partial POS Runtime

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 300-450 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single branch with work-unit commits; split if diff grows past 400 lines |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Runtime options + communication adapters | PR 1 | SMTP/WhatsApp construction and failure recovery. |
| 2 | Persisted lookup + report/print adapters | PR 1 | Keep tests with each behavior. |

## Phase 1: Tests First

- [x] 1.1 Add DI construction tests covering `IEmailService`, `IWhatsAppService`, `IReportService`, `IPrintService`, and `IProductLookupService`.
- [x] 1.2 Add communication tests for missing SMTP settings and missing WhatsApp script returning logged failures.
- [x] 1.3 Add SQLite product lookup tests for name, barcode, stock, and inactive exclusion.
- [ ] 1.4 Add report/print tests proving no `NotImplementedException` escapes.

## Phase 2: Runtime Options and Communication

- [x] 2.1 Create infrastructure option records for SMTP, WhatsApp script, and printer command settings.
- [x] 2.2 Update `DependencyInjection.cs` to register options defaults and runtime services.
- [x] 2.3 Update `SmtpEmailService.cs` to validate required options and return recoverable failures through `SendDocumentUseCase`.
- [x] 2.4 Update `WhatsAppScriptService.cs` to launch configured Windows-compatible processes without `/bin/bash`.

## Phase 3: Persisted Lookup and WPF Wiring

- [x] 3.1 Create `ProductLookupService` over `AppDbContext.Products` mapping to `ProductLookupResult`.
- [x] 3.2 Register `IProductLookupService` in infrastructure.
- [ ] 3.3 Update `App.xaml.cs` to inject persisted lookup into `CashierViewModel` and `QuoteViewModel`.
- [ ] 3.4 Keep test constructors able to inject fakes/prototype lookup.

## Phase 4: Report and Print Runtime Behavior

- [ ] 4.1 Implement `ReportService.GenerateDailySalesReportAsync` with non-empty daily PDF bytes from persisted totals.
- [ ] 4.2 Replace `PrintService` stub with configured command execution or clear configuration failure.
- [ ] 4.3 Ensure sale persistence and document sending do not depend on printer availability.

## Phase 5: Verification and Documentation

- [ ] 5.1 Run `dotnet test POS.sln --no-restore`.
- [ ] 5.2 Run `dotnet build POS.sln --no-restore`.
- [ ] 5.3 Update README or docs only if runtime configuration keys/usage become user-facing.
