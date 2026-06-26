# Exploration: Complete Partial POS Runtime

## Current State

The WPF-only POS already has Clean Architecture layers, SQLite persistence, WPF navigation, shared ViewModels, quote history/send flows, operational reporting queries, and 103 passing tests. The remaining gap is not broad feature discovery; it is runtime readiness for modules already presented as implemented.

Known gaps:
- `SmtpEmailService` depends on `IConfiguration`, but `App.xaml.cs` registers infrastructure without configuration.
- `WhatsAppScriptService` launches `/bin/bash`, which conflicts with the Windows WPF target.
- `ReportService` and `PrintService` throw `NotImplementedException`.
- `CashierViewModel` and `QuoteViewModel` still use `PrototypeProductLookupService` even though persisted products exist.
- The design docs expect `SystemConfiguration`, but the current code has no entity/repository/table for it.

## Affected Areas

- `src/POS.Presentation/App.xaml.cs` — WPF composition root and SQLite path.
- `src/POS.Infrastructure/DependencyInjection.cs` — infrastructure service registration.
- `src/POS.Infrastructure/Services/SmtpEmailService.cs` — email adapter.
- `src/POS.Infrastructure/Services/WhatsAppScriptService.cs` — external WhatsApp adapter.
- `src/POS.Infrastructure/Services/ReportService.cs` — report export adapter stub.
- `src/POS.Infrastructure/Services/PrintService.cs` — receipt printing adapter stub.
- `src/POS.Infrastructure/Persistence/Repositories/ProductRepository.cs` — candidate source for product lookup.
- `src/POS.Presentation.Shared/ViewModels/CashierViewModel.cs` and `QuoteViewModel.cs` — currently receive prototype lookup.
- `tests/POS.Application.Tests` and `tests/POS.Presentation.Shared.Tests` — existing coverage patterns.

## Approaches

1. **Minimal runtime hardening**
   - Pros: Smallest change; fixes broken service construction and platform mismatch.
   - Cons: Leaves report/print output thin and product lookup partly prototype unless included.
   - Effort: Medium.

2. **Full SystemConfiguration module first**
   - Pros: Matches design docs closely for SMTP and WhatsApp settings.
   - Cons: Larger data/model/UI scope; risks drifting into Settings module.
   - Effort: High.

3. **Adapter options plus persisted lookup**
   - Pros: Closes runtime gaps with typed options, Windows script launching, non-throwing report/print adapters, and persisted lookup without a full Settings UI.
   - Cons: Configuration remains file/env based until Settings/SystemConfiguration is built.
   - Effort: Medium.

## Recommendation

Use approach 3. It finishes the partially implemented runtime paths while preserving architecture boundaries. Treat `SystemConfiguration` as a later Settings feature; this change should introduce typed infrastructure options that can later be backed by database configuration without changing Application contracts.

## Risks

- Report/print implementations can grow beyond a small review if they attempt full PDF/Excel/ESC-POS coverage.
- WhatsApp integration must remain a script handoff, not a Meta API integration.
- Persisted product lookup may require DTO mapping without exposing EF entities to WPF.

## Ready for Proposal

Yes. The scope is clear: make already-exposed runtime paths usable and recoverable, not add new POS modules.
