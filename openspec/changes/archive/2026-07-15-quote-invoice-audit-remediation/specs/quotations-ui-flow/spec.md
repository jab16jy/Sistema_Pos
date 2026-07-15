</sdd-spec>
<Delta for quotations-ui-flow>
## ADDED Requirements

### Requirement: Invoice summary local timezone display

The system MUST format and display the creation timestamp (`InvoiceSummaryRow.CreatedAt`) of sales and invoices using the Colombian local time zone offset (UTC-5, es-CO).

#### Scenario: Display invoice creation date in local time
- GIVEN a sale created at UTC time "2026-07-14T23:00:00Z"
- WHEN the invoice summary is displayed in the list
- THEN the system MUST format the creation date adjusted to Colombian local time
- AND the UI MUST display "2026-07-14 18:00:00"

## MODIFIED Requirements

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
</Delta for quotations-ui-flow>
