# Step 19b: idempotency and retries

## Design as built

The branch starts at `origin/main`, commit `dd3fd90`, which includes PR #35.
The branch is `feat/19b-idempotency-and-retries`.

The host owns one table, `api.idempotency_keys`.
Its primary key is the identity provider's user id and the request key.
It holds the method, route, request hash, lease, lock expiry, saved result and creation time.
It has no tenant column or foreign key to a module.
It stores no request body.

1. **Claim phase.** The executor inserts the key with one SQL statement.
   PostgreSQL commits that statement before the command runs.
   An expired, unfinished claim can be taken again with `FOR UPDATE SKIP LOCKED`.
2. **Business phase.** Wolverine opens the module transaction.
   Middleware locks the key row in that transaction.
   The handler changes its business data.
   A postprocessor writes `Result<T>` through the same module DbContext and clears the lock.
   Wolverine commits the business data, result, saga and outgoing envelopes together.
3. **Failure release.** A validation error or exception leaves no saved result.
   After the business transaction ends, the executor deletes its unfinished claim.
   The lease condition prevents an old caller from deleting a newer claim.

The layer has no `BeginTransaction`, `Commit` or transaction wrapper.
A command with no business DbContext uses `ApiDbContext` and Wolverine's own transaction frames.
The host's generic policy selects the handler's actual DbContext from Wolverine's service dependencies.
The key, subject and lease travel in envelope headers.
`CreateTenant` no longer carries its old key field.

Endpoints opt in with `IdempotencyKeyRequired` metadata and call `IIdempotencyExecutor`.
Only `POST /v1/admin/tenants` opts in in production today.
Authorization still runs before the key check and replay.
The OpenAPI document declares the header as required.
The endpoint maps a replayed handler result through the same pure function as the first result.
The saved result includes the first trace id, so a business rejection replays the same Problem Details body.

The worker's hosted service sends a cleanup message each hour through Wolverine's durable queue.
Its handler uses the host DbContext and Wolverine transaction middleware.
It removes expired rows and keeps an active lease.
`Host:Role=all` includes the worker service; `web` does not.
`Api:IdempotencyKeyRetention` defaults to 24 hours and validates a minimum of 24 hours at startup.
Tests cover both 24 and 48 hours with a fake clock and real PostgreSQL.

Sources: [Stripe, Idempotent requests](https://docs.stripe.com/api/idempotent_requests), [Brandur Leach, atomic phases](https://brandur.org/idempotency-keys), [IETF draft 07](https://greenbytes.de/tech/specs/draft-ietf-httpapi-idempotency-key-header-07.html), [Wolverine middleware](https://wolverinefx.net/guide/handlers/middleware.html).

## Spike and production atomicity

`IdempotencySpikeTests.SaveResult_BusinessWriteFails_RollsBackBothAndCommitsBothOnSuccess` passes.
The prototype uses the application role on PostgreSQL 18 and a separate test database.
It checks three outcomes:

- A handler throws after the tenant insert. Neither row exists.
- Middleware throws after the result insert. Neither row exists.
- Both inserts succeed. Both rows exist.

The prototype's small table lives in the test-only `spike` schema.
It does not alter the production model.

`IdempotencyTests.CreateTenant_CommitFailsAfterTheResultWrite_RollsBackTheTenantAndResult` checks the production policy.
Test middleware reads the saved result through the real ControlPlane DbContext before commit.
It then throws.
The HTTP response is 500, and neither the tenant nor the key row remains.
Its red run without the executor returned 200 instead of 500.
This also detects a result saved through a separate transaction.

## HTTP behaviour and red evidence

The first six HTTP tests ran red against the old implementation.
The complete boundary tests then ran red before the final executor and header middleware were written.
An earlier executor draft was removed before that final red run.
The tests use real PostgreSQL; the email and identity provider use the existing hand-written fakes.

Commands from `apps/api`:

```sh
dotnet tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll \
  -class '*IdempotencyTests' -class '*IdempotencyBoundaryTests' -class '*CreateTenantTests'
dotnet tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll \
  -method '*CreateTenant_WithoutAnIdempotencyKey*' -method '*CreateTenant_WithAnInvalidIdempotencyKey*'
```

The boundary red run had 46 cases, with 15 failures.
The header red run had six cases, with six failures.
The failures below are the assertions from those runs.
Test names omit their shared `Api.IntegrationTests` namespace.

| Required behaviour | Test | Red output |
| --- | --- | --- |
| Missing key answers 400 | `Admin.CreateTenantTests.CreateTenant_WithoutAnIdempotencyKey_IsABadRequest` | `created.StatusCode should be BadRequest but was OK` |
| Invalid key answers 400 | `Admin.CreateTenantTests.CreateTenant_WithAnInvalidIdempotencyKey_IsABadRequest` (five cases) | `created.StatusCode should be BadRequest but was OK` |
| First request runs and saves its result | `Idempotency.IdempotencyTests.CreateTenant_FirstRequest_SavesTheHandlerResultWithoutTheRequestBody` | Saved row count `should be 1L but was 0L` |
| Finished request replays its status and body | `Admin.CreateTenantTests.CreateTenant_TheSameKeyAndBodyAgain_ReturnsTheFirstTenantAndCreatesNothingNew` | `again.StatusCode should be OK but was Conflict` |
| Replay preserves Location and carries the replay header | `Idempotency.IdempotencyBoundaryTests.RunCommand_FinishedRequest_ReplaysTheBodyStatusAndLocation` | Location `should be /v1/idempotency/01a13656-a643-7514-b80d-c5f46a22c403 but was /v1/idempotency/01a13656-a647-74c8-bf8c-e533aecb2d64` |
| Running request answers 409 | `Idempotency.IdempotencyBoundaryTests.RunCommand_FirstRequestIsRunning_AnswersConflictWithoutWaiting` | `concurrent.StatusCode should be Conflict but was Created` |
| Different input answers 422 | `Admin.CreateTenantTests.CreateTenant_TheSameKeyWithAnotherBody_IsUnprocessable` | `reused.StatusCode should be UnprocessableEntity but was OK` |
| Different endpoint, route value or method answers 422 | `Idempotency.IdempotencyBoundaryTests.RunCommand_KeyReusedWithAnotherRouteOrMethod_IsUnprocessable` (three cases) | `reused.StatusCode should be UnprocessableEntity but was Created` |
| Validator failure saves nothing; a corrected retry runs | `Idempotency.IdempotencyTests.CreateTenant_ValidationFails_DoesNotSaveAndCorrectedRetryRuns` (two cases) | Corrected retry's row count `should be 1L but was 0L` |
| Domain validation failure saves nothing; a corrected retry runs | `Idempotency.IdempotencyTests.CreateTenant_DomainValidationFails_DoesNotSaveAndCorrectedRetryRuns` | Corrected retry's row count `should be 1L but was 0L` |
| Business rejection is saved and replays exactly | `Idempotency.IdempotencyTests.CreateTenant_BusinessRejection_ReplaysTheExactStatusAndBody` | `The given header was not found` |
| Exception or 5xx saves nothing, rolls back and allows a retry | `Idempotency.IdempotencyBoundaryTests.RunCommand_ExceptionOrServerFailure_ReleasesTheClaimAndRollsBackTheBusinessWrite` (500 and 503) | Successful retry's completed row count `should be 1L but was 0L` |

The exception/5xx test also reads the database after the failed response, before the retry.
It checks that both the claim and the business row are absent.
The 503 endpoint maps the handler exception to 503 after the executor releases the claim.
It is a test endpoint only.

`DescribeEndpoint_IdempotencyRequired_DeclaresTheRequiredHeader` initially failed with a missing OpenAPI property.
Existing tests keep the 255-character boundary and separate callers under the same key.

Two recovery tests add coverage:

- `RunCommand_LeaseExpiresWhileTheHandlerRuns_RemainsConflictUntilTheTransactionEnds` advances a fake clock while the transaction holds the key row.
  The red run returned Created instead of Conflict.
  The final implementation keeps the live transaction's lock.
- `RunCommand_AbandonedExpiredClaim_RunsAgainAndSavesTheResult` recreates only the unfinished claim after a rolled-back handler.
  A retry takes it and commits one business row and one result.

## Default retries and InvokeAsync

The global fallback uses Wolverine's native retry continuations.
It retries a queued message three times inline, after 100 ms, 500 ms and 2 s.
Its fourth failure moves the message to the dead letter queue.
It declines envelopes without a queue destination.
Message-specific rules still run first.

`DefaultRetryTests.HandleQueuedMessage_AlwaysFails_RetriesThreeTimesThenDeadLetters` runs with `all` and `worker`.
Its red output was `attempt count should be 4 but was 1` in both roles.
The green test checks four actual handler attempts and the committed PostgreSQL dead-letter row.
Each case has a separate database, so other test workers cannot consume its message.

`HandleQueuedMessage_MessageSpecificRule_RunsBeforeTheGlobalRule` checks that a one-retry message runs twice, rather than four times.
`ExistingRetryPolicyTests` compiles the real handler chains, then checks all three tenant step rules, the invitation email's configured pauses and the original immediate `SagaConcurrencyException` retries.
Existing saga integration tests also run those steps and compensations.

An unrestricted global rule **does apply to HTTP InvokeAsync**.
`InvokeCommand_UnrestrictedGlobalRule_RetriesTheHttpCommand` demonstrates four handler attempts.
`InvokeCommand_DefaultRule_DoesNotRetryTheHttpCommand` demonstrates one attempt under the final rule, on a web host.
The HTTP response is 500 in both tests.

The first full run of the new retry test exposed its own tracker problem.
The tracker waited for unhandled fault traffic after the original message had reached the DLQ at 4067 ms.
Its default tracking deadline then expired.
The test now waits for the committed DLQ row through the repository's existing condition-wait helper.
It does not extend the tracker timeout.

Sources: [Wolverine, Error Handling](https://wolverinefx.net/guide/handlers/error-handling.html), Nygard, *Release It!*, Chapter 5 (bounded retries), Hohpe and Woolf, "Dead Letter Channel".

## Flaky health test

The fixture treated a sent termination signal as a terminated session.
`pg_terminate_backend(pid)` defaults its timeout to zero.
It reports successful signal delivery even when the backend is still alive.
The health check could therefore run before every pooled connection had ended.
The old helper also disabled new connections and sent termination signals in one legacy SQL batch.

The regression test delays a real PostgreSQL backend with SIGSTOP.
It starts the database-close helper and observes the pending SIGTERM in `/proc`.
The old helper completed while the backend was still alive.
Red output:

```text
CloseDatabase_TerminationSignalIsPending_WaitsForTheSessionToEnd [FAIL]
completedWhileTheSessionWasStillAlive should be False but was True
```

The fixture now commits the statement that disables new connections first.
It then calls `pg_terminate_backend(pid, 5000)` for every session.
It checks that a fresh `pg_stat_activity` query has zero sessions before readiness runs.
The five-second bound is on confirmed backend termination, not an increased HTTP or test timeout.
The test resumes the backend in a finally block.
The original health test has no retry and no added sleep.

An early consecutive-run attempt exposed a second faulty assumption in the fixture fix.
An aggregate of the termination Boolean results returned false in its third run.
That Boolean can also be false when the session ends before the signal arrives.
PostgreSQL's implementation explicitly permits that race.
The final helper checks the live-session count instead of treating every false result as a live session.
Temporary notice diagnostics were removed.
The earlier failed sequence is not counted as the final ten consecutive runs.

Sources: [PostgreSQL 18, Server Signaling Functions](https://www.postgresql.org/docs/18/functions-admin.html#FUNCTIONS-ADMIN-SIGNAL), [PostgreSQL 18, backend signaling implementation](https://github.com/postgres/postgres/blob/REL_18_STABLE/src/backend/storage/ipc/signalfuncs.c), [Npgsql, Batching](https://www.npgsql.org/doc/basic-usage.html#batching).

## Test counts

The baseline full solution run passed 669 tests.
The per-project counts come from xUnit discovery with `-preEnumerateTheories` and agree with the full solution total.

| Project | Before | After |
| --- | ---: | ---: |
| Audit.IntegrationTests | 5 | 5 |
| ControlPlane.IntegrationTests | 42 | 42 |
| ControlPlane.UnitTests | 84 | 70 |
| Notifications.UnitTests | 8 | 8 |
| SharedKernel.UnitTests | 7 | 7 |
| Tenancy.IntegrationTests | 23 | 23 |
| Api.ArchitectureTests | 180 | 182 |
| Api.EndToEndTests | 1 | 1 |
| Api.IntegrationTests | 319 | 351 |
| Total | 669 | 689 |

The 14 deleted unit cases belonged to the old tenant-creation request entity.
The HTTP tests now cover its key validation and input matching in the shared layer.
The new host DbContext is included in the schema-name and pending-migration checks.

## Accepted deployment risk

The new ControlPlane migration drops `control_plane.tenant_creation_requests`.
An old pod that handles tenant creation during the deploy can fail.
The maintainer accepts this exception to expand-first migration practice because there is no production database.

## Final verification

`dotnet build Api.slnx` passed with zero warnings and zero errors.
The final targeted outage run passed both the original health test and the SIGSTOP/SIGTERM regression.

The `architecture-reviewer` subagent reviewed `dd3fd90..473dd98` against the repository rules.
It found no actionable architecture violation.
It also reviewed the final session-count fixture change separately and found no actionable violation.
It checked W1, W7, W11, I1 to I7, R4, R6 and N4.

The old-name search found no matches outside migrations and this report:

```sh
rg -n 'tenant_creation_requests|TenantCreationRequest|FindCreationRequestAsync' . \
  --glob '!**/Migrations/**' \
  --glob '!apps/api/docs/reports/19b-idempotency-and-retries.md' \
  --glob '!**/bin/**' --glob '!**/obj/**'
```

`git diff --check` passed.
The final ten consecutive full runs all passed at commit `0e99247`.
Each full run includes `CheckHealth_DatabaseGoesAway_IsLiveButNotReady` and the termination regression.
No test was retried or skipped.
Only this report changed after those runs.

The command ran from `apps/api` in zsh:

```sh
for run in {1..10}; do
  dotnet test --solution Api.slnx > "/private/tmp/19b-final-full-${run}.log" 2>&1
  result=$?
  print "RUN ${run} EXIT ${result}"
  rg '^  (total|failed|succeeded|skipped|duration):' "/private/tmp/19b-final-full-${run}.log"
  if (( result != 0 )); then
    tail -n 100 "/private/tmp/19b-final-full-${run}.log"
    exit "$result"
  fi
done
```

Each command exited with code 0:

| Full run | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | --- |
| 1 | 689 | 0 | 0 | 55s 328ms |
| 2 | 689 | 0 | 0 | 55s 182ms |
| 3 | 689 | 0 | 0 | 53s 790ms |
| 4 | 689 | 0 | 0 | 54s 503ms |
| 5 | 689 | 0 | 0 | 51s 922ms |
| 6 | 689 | 0 | 0 | 53s 611ms |
| 7 | 689 | 0 | 0 | 53s 982ms |
| 8 | 689 | 0 | 0 | 52s 756ms |
| 9 | 689 | 0 | 0 | 52s 420ms |
| 10 | 689 | 0 | 0 | 51s 267ms |

Skipped: none in the implementation.
Not checked: staging, production, Railway and GitHub settings, or the web application's build. The request excludes those changes and covers the API.
Risks: the accepted old-pod failure during the contract migration.
