# Customer Management Specification

## Purpose

Define operational customer records, lookup, and customer-linked history without turning receivable accounting into customer CRUD.

## Requirements

### Requirement: Maintain customer master records

The system MUST create, update, and deactivate customer records with enough identity data to support sale linkage and later lookup.

#### Scenario: Register a searchable customer

- GIVEN an operator provides the required customer identity data
- WHEN the customer record is saved
- THEN the customer MUST be available for later sale selection and search results

#### Scenario: Reject incomplete customer registration

- GIVEN the required customer identity data is missing
- WHEN the operator attempts to save the record
- THEN the system MUST reject the save with a recoverable validation result

### Requirement: Show customer-linked operational history

The system MUST show persisted customer-linked sales and status history, and customer-master actions SHALL NOT settle or rewrite receivable balances.

#### Scenario: View customer history

- GIVEN a customer has persisted linked sales or receivable activity
- WHEN the operator opens that customer history
- THEN the system MUST show the persisted linked records for that customer

#### Scenario: Handle customer with no history

- GIVEN a customer has no linked sales or receivable activity
- WHEN the operator opens that customer history
- THEN the system MUST show a zero-state result without errors
