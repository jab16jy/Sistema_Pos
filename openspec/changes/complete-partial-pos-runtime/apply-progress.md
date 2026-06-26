# Apply Progress: Complete Partial POS Runtime

## Mode

Standard mode. `openspec/config.yaml` has `strict_tdd: false` and `rules.apply.tdd: false`.

## Completed Tasks

- [x] 1.1 DI construction tests cover runtime readiness contracts.
- [x] 1.2 Communication tests cover missing SMTP settings and missing WhatsApp script.
- [x] 1.3 SQLite product lookup tests cover name, barcode, stock, and inactive exclusion.
- [x] 2.1 Runtime option classes created for SMTP, WhatsApp script, and printer command settings.
- [x] 2.2 Infrastructure DI registers runtime options and runtime services.
- [x] 2.3 SMTP service validates required options and fails with a recoverable message.
- [x] 2.4 WhatsApp script service launches configured processes without `/bin/bash`.
- [x] 3.1 EF-backed `ProductLookupService` maps active products to lookup DTOs.
- [x] 3.2 Infrastructure registers `IProductLookupService`.
- [x] 1.4 Report/print tests prove no `NotImplementedException` escapes.
- [x] 4.1 `ReportService.GenerateDailySalesReportAsync` returns non-empty PDF bytes from persisted totals.
- [x] 4.2 `PrintService` uses configured command execution or clear configuration failure.
- [x] 4.3 Printer availability is independent from sale persistence/document sending paths.
- [x] 3.3 WPF composition injects persisted lookup into cashier and quote flows.
- [x] 3.4 Existing ViewModel constructors still support injected fakes/prototype lookup for tests.
- [x] 5.3 README documents runtime environment variables.

## Evidence

- `dotnet test tests\POS.Application.Tests\POS.Application.Tests.csproj --no-restore` passed with 45 tests after runtime options and lookup.
- `dotnet test tests\POS.Application.Tests\POS.Application.Tests.csproj --no-restore` passed with 47 tests after report/print runtime behavior.

## Remaining

- Full solution verify.
