# Spec: Loyalty Points Module

**Change**: new-pos-modules
**Feature**: loyalty-points
**Version**: 1.0
**Date**: 2026-06-27

---

## Purpose

Define what must be true after the loyalty-points feature is implemented. This spec covers the domain model, accumulation logic, redemption flow, balance view, and navigation entry. It does NOT prescribe implementation approach.

---

## Domain Model Requirements

### R-LP-01: LoyaltyAccount entity exists per customer

A LoyaltyAccount entity must exist in POS.Domain and be associated 1:1 with a Client entity.

**Attributes required:**
- `Id` (Guid)
- `ClientId` (Guid, foreign key to Client)
- `CreatedAt` (DateTimeOffset)

**Computed property:**
- `Balance` (int) — NOT persisted as a column; computed from SUM of LoyaltyTransaction.Points WHERE ClientId matches and transaction is active.

**Constraint:** There must be exactly one LoyaltyAccount per Client. Duplicate accounts for the same Client are rejected.

---

### R-LP-02: LoyaltyTransaction entity records all point movements

A LoyaltyTransaction entity must exist in POS.Domain.

**Attributes required:**
- `Id` (Guid)
- `LoyaltyAccountId` (Guid, FK to LoyaltyAccount)
- `Points` (int) — positive for EARN, negative for REDEEM
- `Type` (enum: EARN | REDEEM)
- `ReferenceId` (Guid, nullable) — SaleId when Type=EARN, SaleId of redemption when Type=REDEEM
- `CreatedAt` (DateTimeOffset)
- `Description` (string, optional)

**Constraint:** Points must not be zero.

---

### R-LP-03: LoyaltyRule defines earn and redeem rates

A LoyaltyRule configuration entity (or value persisted in SystemConfiguration) must exist.

**Required fields:**
- `PointsPerCOPUnit` (decimal) — default: 1 point per 1,000 COP (i.e., 0.001 pts/COP)
- `COPPerPoint` (decimal) — COP monetary value of 1 redeemed point; default: 1 COP/point
- `MinimumRedemptionPoints` (int) — minimum points required to redeem in a single transaction; default: 1

**No expiry rule** and **no loyalty tier rules** are included in this MVP slice.

---

## Point Accumulation Requirements

### R-LP-04: Points are earned atomically on sale close

When a sale is confirmed and saved, if the sale has an associated Client with a LoyaltyAccount, a LoyaltyTransaction of Type=EARN must be created within the same database transaction as the sale.

**Earn formula:**
```
PointsEarned = FLOOR(SaleTotal_COP / (1 / PointsPerCOPUnit))
             = FLOOR(SaleTotal_COP * PointsPerCOPUnit)
```

Default: `FLOOR(SaleTotal_COP / 1000)` — e.g., $50,000 COP sale → 50 points.

**Constraint:** If `PointsEarned` rounds to 0 (sale total < 1,000 COP), no LoyaltyTransaction is created. The sale is still saved normally.

**Constraint:** If the sale has no associated Client, no LoyaltyTransaction is created. The sale is still saved normally.

**Constraint:** If the LoyaltyAccount creation or LoyaltyTransaction creation fails, the entire sale must be rolled back.

---

### R-LP-05: Sale total used for point calculation excludes loyalty redemption discount

When a customer redeems points as a partial payment in the same sale, the loyalty discount amount must be subtracted from SaleTotal before computing points earned.

**Formula:**
```
EffectiveSaleTotal = SaleTotal_COP - LoyaltyDiscountApplied_COP
PointsEarned = FLOOR(EffectiveSaleTotal * PointsPerCOPUnit)
```

---

## Redemption Requirements

### R-LP-06: Loyalty redemption is optional at cashier checkout

During cashier checkout, the operator may optionally apply loyalty points as a partial payment discount. This step is optional; a sale may proceed without redemption.

**Preconditions:**
- A Client with a LoyaltyAccount has been associated to the current sale.
- The client's computed balance (`SUM(LoyaltyTransaction.Points)`) is >= `MinimumRedemptionPoints`.

---

### R-LP-07: Redemption creates a REDEEM transaction and adjusts the sale total

When loyalty redemption is applied:

1. A LoyaltyTransaction of Type=REDEEM with negative Points value is created.
2. The sale line shows a discount equivalent to `RedeemedPoints * COPPerPoint` COP.
3. The adjusted sale total = `OriginalTotal - (RedeemedPoints * COPPerPoint)`.
4. The adjusted total must be >= 0; it cannot go negative.
5. The REDEEM transaction is saved within the same database transaction as the sale.

**Constraint:** A redemption that would make the adjusted sale total negative must be rejected before sale confirmation.

---

### R-LP-08: Redemption cannot exceed current balance

If the operator attempts to redeem more points than the client's current balance, the Application layer must reject the request with a descriptive error. The cashier screen must surface this error without crashing.

---

## Customer Balance View Requirements

### R-LP-09: Balance is visible on the customer detail screen

The customer detail screen (ClientViewModel or LoyaltyAccountViewModel) must display:
- Current points balance (computed from LoyaltyTransaction SUM).
- A list of the most recent 20 LoyaltyTransaction records (date, type, points, description).

---

## Navigation Requirements

### R-LP-10: Loyalty is a new top-level sidebar entry

The main navigation shell must include a new sidebar entry for "Puntos / Fidelización" that routes to the LoyaltyViewModel/View. It must appear after "Clientes" and before "Proveedores" in sidebar order.

---

## Acceptance Scenarios

### Scenario LP-S1: Points earned on a $50,000 COP sale with associated customer

```
GIVEN a sale with total = $50,000 COP
AND the sale has an associated Client with LoyaltyAccountId = {AccountA}
AND LoyaltyRule.PointsPerCOPUnit = 0.001
WHEN the sale is confirmed and saved
THEN a LoyaltyTransaction of Type=EARN with Points=50 is persisted for AccountA
AND the LoyaltyAccount balance for AccountA is increased by 50 points
AND the sale is saved successfully
```

### Scenario LP-S2: No points created when sale total is below 1,000 COP

```
GIVEN a sale with total = $800 COP
AND the sale has an associated Client with LoyaltyAccountId = {AccountA}
AND LoyaltyRule.PointsPerCOPUnit = 0.001
WHEN the sale is confirmed and saved
THEN no LoyaltyTransaction is created (FLOOR(800 * 0.001) = 0)
AND the sale is saved successfully without error
```

### Scenario LP-S3: No points created when sale has no associated client

```
GIVEN a sale with total = $80,000 COP
AND the sale has NO associated Client
WHEN the sale is confirmed and saved
THEN no LoyaltyTransaction is created
AND the sale is saved successfully without error
```

### Scenario LP-S4: Loyalty redemption reduces sale total correctly

```
GIVEN a client with LoyaltyAccount balance = 200 points
AND a sale in progress with total = $60,000 COP
AND LoyaltyRule.COPPerPoint = 1
AND LoyaltyRule.MinimumRedemptionPoints = 1
WHEN the operator redeems 100 points
THEN a LoyaltyTransaction of Type=REDEEM with Points=-100 is staged
AND the effective sale total = $60,000 - $100 = $59,900 COP
AND upon sale confirmation, the REDEEM transaction is persisted within the same DB transaction
AND the client's new balance = 200 - 100 + FLOOR(59900 * 0.001) = 100 + 59 = 159 points
```

### Scenario LP-S5: Redemption rejected when requested points exceed balance

```
GIVEN a client with LoyaltyAccount balance = 50 points
AND a sale in progress with total = $30,000 COP
WHEN the operator attempts to redeem 100 points
THEN the Application layer returns a validation error: "Saldo insuficiente: 50 puntos disponibles, se solicitaron 100"
AND the sale total remains $30,000 COP
AND no LoyaltyTransaction is created
```

### Scenario LP-S6: Redemption rejected when it would make sale total negative

```
GIVEN a client with LoyaltyAccount balance = 1,000,000 points
AND a sale in progress with total = $5,000 COP
AND LoyaltyRule.COPPerPoint = 1
WHEN the operator attempts to redeem 10,000 points (= $10,000 COP discount)
THEN the Application layer returns a validation error: "El descuento excede el total de la venta"
AND the sale total remains $5,000 COP
```

### Scenario LP-S7: LoyaltyAccount balance is computed correctly on balance view

```
GIVEN a client with the following LoyaltyTransaction history:
  - EARN +100 (SaleId=S1)
  - EARN +50  (SaleId=S2)
  - REDEEM -30 (SaleId=S3)
WHEN the operator opens the customer detail / balance view
THEN the displayed balance = 100 + 50 - 30 = 120 points
AND the 3 transactions appear in the recent transactions list in descending date order
```

### Scenario LP-S8: Sale rollback when LoyaltyTransaction creation fails

```
GIVEN a sale with total = $40,000 COP
AND the sale has an associated Client with a LoyaltyAccount
WHEN the sale is submitted and the LoyaltyTransaction insert raises a database error
THEN the entire database transaction is rolled back
AND no Sale, SaleItem, Payment, or LoyaltyTransaction record is persisted
AND the Application layer surfaces an error to the cashier screen
```

### Scenario LP-S9: Loyalty sidebar entry is visible and navigable

```
GIVEN the POS application is running with a logged-in administrator or cashier
WHEN the operator looks at the main sidebar
THEN a "Puntos" entry is visible
AND clicking it navigates to the LoyaltyViewModel/View without error
AND the view displays a customer search or account list
```

---

## Constraints and Non-Goals

- No loyalty tiers in this slice.
- No loyalty point expiry in this slice.
- No retroactive point calculation for historical sales.
- LoyaltyAccount balance is always computed from LoyaltyTransaction SUM — no denormalized balance column.
- Redemption applies as a flat discount to the sale total, not per-item.
