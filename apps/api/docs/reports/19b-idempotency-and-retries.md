# Step 19b report

## Baseline

The branch starts from `origin/main`, commit `dd3fd90`, which includes PR #35.
`dotnet build Api.slnx` passed with zero warnings and zero errors.
`dotnet test --solution Api.slnx` passed: 669 tests, zero failures, zero skips.

## Atomicity spike

`IdempotencySpikeTests.SaveResult_BusinessWriteFails_RollsBackBothAndCommitsBothOnSuccess` passes.
The test uses PostgreSQL 18 and the application role.
It runs a command with the key in the envelope headers.
The command returns `Result<SpikeTenantResult>`.
Wolverine starts the ControlPlane transaction.
An `AfterAsync` middleware writes the result through the same ControlPlane DbContext.
Wolverine commits the transaction.

The test checks three outcomes:

- The handler throws after the tenant insert. Neither row exists.
- The middleware throws after the result insert. Neither row exists.
- Both inserts succeed. Both rows exist.

No hand-written transaction code is used.
The spike creates its small table in a separate test database.
It does not change the production model.

Source: [Wolverine middleware](https://wolverinefx.net/guide/handlers/middleware.html), "After is before the commit".
Source: [Brandur, Implementing Stripe-like Idempotency Keys in Postgres](https://brandur.org/idempotency-keys), atomic phases.

## Accepted deployment risk

The ControlPlane contract migration drops `control_plane.tenant_creation_requests`.
An old pod that handles tenant creation during the deploy can fail.
The maintainer accepts this exception to expand-first migration practice because there is no production database.

## Execution checks

- [x] Verify PR #35 and run the baseline suite.
- [x] Prove the shared transaction with the spike.
- [ ] Write red HTTP tests, then add the schema and layer.
- [ ] Move CreateTenant to the layer and remove the old implementation.
- [ ] Test the global retry rule and InvokeAsync behaviour.
- [ ] Diagnose the health test and run ten full suites.
- [ ] Update the architecture and configuration documents.
- [ ] Run the architecture reviewer, push and open the pull request.

Skipped: none at the spike stage.
Not checked: the remaining execution checks.
Risks: the accepted deployment risk above.
