# Cashier Sales Flow Specification

## Purpose

Defines the user-visible cashier sale outcome: confirmed sales become durable business records and dashboard metrics reflect real sale data.

## Requirements

### Requirement: Persisted cashier sale confirmation

The system MUST create a durable sale when a cashier confirms a valid cart with customer/payment data.

#### Scenario: Confirm valid sale

- GIVEN a cashier has a valid cart, payment, and customer context
- WHEN the cashier confirms the sale
- THEN a sale record MUST exist with line items, payment, totals, and invoice number
- AND inventory effects MUST be represented according to the sale outcome

#### Scenario: Reject invalid confirmation

- GIVEN the cart is empty or payment data is incomplete
- WHEN the cashier confirms the sale
- THEN no sale MUST be persisted
- AND the cashier MUST receive a recoverable validation result

### Requirement: Real dashboard sales metrics

The system MUST show dashboard sales metrics from persisted sales rather than prototype or hardcoded values.

#### Scenario: Metrics reflect persisted sales

- GIVEN persisted sales exist for the dashboard period
- WHEN the dashboard metrics are loaded
- THEN totals and counts MUST match persisted sale records

#### Scenario: Metrics handle no sales

- GIVEN no persisted sales exist for the dashboard period
- WHEN the dashboard metrics are loaded
- THEN totals and counts MUST show zero-state values without errors
