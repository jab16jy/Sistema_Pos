# Delta for Reporting Cash Closing

## ADDED Requirements

### Requirement: Export service is non-throwing

The reporting export service MUST provide bounded report bytes for the currently supported daily sales scope or return a recoverable unsupported/configuration result through callers.

#### Scenario: Daily report export succeeds

- GIVEN persisted completed sales exist for a date
- WHEN the report service generates the daily sales report
- THEN it MUST return non-empty bytes
- AND those bytes SHOULD preserve cash versus digital separation

#### Scenario: Report scope unsupported

- GIVEN a requested report format or scope is not yet supported
- WHEN the report service is invoked
- THEN the application MUST avoid `NotImplementedException`
- AND the caller MUST receive a recoverable failure path
