# Quotations UI Flow Specification

## Purpose

Defines the Avalonia quotation workflow from navigation through quote creation, customer contact, sending, and sale conversion.

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

The system MUST use real customer contact data when sending quotes and MUST allow eligible quotes to convert into sales.

#### Scenario: Send quote to customer contact

- GIVEN a quote has a customer with usable contact data
- WHEN the user sends the quote
- THEN the send workflow MUST target that customer contact
- AND the quote status MUST reflect the send result

#### Scenario: Convert eligible quote

- GIVEN an eligible quote exists
- WHEN the user converts it to a sale
- THEN a sale workflow MUST be started from the quote contents
- AND the quote MUST remain traceable to the resulting sale outcome

#### Scenario: Block quote send without contact

- GIVEN a quote customer has no usable contact data
- WHEN the user attempts to send the quote
- THEN sending MUST be blocked with a recoverable validation result
