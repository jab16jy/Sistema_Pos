# 7.2 Adversarial Integrity Review

Date: 2026-07-17
Method: independent source trace of all eight critical commands from presentation
contract through CriticalCommandAuthorizer, transaction, and persistence, plus
targeted SQLite integration test inspection. No product code edited.

## Eight critical commands traced

1. Sale confirmation - CreateSaleUseCase.ExecuteAsync
2. Receivable payment - ApplyReceivablePaymentUseCase.ExecuteAsync / ExecuteAllAsync
3. Purchase registration - RegisterPurchaseUseCase.ExecuteAsync
4. Stock adjustment - AdjustStockUseCase.ExecuteAsync
5. Shift start - StartShiftUseCase.ExecuteAsync
6. Shift close - CloseShiftUseCase.ExecuteAsync
7. Cash closing - CloseCashRegisterUseCase.ExecuteAsync
8. Return authorization - ProcessReturnUseCase.ExecuteAsync

Confirmed by direct source read: every one of the eight calls
CriticalCommandAuthorizer.AuthorizeAsync(permissionKey) as its first executed
statement, strictly before any repository read or mutation
(src/POS.Application/Services/CriticalCommandAuthorizer.cs:21-39). The authorizer
itself rejects (throws UnauthorizedActionException) before touching any repository
beyond IUserRepository/IRoleRepository reads: unauthenticated/empty session,
missing/inactive user, missing/inactive role, or missing permission (non-admin) all
short-circuit before returning a CanonicalActor.

## Scenario 1: duplicate operation retries (idempotency)

- ProcessReturnUseCase.ExecuteInTransactionAsync looks up
  _saleReturns.GetByOperationIdAsync(request.OperationId) first and returns the
  already-persisted return's ID without re-running derivation/staging.
  Evidence: tests/POS.Application.Tests/Invoices/ProcessReturnUseCaseTests.cs:58-71
  (ExecuteAsync_RepeatedOperationIdReturnsPersistedReturnWithoutDuplicating) --
  asserts single event, single inventory movement, SaveCount == 1 after two
  identical calls.
- DB-level backstop: SaleReturnConfiguration places a filtered unique index on
  OperationId (IS NOT NULL), so even a race between two concurrent requests
  with the same operation ID that both miss the in-transaction lookup would collide
  at the database boundary and one write would fail -- inside the same
  ExecuteInTransactionAsync scope, so it rolls back cleanly (see Scenario 6/rollback
  section below).
- Loyalty compensation idempotency: the composite unique index on
  (CompensatesTransactionId, SaleReturnId) in LoyaltyTransactionConfiguration.cs:22-24
  makes a given source/return compensation pair a one-time write; a retried
  return (via the operation-ID early return) never re-enters
  CompensateReturnAsync, so no double-count risk. Evidence:
  ExecuteAsync_TwoPartialReturnsCompensateTheSameAsAFullReturn
  (ProcessReturnUseCaseTests.cs:74-94) explicitly repeats the second operation ID
  and asserts exactly 4 compensation rows total (not 6).
- No gap found.

## Scenario 2: cross-resolution writes (settlement + reconciliation both staged)

- ProcessReturnUseCase.ResolveAsync returns exactly one of (paymentMethod, null)
  or (null, receivable) via a switch expression over ReturnResolutionChoice
  (ProcessReturnUseCase.cs:216-224); the two are structurally exclusive by
  construction (a PaymentMethod?/Receivable? tuple, never both populated).
  StageResolutionAsync (ProcessReturnUseCase.cs:246-272) branches on
  paymentMethod is not null and returns immediately after staging the settlement;
  it only reaches the reconciliation branch otherwise.
- DB-level backstop: ReturnSettlementConfiguration and
  ReturnReceivableReconciliationConfiguration both declare a unique index on
  SaleReturnId and a restrictive one-to-one FK to SaleReturn
  (ReturnSettlementConfiguration.cs:24-28,
  ReturnReceivableReconciliationConfiguration.cs:24-28). Even a hypothetical
  future code path that tried to stage both would fail at the database boundary
  inside the same transaction (no possible silent double-write).
- Test evidence: ExecuteAsync_StagesExactlyOneCashOrDigitalSettlement and
  ExecuteAsync_ReconcilesMatchingReceivableWithoutCreatingSettlement
  (ProcessReturnUseCaseTests.cs:99-125) each assert the other table is empty
  after a successful return.
- No gap found.

## Scenario 3: negative/excess receivables

- Receivable.ReduceForReturn (src/POS.Domain/Entities/Receivable.cs:91-110)
  throws InvalidOperationException if the receivable is cancelled, the amount is
  <= 0, or the amount exceeds OutstandingBalance. ResolveAsync additionally
  pre-validates customer/sale linkage and sufficient balance before ever reaching
  StageResolutionAsync (ProcessReturnUseCase.cs:234-241).
  Receivable.ApplySettlement (separate payment path, line 62-77) has the same
  overpayment guard for the unrelated ApplyReceivablePaymentUseCase flow.
- Test evidence: ExecuteAsync_RejectsMismatchedOrInsufficientReceivableWithoutMutating
  (ProcessReturnUseCaseTests.cs:127-146) covers both a mismatched customer/sale and
  an insufficient balance, asserting zero mutation in both cases via AssertRejected
  (checks SaveCount == 0, no return events, unchanged stock).
- No gap found.

## Scenario 4: stale product saves (optimistic concurrency)

- Product.Version is the EF concurrency token; AppDbContext.AdvanceProductVersions
  bumps Version centrally right before every save
  (src/POS.Infrastructure/Persistence/AppDbContext.cs:157-167), and
  SaveChangesWithConcurrencyHandlingAsync translates DbUpdateConcurrencyException
  into ConcurrencyConflictException, clearing the change tracker
  (AppDbContext.cs:141-155).
- Verified no automatic retry exists anywhere: the only two catch sites for
  ConcurrencyConflictException are in CustomersViewModel.cs:603 and :633
  (receivable payment flow), and both just reload state and show a message asking
  the human to retry manually -- they do not resubmit the financial command.
  Grepped the whole src/ tree; no other catch site exists, so every other command
  simply propagates the exception up to its caller (also no auto-retry).
- Test evidence: ProductConcurrencyTests.cs --
  TwoContexts_UpdatingSameProduct_ThrowsConcurrencyConflictException_AndFirstUpdateSurvives
  and StaleStockWrite_ClearsChangeTracker_AndWritesNoOutcome prove the first writer
  wins, the stale writer's movement is never persisted, and the change tracker is
  cleared (no partial state leaks into a subsequent save on the same context).
  BusyOrLockedDatabase_ThrowsTransactionConflictException_NotConcurrencyConflictException
  proves SQLite busy/locked errors take the separate TransactionConflictException
  path and are not conflated with a real concurrency conflict.
- No gap found.

## Scenario 5: migration with orphans/duplicate usernames

- DatabaseSchemaRollout.EnsureLatestSchemaAsync calls
  IntegrityPreflight.VerifyAsync(dbContext, ct) as its first statement, strictly
  before dbContext.Database.MigrateAsync(ct)
  (src/POS.Infrastructure/DatabaseSchemaRollout.cs:17-22) -- matches design.md's
  explicit requirement that the gate runs before migration, not after.
  IntegrityPreflight.CheckAsync checks duplicate usernames with
  StringComparer.Ordinal (BINARY/case-sensitive semantics, per spec) and orphan
  checks for Sales/Returns/Shifts -> Users, Receivables -> Customers/Sales,
  InventoryMovements -> Products, SaleReturnItems -> SaleItems/Products, Quotes ->
  Customers/Sales. VerifyAsync throws before any schema mutation if any error is
  found (IntegrityPreflight.cs:204-211).
- Test evidence: tests/POS.Application.Tests/Persistence/IntegrityPreflightTests.cs,
  IdentityReceivableMigrationTests.cs, and
  InventoryReturnQuoteMigrationTests.cs (all present, part of the 400-test
  Application suite verified green in this session) exercise duplicate usernames,
  each orphan category, and valid legacy data migrating unchanged.
- No gap found. Full re-verification of these specific tests happens as part of 7.3,
  since they are part of the full dotnet test POS.sln run.

## Scenario 6: concurrent quote allocation

- QuoteRepository.GetNextQuoteNumberAsync uses a single atomic
  INSERT ... ON CONFLICT DO UPDATE ... RETURNING statement inside the caller's
  transaction (QuoteRepository.cs:38-96), seeded from
  MAX(CAST(SUBSTR(QuoteNumber, ...) AS INTEGER)) over existing QUOTES rows for
  first allocation in a year, then a plain increment thereafter. Deletion never
  touches QUOTE_SEQUENCES, so numbers are burned not reused.
- Test evidence: QuoteSequenceTests.cs proves concurrent same-year allocations are
  distinct, first allocation seeds from historical max, deletion does not free a
  number, and a new year is independent of another year's counter.
- No gap found.

## Rollback preserves audit evidence policy

- AppDbContext.AddAuditEntries() is called synchronously inside every
  SaveChanges(Async) override, immediately before delegating to
  base.SaveChangesAsync, and adds AuditLog rows to the same ChangeTracker
  that holds the business entity changes (AppDbContext.cs:101-121, 214-230). Both
  sets of rows are part of the same EF SaveChanges call inside the same SQLite
  transaction (ExecuteInTransactionAsync, AppDbContext.cs:169-201). A failure at
  any point rolls back the whole transaction via transaction.RollbackAsync plus
  ChangeTracker.Clear() (AppDbContext.cs:185-188), so a rolled-back operation
  never leaves a dangling AuditLog row claiming something happened -- the audit
  write and the business write share one atomic commit.
- Direct proof: ReturnReceivableReconciliationTests.ExecuteAsync_WhenCommitFails_RollsBackReturnStockAndResolution
  (tests/POS.Application.Tests/Receivables/ReturnReceivableReconciliationTests.cs)
  forces SaveChangesAsync to throw inside the transaction and then, on a fresh
  DbContext against the same connection, asserts SaleReturns,
  InventoryMovements, ReturnSettlements, and ReturnReceivableReconciliations
  are all empty, and product stock/receivable balance are unchanged. Although this
  test does not directly assert on AuditLogs, it exercises the identical
  ExecuteInTransactionAsync rollback path that also empties the ChangeTracker
  that would have contained the audit row for the failed return -- the mechanism is
  the same transaction boundary, so the conclusion (no dangling audit evidence)
  follows directly from the shared commit/rollback path, not a separate one.
- No gap found.

## No automatic financial retry anywhere

Grepped every catch site for ConcurrencyConflictException and
TransactionConflictException across src/. Only the two CustomersViewModel
sites catch and reload/re-prompt; no use case, repository, or view model catches
either exception type and resubmits the same command. Confirmed by direct read of
CreateSaleUseCase, ApplyReceivablePaymentUseCase, RegisterPurchaseUseCase,
AdjustStockUseCase, StartShiftUseCase, CloseShiftUseCase,
CloseCashRegisterUseCase, and ProcessReturnUseCase -- none contains a retry loop
or recursive self-call around its SaveChangesAsync/ExecuteInTransactionAsync call.

## Shift ownership (no administrator bypass)

- CloseShiftUseCase.ExecuteAsync authorizes, loads the requested shift, then
  throws UnauthorizedActionException unless shift.CashierUserId == actor.UserId
  (src/POS.Application/UseCases/Shifts/CloseShiftUseCase.cs:29-38). There is no
  role check or administrator override anywhere in this path -- an administrator
  who is not the shift owner is rejected exactly like any other non-owner cashier.
- No gap found.

## Findings summary

| Finding | Severity | Status |
|---|---|---|
| InventoryViewModel.OperatorUserId is a dead/unused property | Cosmetic | Non-blocking, already noted in 7.1 report; deferred |
| (none) | BLOCKING | None found |

## Exit criterion

Zero blocking findings. All six adversarial scenarios plus the rollback/audit-evidence,
shift-ownership, and no-auto-retry checks are backed by either an existing focused
test (pointed to above with exact file/line evidence) or a direct source-level
invariant proof where a DB-level constraint makes the scenario structurally
impossible. No new packet required; no product code touched during this review.
