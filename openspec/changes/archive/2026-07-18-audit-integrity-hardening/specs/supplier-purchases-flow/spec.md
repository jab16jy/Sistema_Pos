# Delta for supplier-purchases-flow

## MODIFIED Requirements

### Requirement: Persist purchases atomically with stock traceability

The system MUST require the canonical active authenticated session actor to hold the existing purchase-registration permission before it persists a purchase. It MUST use that session actor for purchase and audit attribution, and it MUST preserve the existing atomic purchase, stock, and inventory-movement outcome.
(Previously: presentation controls could be the only permission boundary and request actor data could diverge from the session audit actor.)

#### Scenario: Reject unauthorized purchase before stock mutation

- GIVEN an authenticated active user lacks purchase-registration permission
- WHEN the user confirms valid purchase items
- THEN the system MUST reject the operation before persisting a purchase or inventory movement
- AND product stock MUST remain unchanged

#### Scenario: Attribute an authorized purchase to the session actor

- GIVEN an authenticated active user has purchase-registration permission
- WHEN the user confirms a valid purchase
- THEN the purchase and resulting audit record MUST be attributed to that session actor
- AND the stock increase and inventory movements MUST remain atomic with the purchase
