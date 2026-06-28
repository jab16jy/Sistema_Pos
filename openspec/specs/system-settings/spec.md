# System Settings Specification

## Purpose

Defines business configuration, printer setup, backup management, and currency settings.

## Requirements

### Requirement: Business information

The system MUST store and allow editing business identity information.

#### Scenario: Set business name

- GIVEN no business name is configured
- WHEN the user sets business name to "Esthetic Medical Store"
- THEN the business name MUST be persisted
- AND receipts MUST display "Esthetic Medical Store" as the business name

#### Scenario: Set business address

- GIVEN the user updates the business address
- WHEN the address is saved
- THEN receipts and reports MUST reflect the new address

#### Scenario: Set business tax ID

- GIVEN the user sets a tax identification number
- WHEN the tax ID is saved
- THEN receipts MUST display the tax ID

### Requirement: Printer configuration

The system MUST allow configuring the receipt printer command and working directory.

#### Scenario: Set printer command path

- GIVEN no printer command is configured
- WHEN the user sets the printer command to "/usr/local/bin/print-receipt"
- THEN the PrintService MUST use this command for receipt printing

#### Scenario: Set printer working directory

- GIVEN the user sets a working directory for the printer
- WHEN a print job is executed
- THEN the printer process MUST run in the configured working directory

#### Scenario: Validate printer command exists

- GIVEN the user sets a printer command path
- WHEN the command path does not exist on the filesystem
- THEN the system MUST display a validation warning
- AND the configuration MUST still be saved (user may configure later)

### Requirement: Backup management

The system MUST allow configuring and triggering database backups.

#### Scenario: Set backup directory

- GIVEN no backup directory is configured
- WHEN the user sets backup directory to "/home/user/backups"
- THEN the system MUST persist this path

#### Scenario: Trigger manual backup

- GIVEN a configured backup directory
- WHEN the user triggers a manual backup
- THEN the system MUST create a timestamped copy of the SQLite database
- AND the backup file MUST be placed in the configured directory
- AND the system MUST display "Backup created successfully"

#### Scenario: Backup fails gracefully

- GIVEN the backup directory does not exist or is not writable
- WHEN the user triggers a backup
- THEN the system MUST display an error message
- AND the system MUST NOT corrupt the existing database

### Requirement: Currency settings

The system MUST allow configuring the display currency and locale.

#### Scenario: Set currency to Colombian Peso

- GIVEN the user selects Colombian Peso (COP) as currency
- WHEN monetary values are displayed
- THEN they MUST be formatted as "$1.234.567" (COP format)

#### Scenario: Set currency to US Dollar

- GIVEN the user selects US Dollar (USD) as currency
- WHEN monetary values are displayed
- THEN they MUST be formatted as "$1,234.57" (USD format)

#### Scenario: Currency change does not affect stored values

- GIVEN sales stored with decimal amounts
- WHEN the user changes the display currency
- THEN stored amounts MUST remain unchanged
- AND only the display format MUST change
