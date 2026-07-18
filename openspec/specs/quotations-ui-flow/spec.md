# Quotations UI Flow Specification

## Purpose

Defines the WPF quotation workflow from navigation through quote creation, customer contact, sending, and sale conversion.

## Requirements

### Requirement: Quotations navigation and history

The system MUST provide a reachable quotations area where users can create and find quotes.

#### Scenario: Navigate to quotations

- GIVEN the user is in the shell
- WHEN the user selects quotations navigation
- THEN the quotations area MUST open
- AND quote creation and history entry points MUST be available

#### Scenario: Search quote history

- GIVEN quotes exist
- WHEN the user searches or filters quote history
- THEN matching quotes MUST be shown with status and customer context

### Requirement: Quote contact, send, and convert workflow

The system MUST use real customer contact data when sending quotes and MUST allow eligible quotes to convert into sales. A quote is eligible for conversion if its expiration date, compared using Colombian local time (UTC-5), is today or in the future.
(Previously: Quote eligibility check did not use timezone-aware comparisons.)

#### Scenario: Send quote to customer contact

- GIVEN a quote has a customer with usable contact data
- WHEN the user sends the quote
- THEN the send workflow MUST target that customer contact
- AND the quote status MUST reflect the send result

#### Scenario: Convert eligible quote using Colombian local time comparison

- GIVEN a quote with expiration date set to today in Colombian local time (UTC-5)
- WHEN the user attempts to convert the quote to a sale
- THEN the system MUST permit the conversion
- AND a sale workflow MUST be started from the quote contents

#### Scenario: Block conversion of expired quote in Colombian local time

- GIVEN a quote with expiration date set to yesterday in Colombian local time (UTC-5)
- WHEN the user attempts to convert the quote to a sale
- THEN the system MUST reject the conversion
- AND the system MUST display a message indicating the quote has expired

#### Scenario: Block quote send without contact

- GIVEN a quote customer has no usable contact data
- WHEN the user attempts to send the quote
- THEN sending MUST be blocked with a recoverable validation result

### Requirement: Invoice summary local timezone display

The system MUST format and display the creation timestamp (`InvoiceSummaryRow.CreatedAt`) of sales and invoices using the Colombian local time zone offset (UTC-5, es-CO).

#### Scenario: Display invoice creation date in local time

- GIVEN a sale created at UTC time "2026-07-14T23:00:00Z"
- WHEN the invoice summary is displayed in the list
- THEN the system MUST format the creation date adjusted to Colombian local time
- AND the UI MUST display "2026-07-14 18:00:00"

### Requirement: Recoverable quote send runtime

The quote send workflow MUST use constructible communication services and report adapter failures as user-recoverable results.

#### Scenario: Quote email configuration missing

- GIVEN a quote has an email contact but SMTP configuration is missing
- WHEN the user sends the quote by email
- THEN the send result MUST fail without crashing the WPF module
- AND the message SHOULD tell the user to configure SMTP

#### Scenario: Quote WhatsApp script missing

- GIVEN a quote has a phone contact but no WhatsApp script is configured
- WHEN the user sends the quote by WhatsApp
- THEN the send result MUST fail without crashing the WPF module
- AND the message SHOULD tell the user to configure the WhatsApp script

### Requirement: Concurrent-safe year-scoped quote numbering

The system MUST allocate each new quote number through a transactional, year-scoped persisted counter. The counter MUST issue a distinct number under concurrent creation, MUST seed from the highest existing number for that year before its first allocation, and MUST NOT reuse an issued number after quote deletion. This requirement applies only to quote numbering and MUST NOT introduce a shared document-sequence mechanism.

#### Scenario: Allocate concurrent quotes distinctly

- GIVEN two SQLite contexts create quotes for the same year concurrently
- WHEN both quote creations commit
- THEN each quote MUST have a distinct number for that year
- AND neither creation MUST depend on counting existing rows

#### Scenario: Seed the counter from historical quotes

- GIVEN existing quotes for 2026 have a highest issued number of 125
- AND no counter row exists for 2026
- WHEN the first new 2026 quote is created
- THEN the quote number MUST be 126
- AND the counter MUST persist the next allocation state atomically

#### Scenario: Do not reuse a deleted quote number

- GIVEN quote number 126 was issued and later deleted
- WHEN another quote is created for the same year
- THEN the new quote number MUST be greater than 126

#### Scenario: Start a new year independently

- GIVEN the current allocation is for 2026
- WHEN the first quote for 2027 is created
- THEN the system MUST allocate from the 2027 counter without changing 2026 issued numbers

### Requirement: Quote relationship migration safety

New quote customer and quote-conversion relationships MUST reference existing non-polymorphic customer and sale records through database-enforced foreign keys where those references are present. Before enforcement, the migration process MUST preflight legacy orphan references and MUST abort before schema mutation if any are found. Historical quote numbers and valid quote data MUST remain unchanged.

#### Scenario: Abort quote relationship migration for orphan conversion data

- GIVEN a legacy quote conversion references a missing sale
- WHEN the operator runs the migration preflight
- THEN diagnostics MUST identify the orphaned quote reference
- AND the migration MUST not modify the schema or quote data
