# Lealtad View Specification

## Purpose

Defines the LoyaltyView UI translation: a program-status summary, accumulation/redemption rule editing, tier/benefit display, and a campaigns/history panel. Per the wireframe, day-to-day point earning/redemption happens in Clientes and Caja — this module only configures rules, tiers, and program-wide tracking.

## Requirements

### Requirement: Program status summary shows total points and adoption metrics

The view MUST show a program status card with an "Activo" state chip, total points issued, and 3 sub-metrics: enrolled customers, checkout usage rate, and redemptions this month.

#### Scenario: Status card reflects current totals

- GIVEN 142,850 total points issued, 247 enrolled customers, 38% checkout usage, 12 redemptions this month
- WHEN the card renders
- THEN it MUST display all four values as shown

### Requirement: Accumulation and redemption rules are editable

The rules panel MUST allow editing: accumulation rate (points per currency spent), redemption rate (points per currency discount), point expiration policy, and checkout usage mode (earn+redeem, earn-only, or balance-only display).

#### Scenario: Saving rules confirms success

- GIVEN the operator edits the accumulation rate
- WHEN the operator activates "Guardar reglas"
- THEN a success toast ("Reglas guardadas") MUST appear

### Requirement: Tiers show point thresholds and benefits

The view MUST display at least 3 tiers (e.g., Bronze, Silver, Gold) each with a point-range chip and a one-line benefit description, and MUST offer an "Editar beneficios" action.

#### Scenario: Tier list is ordered by ascending threshold

- GIVEN tiers Bronze (0-499), Silver (500-999), Gold (1000+)
- WHEN the tier list renders
- THEN it MUST display them in ascending point-threshold order

### Requirement: Campaigns and history panel surfaces program-wide signals

The history panel MUST show active campaigns, upcoming point expirations (customer count within N days), and sales-impact metrics, plus an explanatory note that daily point operations live in Clientes and Caja, not here.

#### Scenario: Upcoming expirations are surfaced with a count

- GIVEN 18 customers have points expiring within 15 days
- WHEN the panel renders
- THEN it MUST display a row indicating "18" customers with near-term expiration

#### Scenario: Panel clarifies this module does not process point redemption directly

- GIVEN the operator views the campaigns/history panel
- WHEN the explanatory note renders
- THEN it MUST state that daily point operation occurs in Clientes and Caja, not in this module
