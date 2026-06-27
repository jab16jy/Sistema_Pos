# Design: Complete Partial POS Runtime

## Technical Approach

Close runtime gaps with bounded infrastructure improvements, not new modules. Keep Application contracts stable and implement WPF composition, options, persisted lookup, and adapter behavior in Infrastructure/Presentation.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Runtime config | Add typed option classes in Infrastructure and register defaults from WPF | Full `SystemConfiguration` table first | Fixes construction and platform issues without expanding into Settings UI. |
| WhatsApp | Launch configured executable/script directly via `ProcessStartInfo` | Keep `/bin/bash`; integrate Meta API | Windows WPF target requires no Unix shell; docs explicitly reject Meta API for this version. |
| Product lookup | Add EF-backed `ProductLookupService : IProductLookupService` | Keep prototype lookup in WPF | Implemented cashier/quote flows should use persisted data while tests can inject doubles. |
| Report export | Implement daily report bytes with current persisted totals, likely QuestPDF/PDF first | Full PDF+Excel suite now | Avoids stubs while keeping review scope sane. |
| Printing | Make printer command/config required and fail recoverably when absent | Fake success; full printer profile matrix | Sale persistence must not be lost; unsupported hardware should be explicit. |

## Data Flow

    WPF App.xaml.cs
        -> AddPosInfrastructure(connection, options)
        -> ViewModels
        -> Application use cases / interfaces
        -> Infrastructure services and EF repositories
        -> SQLite / SMTP / external script / file bytes

For quote sending:

    QuoteHistoryViewModel -> SendDocumentUseCase -> PdfService
        -> IEmailService or IWhatsAppService
        -> DocumentSendLogRepository -> AppDbContext

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/POS.Infrastructure/DependencyInjection.cs` | Modify | Accept/register runtime options and EF product lookup. |
| `src/POS.Presentation/App.xaml.cs` | Modify | Build runtime options and inject `IProductLookupService` into cashier/quote flows. |
| `src/POS.Infrastructure/Services/SmtpEmailService.cs` | Modify | Use typed SMTP options and fail clearly when incomplete. |
| `src/POS.Infrastructure/Services/WhatsAppScriptService.cs` | Modify | Use configured Windows-compatible process path/arguments. |
| `src/POS.Infrastructure/Services/ReportService.cs` | Modify | Generate non-empty daily report bytes from persisted sales/payment totals. |
| `src/POS.Infrastructure/Services/PrintService.cs` | Modify | Remove `NotImplementedException`; fail recoverably when printer config is absent. |
| `src/POS.Infrastructure/Persistence/Queries/ProductLookupService.cs` | Create | Query active products by name/barcode and map DTOs. |
| `tests/POS.Application.Tests` / `tests/POS.Presentation.Shared.Tests` | Modify | Add strict TDD coverage for changed behavior. |

## Interfaces / Contracts

Keep existing Application interfaces:

```csharp
IEmailService, IWhatsAppService, IPdfService, IPrintService, IReportService, IProductLookupService
```

Add infrastructure-only options:

```csharp
SmtpOptions { Host, Port, User, Password, UseSsl, FromAddress }
WhatsAppScriptOptions { FileName, ArgumentsTemplate, WorkingDirectory }
PrinterOptions { Command, ArgumentsTemplate }
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|--------------|----------|
| Application | Send failures still log results | Existing fake services plus new failure cases. |
| Infrastructure | DI resolves services; lookup queries active products | SQLite in-memory/temp database tests. |
| Presentation.Shared | ViewModels remain injectable/testable | Existing tests continue using doubles/prototype. |
| Manual Windows | WPF startup and script config paths | Run app after automated tests. |

## Migration / Rollout

No migration required. Runtime options are code/app configuration first; `SystemConfiguration` remains future work.

## Open Questions

- [ ] Exact production SMTP/script/printer settings are still operator/environment decisions.
