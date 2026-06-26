# Delta for Quotations UI Flow

## ADDED Requirements

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
