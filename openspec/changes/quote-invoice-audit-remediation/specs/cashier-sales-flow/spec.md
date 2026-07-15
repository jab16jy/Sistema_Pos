</sdd-spec>
<Delta for cashier-sales-flow>
## ADDED Requirements

None.

## MODIFIED Requirements

### Requirement: Persisted cashier sale confirmation

The system MUST create a durable sale when a cashier confirms a valid cart with customer/payment data, including the classified payment outcome needed for cash-versus-digital reporting and any receivable balance created by the confirmation. For sales created via quote conversion, the cashier discount validation policy MUST be bypassed.
(Previously: Cashier discount limits were validated on all sale confirmations, blocking conversion of quotes with pre-approved discounts exceeding standard limits.)

#### Scenario: Confirm valid sale
- GIVEN a cashier has a valid cart, payment, and customer context
- WHEN the cashier confirms the sale
- THEN a sale record MUST exist with line items, payment classifications, totals, and invoice number
- AND inventory effects MUST be represented according to the sale outcome

#### Scenario: Reject invalid confirmation
- GIVEN the cart is empty or payment data is incomplete
- WHEN the cashier confirms the sale
- THEN no sale MUST be persisted
- AND the cashier MUST receive a recoverable validation result

#### Scenario: Confirm sale with deferred balance
- GIVEN a cashier confirms a sale for an identified customer with an unpaid remainder
- WHEN the sale is persisted
- THEN the persisted outcome MUST separate cash collected, digital collected, and receivable balance created
- AND reporting totals MUST treat only collected cash as expected drawer cash

#### Scenario: Bypass cashier discount policy during quote conversion
- GIVEN a quote conversion request with a pre-approved manual line discount of 25% (where standard cashier limit is 20%)
- WHEN the sale is confirmed with the quote conversion flag set to true
- THEN the system MUST bypass discount limit checks
- AND the sale MUST be persisted successfully with the 25% discount
</Delta for cashier-sales-flow>
