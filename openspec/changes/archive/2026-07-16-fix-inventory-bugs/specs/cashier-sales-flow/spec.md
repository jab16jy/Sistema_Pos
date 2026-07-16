# Delta for Cashier Sales Flow

## MODIFIED Requirements

### Requirement: Complete held sale ledger description

When a cashier completes a previously held sale (converting it to a completed sale), the system MUST update the ledger description / inventory movement reason of its corresponding inventory movements to "Venta".
(Previously: Completing a held sale kept the movement description as "Venta en espera", which did not match direct cashier sales.)

#### Scenario: Complete held sale updates description

- GIVEN a held sale with inventory movement reason "Venta en espera"
- WHEN the cashier completes the sale
- THEN the system MUST update the inventory movement reasons to "Venta"
