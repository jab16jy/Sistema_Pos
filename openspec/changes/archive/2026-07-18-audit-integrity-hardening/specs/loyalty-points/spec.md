# Delta for loyalty-points

## ADDED Requirements

### Requirement: Cumulative partial-return compensation

For a sale with one or more accepted returns, the system MUST calculate the cumulative loyalty correction from all historical and current returned quantities using the original earning and redemption rules. It MUST append only the unpaid compensation delta after accounting for prior compensation transactions linked to the sale or return source. The resulting correction MUST NOT exceed the source transaction bounds.

#### Scenario: Compensate two partial returns cumulatively

- GIVEN a completed sale earned 10 points for 10 units
- AND the first accepted return of 3 units already produced a 3-point compensation
- WHEN a second accepted return of 4 units is processed
- THEN the system MUST record the additional 4-point compensation
- AND the cumulative compensation MUST equal 7 points

#### Scenario: Preserve rounding and redemption bounds

- GIVEN a sale has earned and redeemed loyalty effects whose return correction requires rounding
- WHEN an accepted partial return is compensated
- THEN the system MUST use the same source rules used for the completed sale
- AND the cumulative correction MUST NOT exceed the earned or redeemed source bounds

### Requirement: Idempotent compensation evidence

The system MUST make repeated processing of the same accepted return idempotent. It MUST use persisted compensation evidence to avoid duplicate point movements while allowing a later valid partial return of the same sale to receive its unpaid delta.

#### Scenario: Repeat a processed return

- GIVEN an accepted return already has its full compensation delta persisted
- WHEN the same return command is submitted again
- THEN no additional loyalty transaction MUST be created
- AND the account balance MUST remain unchanged

### Requirement: Historical loyalty compatibility

Existing loyalty transactions and account balances MUST be preserved. The system MUST NOT retroactively create corrections for historical returns solely because they predate this behavior; a newly processed valid return MUST, however, account for any persisted prior compensation when calculating its delta.

#### Scenario: Process a new return after historical compensation

- GIVEN a legacy partial return has a persisted compensation transaction
- WHEN a new valid partial return for the same sale is processed
- THEN the historical transaction MUST remain unchanged
- AND the new compensation MUST include it when calculating the unpaid delta
