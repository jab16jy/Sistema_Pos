# Delta for Cashier Sales Flow

## MODIFIED Requirements

### Requirement: Persisted cashier sale confirmation

The system MUST create a durable sale when a cashier confirms a valid cart with customer/payment data, including the classified payment outcome needed for cash-versus-digital reporting and any receivable balance created by the confirmation.
(Previously: Confirmation persisted sale totals and inventory effects, but did not require classified reporting totals or receivable outcome linkage.)

#### Scenario: Confirm valid sale

- GIVEN a cashier has a valid cart, payment, and customer context
- WHEN the cashier confirms the sale
- THEN a sale record MUST exist with line items, payment classifications, totals, and invoice number
- AND inventory effects MUST be represented according to the sale outcome

#### Scenario: Reject invalid confirmation

- GIVEN the cart is empty or payment data is incomplete
- WHEN the cashier confirms the sale
- THEN no sale MUST be persisted
- AND the cashier MUST receive a recoverable validation result

#### Scenario: Confirm sale with deferred balance

- GIVEN a cashier confirms a sale for an identified customer with an unpaid remainder
- WHEN the sale is persisted
- THEN the persisted outcome MUST separate cash collected, digital collected, and receivable balance created
- AND reporting totals MUST treat only collected cash as expected drawer cash

### Requirement: Persisted product lookup for cashier

The cashier flow SHOULD use persisted product lookup when the WPF infrastructure container is available, while tests MAY continue injecting in-memory lookup doubles.

#### Scenario: Cashier searches active product

- GIVEN an active product exists in SQLite
- WHEN the cashier searches by name or barcode
- THEN the cashier search results MUST include the persisted product
- AND the result MUST include price and available stock

#### Scenario: Cashier search excludes inactive product

- GIVEN an inactive product exists in SQLite
- WHEN the cashier searches by name or barcode
- THEN the inactive product MUST NOT appear in cashier search results

### Requirement: Payment platform validation

The Application layer MUST define and enforce a canonical list of valid payment platforms: "Nequi", "Bancolombia", "Daviplata", "Efectivo". This list MUST be defined as a constant list in POS.Application, not hardcoded in the ViewModel or in the Database. Any payment submission with a platform value not in this list MUST be rejected with a validation error before the sale is persisted.

#### Scenario: Nequi payment accepted

- GIVEN a sale in progress with total = $45,000 COP
- AND the operator selects platform = "Nequi"
- WHEN the sale is confirmed
- THEN the Payment is persisted with Platform="Nequi"
- AND the sale is saved successfully

#### Scenario: Unknown platform rejected

- GIVEN a sale in progress
- AND the operator submits platform = "Efectivo2" (not in canonical list)
- WHEN the Application layer validates the payment
- THEN an error is returned: "Plataforma de pago no válida: 'Efectivo2'. Válidas: Nequi, Bancolombia, Daviplata, Efectivo"
- AND no sale is persisted

#### Scenario: Empty platform rejected

- GIVEN a sale in progress
- AND the operator submits platform = "" (empty string)
- WHEN the Application layer validates the payment
- THEN an error is returned: "La plataforma de pago es requerida"
- AND no sale is persisted

### Requirement: Optional customer association at checkout

The cashier flow MUST allow associating a Client with a sale at any point before final payment confirmation. Association is optional. A sale without a Client proceeds normally with no loyalty side effects.

#### Scenario: Sale without customer association saves without loyalty side effects

- GIVEN a sale in progress with no Client associated
- WHEN the operator confirms the sale with platform = "Efectivo", Amount=$20,000 COP
- THEN the sale is saved successfully
- AND no LoyaltyTransaction is created
- AND no loyalty error is surfaced

### Requirement: Points accumulate atomically with sale confirmation

When a sale with an associated Client is confirmed, point accumulation MUST be triggered per the loyalty-points spec within the same database transaction as the sale save. No separate "earn points" step is required from the cashier.

#### Scenario: Points accumulate after sale with customer association

- GIVEN a sale with Client associated (LoyaltyAccountId=AccountA, balance=0)
- AND sale total = $35,000 COP
- AND platform = "Daviplata"
- WHEN the sale is confirmed
- THEN the sale is persisted with ClientId associated
- AND a LoyaltyTransaction EARN with Points=35 is persisted for AccountA within the same transaction
- AND AccountA balance = 35 points

### Requirement: Optional loyalty redemption at checkout

During checkout, if a Client is associated and has sufficient balance, the cashier MAY optionally enter a redemption amount (in points). The sale total is reduced by `RedeemedPoints × COPPerPoint` COP before payment is tendered. The reduced total is the basis for payment validation.

#### Scenario: Loyalty redemption reduces sale total at checkout

- GIVEN Client with balance = 200 points is associated to the current sale
- AND sale total (before redemption) = $50,000 COP
- AND operator redeems 100 points (= $100 COP discount)
- WHEN the operator confirms the sale with platform = "Efectivo"
- THEN effective total = $49,900 COP
- AND REDEEM transaction (-100 pts) and EARN transaction (+FLOOR(49900*0.001) = +49 pts) are both persisted
- AND Client balance = 200 - 100 + 49 = 149 points
- AND Payment amount = $49,900 COP
