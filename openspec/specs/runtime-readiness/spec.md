# Runtime Readiness Specification

## Purpose

Define runtime behavior for POS features that are already exposed in WPF but still rely on prototype data, missing configuration, platform-specific process launching, or throwing infrastructure stubs.

## Requirements

### Requirement: WPF runtime composition succeeds

The system MUST construct implemented WPF module dependencies without missing configuration services.

#### Scenario: Start WPF with infrastructure services

- GIVEN the WPF app builds its service provider
- WHEN communication services are resolved
- THEN SMTP and WhatsApp services MUST be constructible
- AND missing optional settings MUST produce recoverable send failures, not startup failure

### Requirement: Configurable local communication adapters

The system MUST send documents through local SMTP/script adapters using configurable runtime options and MUST record failures through the existing send log path.

#### Scenario: Email settings are absent

- GIVEN SMTP settings are absent or incomplete
- WHEN a document is sent by email
- THEN the send result MUST be unsuccessful
- AND the error MUST explain that SMTP configuration is missing

#### Scenario: WhatsApp script runs on Windows

- GIVEN a configured script path and arguments exist
- WHEN a document is sent by WhatsApp
- THEN the adapter MUST launch the configured process without assuming `/bin/bash`
- AND the send log MUST record success or failure

### Requirement: Implemented modules do not throw infrastructure stubs

Report and print services MUST return bounded current-scope behavior instead of throwing `NotImplementedException`.

#### Scenario: Generate daily report bytes

- GIVEN persisted sales exist for a date
- WHEN daily report generation is requested
- THEN the report service MUST return non-empty export bytes
- AND cash and digital totals SHOULD remain separated

#### Scenario: Receipt printing is unavailable

- GIVEN no printer command or device is configured
- WHEN receipt printing is requested
- THEN the print service MUST fail with a recoverable configuration message
- AND the sale MUST remain persisted

### Requirement: Implemented WPF flows use persisted product lookup

Cashier and quote creation SHOULD use persisted product search when infrastructure is available.

#### Scenario: Search persisted product

- GIVEN an active product exists in SQLite
- WHEN cashier or quote search requests its name or barcode
- THEN the result MUST come from persisted product data
- AND inactive products MUST NOT be returned
