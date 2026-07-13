# Delta for Customer Tier Badges

## ADDED Requirements

### Requirement: Render customer tier badges with their persisted foreground

The Customers experience MUST render every foreground-bearing element of a customer tier badge using that tier's persisted `Black` or `White` foreground. This includes the badge icon and all badge text. The system MUST use `White` when the tier foreground is the migration default.

#### Scenario: Render a black customer tier badge foreground

- GIVEN a customer is assigned to a tier with background color `#F4D03F` and foreground `Black`
- WHEN the operator views the customer in Customers
- THEN the tier badge icon MUST use black foreground
- AND every tier badge text element MUST use black foreground

### Requirement: Apply tier foreground only to a styled Cashier tier badge

When Cashier renders existing customer tier information as a styled tier badge, it MUST use the assigned tier's persisted foreground for every badge foreground element. The system MUST NOT apply the tier foreground to unrelated loyalty discount rows or ordinary Cashier text.

#### Scenario: Keep unrelated Cashier loyalty text unchanged

- GIVEN a customer has a tier with foreground `Black`
- AND Cashier renders a styled tier badge and a separate loyalty discount row
- WHEN the customer is associated to the sale
- THEN the tier badge foreground elements MUST use black
- AND the separate loyalty discount row MUST retain its own non-tier styling
