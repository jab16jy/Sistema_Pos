# Delta for Loyalty Tier Administration

## ADDED Requirements

### Requirement: Persist a closed tier badge foreground vocabulary

The system MUST persist one tier badge foreground for every loyalty tier. The allowed values MUST be exactly `Black` and `White`; arbitrary color strings, UI brush values, and framework-specific types MUST be rejected. Existing tiers and newly created tiers without an explicit selection MUST receive `White` as the non-null persisted default.

#### Scenario: Migrate existing tiers to white foreground

- GIVEN a database contains loyalty tiers created before the foreground feature
- WHEN the foreground migration is applied
- THEN each existing tier MUST load with persisted foreground `White`
- AND the foreground column MUST be non-null

#### Scenario: Reject an unsupported foreground

- GIVEN an administrator submits a tier with foreground `Blue`
- WHEN the tier is validated
- THEN the system MUST reject the tier update
- AND the system MUST NOT persist an unsupported foreground value

### Requirement: Edit and preview the tier foreground

The tier administration experience MUST expose only `Black` and `White` as foreground selections for new and existing tiers. It MUST show a preview that applies both the tier background and the selected foreground. The system MUST load, create, and update the persisted foreground through tier requests and results.

#### Scenario: Save a black foreground selection

- GIVEN an administrator edits a tier with a configured background color
- AND the administrator selects `Black` foreground
- WHEN the administrator saves a valid tier
- THEN the system MUST persist `Black`
- AND a subsequent tier load MUST return `Black`

### Requirement: Validate tier multiplier text strictly before save

The tier editor MUST treat its displayed multiplier text as valid only when it is a complete, positive decimal value using either a dot or comma decimal separator. It MUST reject blank text, malformed text, zero, negative values, and values containing unsupported extra separators or characters. A failed parse MUST invalidate the pending save and MUST NOT submit or retain a previously parsed numeric multiplier as the value for the displayed invalid text. Domain validation MUST remain the final invariant.

#### Scenario: Reject stale multiplier submission

- GIVEN a tier editor previously parsed multiplier text `1.5`
- AND the administrator changes the displayed multiplier text to `abc`
- WHEN the administrator attempts to save
- THEN the system MUST show a recoverable validation error
- AND the system MUST NOT submit `1.5` or update the tier

#### Scenario: Accept supported decimal formats

- GIVEN an administrator enters multiplier text `1.5` or `1,5`
- WHEN the editor validates and saves the tier
- THEN the system MUST accept the positive decimal value
- AND the persisted multiplier MUST equal the parsed entered value
