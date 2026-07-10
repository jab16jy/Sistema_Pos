# Bulk Product Category Assignment Specification

## Purpose

Defines the category-maintenance workflow that groups multiple active products under one existing category without leaving the Products module.

## Requirements

### Requirement: Select active products for assignment

The system MUST let an operator search active products and select one or more products in the Categories subsection.

#### Scenario: Search and select products

- GIVEN active products exist in the catalog
- WHEN an operator searches and selects products in the Categories subsection
- THEN the system MUST show the matching active products and the selected-product count

### Requirement: Assign selected products to an active category

The system MUST let an operator assign selected products to an active target category.

#### Scenario: Assign a selected batch

- GIVEN an operator selected one or more active products and an active target category
- WHEN the operator confirms assignment
- THEN each selected product with a different category MUST receive the target category
- AND the system MUST persist the changed products in one unit-of-work save
- AND the Products list MUST refresh to show the new categories

### Requirement: Reject an invalid batch without partial changes

The system MUST validate the target category and every selected product before it changes any product category.

#### Scenario: A selected product is missing

- GIVEN a batch includes a product that no longer exists
- WHEN the operator submits the assignment
- THEN the system MUST report a recoverable error
- AND the system MUST NOT change any selected product
- AND the system MUST NOT save the unit of work

### Requirement: Keep category selection operationally safe

The system MUST offer only active categories as bulk-assignment targets in the user interface.

#### Scenario: Inactive category

- GIVEN a category is inactive
- WHEN the operator opens the assignment target list
- THEN the inactive category MUST NOT be offered as a target
