# Customer Receivables Specification

## Purpose

Define customer credit and debt-settlement behavior as accounting records linked to customers and sales.

## Requirements

### Requirement: Track receivable balances from sales

The system MUST create a customer receivable entry whenever a confirmed sale leaves an unpaid balance, and each entry SHALL remain linked to its customer and originating sale.

#### Scenario: Create receivable from partial-credit sale

- GIVEN a confirmed sale has an identified customer and an unpaid remainder
- WHEN the sale is persisted
- THEN the system MUST create a pending receivable balance linked to that sale

#### Scenario: Reject orphan receivable creation

- GIVEN a sale leaves an unpaid remainder without an identified customer
- WHEN the operator attempts to confirm the sale
- THEN the system MUST reject the operation with a recoverable validation result

### Requirement: Apply debt payments as classified settlements

The system MUST record each receivable payment with amount, date, and payment classification, and it MUST update the outstanding balance without exceeding the amount due.

#### Scenario: Apply partial debt payment

- GIVEN a customer has an open receivable balance
- WHEN the operator records a partial payment
- THEN the system MUST reduce the outstanding balance and keep the receivable open

#### Scenario: Reject overpayment on receivable

- GIVEN a customer has an open receivable balance
- WHEN the operator records a payment greater than the amount due
- THEN the system MUST reject the payment and preserve the prior balance
