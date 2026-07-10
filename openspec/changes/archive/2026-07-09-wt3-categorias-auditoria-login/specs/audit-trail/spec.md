<Delta for audit-trail>
## ADDED Requirements

### Requirement: Audit log scrolling layout

The audit log viewer interface MUST constrain the height of the log history DataGrid using a responsive grid layout. A vertical scrollbar MUST be rendered automatically inside the DataGrid when the number of log entries exceeds the display height, keeping the pagination controls and filtering panel visible at all times.

#### Scenario: Display scrollbar on log data overflow
- GIVEN the audit log page has loaded 100 log entries
- WHEN the viewport height is smaller than the required space to render all entries
- THEN a vertical scrollbar MUST appear on the DataGrid
- AND the pagination controls MUST remain visible at the bottom of the screen
</Delta for audit-trail>
