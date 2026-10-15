# Architecture: multi-tenant SaaS starter template (API)

Last updated: 2026-10-14

This document is the single source of truth for the API in `apps/api/`. If the code does not agree with this document, the code changes. No other file overrides it.

Each rule has an id (for example `R4`, `W2`). Use the id when you refer to a rule in a review, a commit message or a comment.

## Purpose and scope

The purpose is a multi-tenant SaaS starter template. Every future product is built on it. The scope is the API only. Correctness, clear boundaries and tenant isolation are more important than speed.

### Fixed rules

1. Each decision agrees with the books and with industry practice.
2. Each deviation from the books is written down openly. A silent deviation is not permitted.
3. There is no billing.
4. There are no commercially licensed packages: MediatR, AutoMapper, MassTransit v9+, FluentAssertions.
5. The architecture controls the tool. The tool does not control the architecture. If a tool does not fit the structure, the tool goes or the deviation is approved openly.
6. The template is platform independent. Provider detail (example: the database provider) does not go into the architecture. It goes into the setup notes.
7. Infrastructure without a use does not go in. A part is added only if it catches a failure that no other control catches.

Setup notes for providers are in docs/setup.md.

### Scope of the first template

The first template runs one flow from start to end: tenant creation.

| In scope | Reason |
| --- | --- |
| `POST /v1/admin/tenants` | The work itself |
| The first system admin comes from configuration | The first person who creates a tenant cannot get on the staff list in another way |
| Tenant creation process (saga, three steps) | The approved flow |
| One handler that sends the invitation email (Notifications) | Step three of the process |
| The handlers that write the "tenant created", "tenant suspended" and "tenant reactivated" records (Audit) | The uses of the Audit module |
| The endpoint that accepts an invitation | Without it, the first Owner cannot exist |
| `GET /v1/me/tenants` | The user sees their tenants after sign-in |
| A copy of each user's email address and name, and `POST /v1/webhooks/clerk`, which keeps it fresh (A7, A8) | The admin screens of step 19d list users by email address and name, not by the identity provider's id |
| One read endpoint behind the tenant door. Example: `GET /v1/tenants/{tenantId}/members` | It exercises the tenant door, membership, RLS and the permission check |
| `GET /v1/admin/tenants` and `GET /v1/admin/users` | The admin app lists every tenant and every user |
| `POST /v1/admin/tenants/{tenantId}/suspend` and `.../reactivate` | A system admin stops a tenant's members, and lets them in again (section 6) |
| `POST /v1/admin/users/{userId}/suspend` and `.../reactivate` | A system admin stops a user in every tenant, and lets them in again (A9) |

Out of scope: member invitation by an Owner, custom roles and role assignment, member removal, tenant deletion, support access, in-app notifications, adding a second system admin.

## 1. The big picture

The system is one code base and one build output. This output runs as two process types: web and worker. A third role, all, runs both in one process, for local development and tests.

| Decision | Source |
| --- | --- |
| The system is one deployment unit. The code is divided by business area | Richards and Ford, FSA 2nd ed., Chapter 11 |
| Each module is a bounded context | Evans, Chapter 14. Khononov, Chapter 3 |
| One artifact, two process types. Web serves HTTP requests. Worker runs background work. A setting selects the role | Twelve-Factor, Factor VIII. Nygard, Chapter 5 (Bulkheads) |
| Each module that stores data has its own PostgreSQL schema. No foreign keys and no joins across modules | Evans, Chapter 14. Richardson, "Database per Service" (adapted to a monolith) |
| The host contains no business rule. It only connects the modules | Industry practice (composition root) |

In Kubernetes this is two Deployments and one image. The requirement is a separate process, not a separate machine.

## 2. Module boundary and structure

Each module is five projects. Another module sees only the `Contracts` project.

| Project | Content | Depends on |
| --- | --- | --- |
| `X.Contracts` | Interfaces, DTOs, event types. No domain types | `SharedKernel` |
| `X.Api` | HTTP endpoints | Application, Contracts, `SharedKernel` |
| `X.Application` | Use case handlers, sagas | Domain, Contracts, other modules' Contracts, `SharedKernel` |
| `X.Domain` | Business rules | `SharedKernel` |
| `X.Infrastructure` | EF Core, adapters for external services, contract implementations | Application, Domain, Contracts, `SharedKernel`, `Tenancy` |

`SharedKernel` holds the result and error types, paging, the marker of a tenant entity and two kinds of endpoint metadata (`IdempotencyKeyRequired`, `SignatureChecked`); `Tenancy` holds the base of a module DbContext, the tenant declaration at the start of each transaction, row level security in migrations, the names of the database roles and the settings that every PostgreSQL connection of the API gets.

- Two controls enforce the boundary: project references (compilation) and architecture tests. Source: Ford, Parsons, Kua, Chapter 2.
- The `Api` project cannot reach the `DbContext`. Source: Evans, Chapter 4.
- A read across modules is a plain method call. The interface is in `A.Contracts`. Its implementation is an `internal sealed` class inside A. Source: Gamma et al., Chapter 4 (Facade). Evans, Chapter 14 (Open Host Service).
- The shared kernel stays small and carries no business concept. Source: Evans, Chapter 14.
- Test projects are next to their module. Tests of the host and system-wide tests (architecture, end to end) stay in the top tests folder (N9).
- Names, folders, routes and permissions follow section 10.

## 3. Messaging

If a module needs data, it reads through a contract. To tell other modules that something happened, it uses an event. The outbox carries the event.

### Outbox rules

| # | Rule | Source |
| --- | --- | --- |
| O1 | The event record is written in the same transaction as the business data | Richardson, Chapter 3. Kleppmann, Chapter 11 |
| O2 | Delivery happens after the commit and separately | Richardson, Chapter 3 |
| O3 | Each listening module runs in its own transaction | Vernon, Chapter 10 |
| O4 | Delivery is at least once. A message that arrives again has no second effect | Hohpe and Woolf, Chapter 10 (Idempotent Receiver) |
| O5 | There is one outbox. It is in a shared infrastructure schema | Industry practice. See the deviations list |

### Requirements for the messaging tool

The tool is Wolverine. The tested version is 6.45.0. The tool is measured against these nine requirements. Each requirement is tested with real PostgreSQL.

1. The event record is written in the same transaction as the business data.
2. Each listening module runs in its own transaction.
3. With many pods, only one pod takes a message at a time.
4. A message that arrives again has no second effect.
5. If a pod dies, its unfinished message is not lost. Another pod takes it.
6. A message that always fails is moved aside. The queue does not block.
7. The tenant travels with the message.
8. The `Domain` and `Contracts` projects do not see the tool's types. `Api`, `Application` and `Infrastructure` can.
9. The architecture controls the tool. We decide the module structure, the schema layout and the transaction rule.

Requirements 3 and 5 are tested with two workers on one database (`TwoWorkerTests`).

### Handlers and pipeline

Wolverine is the dispatcher. An endpoint sends a command or a query through `IMessageBus`. A handler is a plain class. Shared behaviour is Wolverine middleware. Source: Fowler, PoEAA Chapter 9 (Service Layer). Hohpe and Woolf, Chapter 3 (Pipes and Filters) and Chapter 10 (Message Dispatcher).

| Order | Command chain | Query chain |
| --- | --- | --- |
| 1 | HTTP: authentication, rate limit, the user's state (A9) and tenant membership, authorization (ASP.NET Core) | HTTP: authentication, rate limit, the user's state (A9) and tenant membership, authorization (ASP.NET Core) |
| 2 | Logging and tracing | Logging and tracing |
| 3 | The transaction opens, the tenant is declared | The transaction opens, the tenant is declared |
| 4 | Input validation | Input validation |
| 5 | Handler | Cache (only on marked queries) |
| 6 | Save, commit, then outbox dispatch | Handler |

Authorization runs once, in ASP.NET Core, before the endpoint sends the message. A refused request opens no transaction. A message from a queue has no caller and is not authorized again.

- Wolverine opens and closes the transaction. We set the rule: each handler runs in its own transaction.
- A handler that calls an external service uses no DbContext, so no transaction is open during the call. It returns its result as a message, or nothing. A step that returns nothing is heard by the saga only when it fails, through its fault. Source: Nygard, Chapter 5 (Integration Points).
- A domain event is handled inside the module and in the same transaction. An integration event leaves the module through the outbox. Source: Vernon, Chapter 8.
- HTTP endpoints are plain ASP.NET minimal APIs. The tool's own endpoint model (Wolverine.Http) is not used.

### Wolverine setup rules

These ten rules come from a spike. The spike is in `apps/api/spikes/WolverineRlsSpike`. The T numbers are the tests in the spike.

| # | Rule | Reason | Evidence |
| --- | --- | --- | --- |
| W1 | Wolverine's EF Core middleware opens and closes the transaction. There is no hand-written transaction code. One exception: TenantDirectory opens a short transaction of its own in the HTTP pipeline, before any message is sent, to read the membership. | The saga record, the business data and the outgoing message go into one transaction (O1, S6) | T3, T4 |
| W2 | The module's DbContext reads the tenant from the message context. An EF Core transaction interceptor runs `set_config('app.tenant_id', ..., true)` when the transaction starts | R4, R5 | T1, T2, T5, T6 |
| W3 | An endpoint on the tenant path relies on the tenant middleware, which sets the tenant on the message bus, and uses `InvokeAsync`. Accepting an invitation uses `InvokeForTenantAsync` with the tenant from the code. Suspending and reactivating a tenant use `InvokeForTenantAsync` with the tenant from the admin route. Creating a tenant passes the id the server chose to the idempotency executor. It uses `InvokeAsync` with `DeliveryOptions.TenantId` and the key's envelope headers (I4). Both Wolverine invocation methods put the tenant on the envelope. The tenant goes to the following messages automatically. A message without a tenant carries the value `*DEFAULT*`. The interceptor does not treat this value as a tenant | Requirement 7, R5. Wolverine, message bus invocation | T3, T6 |
| W4 | `MultipleHandlerBehavior.Separated` is on | O3 | T8 |
| W5 | Messages wait in durable queues, and the message store is in the shared `wolverine` schema. In the role `all`, a message goes to a durable local queue (`UseDurableLocalQueues`). In the roles `web` and `worker`, every message goes through the outbox to one PostgreSQL queue in the same schema (Wolverine's PostgreSQL transport). Only a worker listens to that queue, and it hands an event with several handlers on to durable local queues, one for each handler (W4). A web host runs no durability agent (`DurabilityAgentEnabled = false`): outbox and inbox recovery run in a worker | O1, O5, Requirement 5, section 1 | T3, `HostRoleTests` |
| W6 | A saga derives from Wolverine's `Saga` class. Its record is stored with EF Core in the module's own schema. The `Version` property is mapped as a concurrency token. There is a retry policy for `SagaConcurrencyException` | S3, S8 | T7, T9 |
| W7 | A business rule rejection returns before any data changes, at the start of the handler or in a Validate or Before method. A failure after a change is an exception | Wolverine also commits a handler that returns a failed `Result` | T10a, T10b |
| W8 | The transaction middleware is first in the Wolverine chain. Validation runs inside the transaction. Authorization runs before, in ASP.NET Core | This is Wolverine's behaviour. A rejected request opens an empty transaction and writes no data | Generated handler code |
| W9 | Handler, saga, message and DbContext types are `public` | Wolverine compiles handler code in a separate assembly | Compile errors CS0051 and CS0122 |
| W10 | The `WolverineFx.RuntimeCompilation` package is necessary | Handler code is generated at startup. Pre-generated code was not tried | The host did not start without the package |
| W11 | Every queued message retries an exception three times inline, after 100 ms, 500 ms and 2 s. It then goes to the dead letter queue. Message-specific rules run first. The tenant steps, invitation email and `SagaConcurrencyException` keep their rules. The global fallback declines an inline `InvokeAsync` envelope, which has no queue destination | Nygard, *Release It!*, Chapter 5 (bounded retries). Hohpe and Woolf, "Dead Letter Channel". Wolverine, "Error Handling" | `DefaultRetryTests`, `ExistingRetryPolicyTests` |

### HTTP idempotency

| # | Rule | Source |
| --- | --- | --- |
| I1 | Every POST that creates something or starts a process requires an idempotency key. An endpoint that only reads, or sets a state such as suspend or reactivate, does not. GET and DELETE take no key. An endpoint opts in with `IdempotencyKeyRequired` metadata. The OpenAPI document declares its header as required. A webhook endpoint takes no idempotency key. Its sender's retries are safe because the endpoint sets a state, and an older event is ignored by its timestamp | Stripe, "Idempotent requests". RFC 9110, section 9.2.2 |
| I2 | The header is `Idempotency-Key`. It contains 1 to 255 visible ASCII characters. A missing or invalid key answers 400. Authorization runs before the key check | IETF Idempotency-Key draft 07. Stripe, "Idempotent requests" |
| I3 | The host owns `api.idempotency_keys`. A key is unique for the identity provider's user id and the key together. The row holds the method, route, SHA-256 of the body and route values, a lease and its expiry, the saved handler result, and its creation time. It holds no request body or tenant column | Brandur Leach, "Implementing Stripe-like Idempotency Keys in Postgres" |
| I4 | The claim is one short, atomic database statement before the handler runs. The endpoint calls `IIdempotencyExecutor`. The key, subject and lease travel in envelope headers. Middleware holds a row lock in the handler's Wolverine transaction. A postprocessor writes the handler's `Result<T>` and clears the lock through the handler's own DbContext, before Wolverine commits its transaction. The result and business data commit together. A command with no business DbContext uses `ApiDbContext` and Wolverine's own transaction frames. The layer opens or commits no handler transaction | Brandur Leach, atomic phases. W1. Wolverine, "Middleware", "After is before the commit" |
| I5 | A finished request with the same input replays the saved result. The endpoint maps it through the same pure function to HTTP. Status, body and Location stay the same. The reply carries `Idempotent-Replayed: true`. The saved result includes the first trace id so a replayed Problem Details body stays the same. A running request answers 409. Another body, route value, method or endpoint under the key answers 422 | Stripe, "Idempotent requests". IETF draft 07 |
| I6 | Validation failures are not saved. A business rejection is saved before any business change (W7). An exception rolls back the business transaction and releases the claim. A server failure from that exception saves no result. A retry runs again. Each endpoint's mapping is a pure function of its saved result | Stripe, "Idempotent requests". W7 |
| I7 | `Api:IdempotencyKeyRetention` defaults to 24 hours and is at least 24 hours. A worker hosted service sends a Wolverine cleanup message each hour. The handler deletes expired rows and keeps an active lease. The role all includes this worker service. A web host does not run it | Stripe, "Idempotent requests". Twelve-Factor, Factor VIII |

The claim expires after one minute so a crashed caller does not hold a key forever.
An expired, unfinished claim can be taken again only when no live handler holds its row lock.
Its new lease fences the old handler: if that handler can no longer update its claim, its whole transaction rolls back.
`CreateTenant_CommitFailsAfterTheResultWrite_RollsBackTheTenantAndResult` verifies this transaction boundary on the production code.

Sources: [Stripe](https://docs.stripe.com/api/idempotent_requests), [Brandur Leach](https://brandur.org/idempotency-keys), [IETF draft 07](https://greenbytes.de/tech/specs/draft-ietf-httpapi-idempotency-key-header-07.html), [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#section-9.2.2).

### Saga rules

| # | Rule | Source |
| --- | --- | --- |
| S1 | The default is an event. There is no saga | Richardson, Chapter 4 |
| S2 | A saga is built only if three conditions are true together: the process covers more than one module or an external system, it needs compensation or a timeout, and someone asks "where is the process now" | Richardson, Chapter 4. Khononov, Chapter 9 |
| S3 | Saga state is kept in its own record. It is not hidden in a field of another entity | Hohpe and Woolf, Chapter 7 (Process Manager) |
| S4 | The type is orchestration | Richardson, Chapter 4 |
| S5 | There are three kinds of step: compensatable, pivot, retryable. Work that cannot be undone comes after the pivot | Richardson, Chapter 4 |
| S6 | The saga record and the outgoing message are written in the same transaction | O1 |
| S7 | Each step and each compensation can be repeated safely | Hohpe and Woolf, Chapter 10 |
| S8 | Only one message is handled for the same saga at a time. The version number on the record protects this | Fowler, PoEAA Chapter 16 |
| S9 | A transient failure is tried a limited number of times. Then compensation starts. If compensation also fails, the saga goes to the "needs attention" state and raises an alert | Nygard, Chapter 5 |
| S10 | Each wait has a timeout | Nygard, Chapter 5 (Timeouts) |
| S11 | Until the pivot, the tenant shows the "provisioning" state | Richardson, Chapter 4 (semantic lock) |
| S12 | The saga class is unit tested without a host. The saga does not call external services. It only decides and returns messages | Khorikov, Chapter 7 |
| S13 | A saga shares no message with another handler. It hears its steps through replies and faults sent to it, not through events meant for other modules. A test enforces it | Richardson, Chapter 4 (command and reply). Wolverine runs a saga alone at the endpoint its message arrives on |

"Tenant is provisioning" and "tenant is active" are the tenant's own business state and stay in its status. Step tracking and retry counts are not put in the status. They stay in the saga record.

The saga class is in the `Application` project. The `Domain` project does not see Wolverine.

### Cache rules

The rules are part of the architecture. The code is not in the first template.

1. The cache runs only on queries and is off by default.
2. The cache step comes after authorization.
3. The step adds the tenant id to the key. A handler cannot add it and cannot forget it.
4. Each entry has a lifetime and a memory limit.
5. When a command changes data, the related entry is removed.
6. With many pods, a shared cache is used or the lifetime is kept short.

## 4. Tenant source and isolation

The tenant id is in the API path. The id in the path does not give access. Verified identity and membership give access. The database enforces isolation.

### Tenant source

| # | Rule | Source |
| --- | --- | --- |
| T1 | The path is `/v1/tenants/{tenantId}/...`. The value is the id, not the slug. The slug is not used in the path | Azure Architecture Center, "Map requests to tenants". Google AIP-122 |
| T2 | The tenant id is not read from the body, the query string or a header. A handler gets the tenant from the message envelope. A saga's messages may carry the tenant id to find the saga; data access still uses the envelope's tenant | Golding, Chapter 7 |
| T3 | If there is no membership, the response is 404 | OWASP API Top 10, API1 |
| T4 | `GET /v1/me/tenants` runs without a tenant | Industry practice |
| T5 | Provider staff are not tenant members. Their door is the `/v1/admin/...` path | Golding, Chapter 2 |
| T6 | The token carries only the user identity. No tenant, role or permission comes from the identity provider | Fixed requirement: the identity provider is authentication only |
| T7 | Membership is verified on each request in one place. No tenant endpoint can go around this filter | OWASP API Top 10, API1 |

A tenant has two descriptive fields. `name` is free text, cannot be empty, has a maximum of 100 characters and does not have to be unique. `slug` is unique and is given when the tenant is created. `GET /v1/me/tenants` returns both, and the tenant's status. The path, authorization and isolation use only the id.

### Row Level Security

| # | Rule | Source |
| --- | --- | --- |
| R1 | Each table that belongs to a tenant has `tenant_id`. RLS is enabled and forced | Golding, Chapters 8 and 9 |
| R2 | The application account is not the table owner and cannot bypass RLS | Golding, Chapter 9. OWASP (least privilege) |
| R3 | Migrations run with a separate account. That account is not used at run time. The application does not start while a module misses a migration, and to check it the application account may read each module's migration history table | Twelve-Factor, Factor XII |
| R4 | The tenant is declared at the start of each transaction. A connection-level setting is not permitted. A transaction declares a tenant or a user, never both. A transaction declares one tenant only. | PostgreSQL connection pool behaviour |
| R5 | If the tenant is not declared, a query returns no data | Nygard, Chapter 5 (Fail Fast) |
| R6 | Tables without a tenant are on an explicit list: `control_plane.tenants`, `control_plane.users`, `control_plane.roles`, `control_plane.system_admins`, and `api.idempotency_keys`. A test checks every table. The wolverine schema and the migration history tables are exempt by pattern. The wolverine tables hold message bodies, invitation links included, and are not under RLS (see Deviations). | Ford, Parsons, Kua, Chapter 2 |
| R7 | There are only two database accounts: migration and application. Running code has no path that bypasses RLS | Golding, Chapter 9 |
| R8 | A background handler gets the tenant from the message and declares it in the same way | Requirement 7 |
| R9 | Each tenant table is tested with real PostgreSQL: read, update, insert for another tenant | Khorikov, Chapter 10 |
| R10 | At startup the application checks its own account. If the account, or a role it is a member of, is a superuser, can bypass RLS or owns a table, the application does not start. The readiness check asks the same. | Nygard, Chapter 5 (Fail Fast). PostgreSQL documentation, Privileges |
| R11 | The membership table has one more policy, `own_memberships`: `FOR SELECT` only, on `user_id` = the declared user (`app.user_id`, set in the transaction like the tenant). It serves `GET /v1/me/tenants`. Any policy other than `tenant_isolation` must be on an explicit list, and a test fails on any other | Postgres combines permissive policies with OR. A write policy would widen access |
| R12 | Deferred (see Deferred): the system admin sees tenant and member counts from ControlPlane's own summary data. The tenant list of step 19d has no member counts. Memberships are under RLS and the list reads across tenants, so a count needs that summary data, which does not exist yet | Golding, Chapter 2 |

Work across tenants goes through the tenant list and declares each tenant in turn. RLS does not protect a cache or data that leaves the database. An EF Core query filter is not added as a second layer. RLS is the single source of truth.

## 5. Identity and authorization

The code checks permissions, not roles. Provider staff enter through a separate door.

| # | Rule | Source |
| --- | --- | --- |
| A1 | Authorization is permission based. The code asks for a permission, not for a role name | OWASP API Top 10, API5. Golding, Chapter 6 |
| A2 | The built-in roles are Owner, Admin and Member. They are the same in each tenant and cannot be changed | Industry practice |
| A3 | A tenant builds its own role (custom role) from the fixed permission list. This feature is not in the first template | Industry practice |
| A4 | A system admin is not a tenant member. The staff list is in ControlPlane. It has its own permission list | Golding, Chapter 2 |
| A5 | Each endpoint carries one of four explicit states: public, signed-in only, requires a permission, checks a signature. An endpoint without a state breaks the architecture test. Each endpoint on the tenant path requires a permission. Each endpoint on the webhooks path has no user and declares `SignatureChecked`, and only those endpoints do. The webhooks group takes a request body of at most 1 MB (`RequestSizeLimitAttribute`), so a larger body answers 413 before its signature is checked (API4) | OWASP API Top 10, API5 |
| A6 | The system door requires a second factor. The rule is in ControlPlane. The host only translates the identity provider's field into a neutral value. Tenant users are not affected | OWASP ASVS, item 4.3.1 |
| A7 | A catalog user keeps a copy of their primary email address, first name and last name. The identity provider owns these fields; T6 does not change. The path that creates a catalog user (accepting an invitation, the first system admin's first visit) fills the copy from the same Backend API call that returns the verified addresses. The webhook `POST /v1/webhooks/clerk` keeps it fresh: `user.updated` becomes `UpdateUserProfile`, which sets the copy of a known user, and an event older than the copy's `ProfileUpdatedAt` is ignored. `user.created`, any other type, and an event for a user the catalog does not know change nothing | Clerk, "Sync data to your app with webhooks" |
| A8 | The copy holds only what a screen needs: no image, no phone number, no metadata. `user.deleted` becomes its own command, `DeleteUser`, which erases the email address and the name and sets `DeletedAt`. The row stays, because memberships and audit entries point to it. An update that arrives after the deletion is ignored | GDPR, Article 5(1)(c) (data minimisation). Clerk, "Sync data to your app with webhooks" |
| A9 | A system admin can suspend a catalog user. A suspended user gets 403 `user.suspended` on every `me` and `tenant` endpoint, accepting an invitation included. The check runs on every request, in one place: `TenantResolutionMiddleware` asks ControlPlane (`IUserDirectory`) for an endpoint with the `me` or the tenant metadata, and authorization turns the user away. It runs before the membership, so a suspended user who is also in a suspended tenant gets `user.suspended`. No endpoint can go around it: a test fails for a `me` or tenant endpoint without its group's metadata, or with anonymous access (`SuspendedUserRoutesTests`). The admin door is not checked: a system admin cannot be suspended (409 `user.is_system_admin`), so the staff keep their door. The identity provider is not told; the user can still sign in, and our tables refuse them | OWASP API Top 10, API1 and API5 (one check no endpoint skips, as T7). NIST SP 800-53 Rev. 5, AC-2 (disable accounts) |

Tenant users and staff sign in with the same identity provider instance. Our tables decide which door a person can use. Terms: "system admin" (Golding), "built-in roles" and "custom roles".

## 6. Modules and tenant creation

There are three modules. A system admin creates the tenant and the first Owner comes by invitation.

| Module | What it holds |
| --- | --- |
| ControlPlane | Tenant record, system admins, users, memberships, roles and permissions, invitations, tenant creation, tenant and user suspension |
| Notifications | It hears an event and sends a message to a person. In the first template, only the invitation email |
| Audit | It hears an event and writes the record of "who did what and when" |

Source: Golding, Chapter 2 (control plane). Khononov, Chapter 3 (when you are not sure, keep the boundary wide).

### Tenant creation process

The process is a saga. Three steps run in order.

The saga is part of the template, and tenant creation is its reference use. It meets all three conditions of S2: the process covers ControlPlane, Notifications and an email service, it needs compensation and timeouts, and someone asks where it is. A product built on the template keeps it. A new saga, or a new step, must meet S1 and S2 too.

1. ControlPlane writes the tenant ("provisioning"), the Owner invitation and the saga record in one transaction. The invitation code and its link are created here. Compensatable.
2. The tenant becomes "active". Pivot.
3. Notifications sends the invitation email. It is tried a limited number of times, with growing pauses.

The Owner signs up with the identity provider like any other user. Tenant creation creates nothing in the identity provider.

Tenant creation messages (N1 to N3):

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `CreateTenant` | `POST /v1/admin/tenants` | ControlPlane, `CreateTenantHandler` (step 1, starts the saga) | Yes |
| `ActivateTenant` | Step 1 | ControlPlane, `ActivateTenantHandler` (step 2, pivot) | Yes |
| `ActivationTimedOut` | Step 1, scheduled | ControlPlane, `TenantCreationSaga` | Yes |
| `TenantActivated` | Step 2 | Audit, `RecordTenantCreatedHandler` | Yes |
| `ActivateTenantReply` | Step 2 | ControlPlane, `TenantCreationSaga` | Yes |
| `OwnerInvitationReady` | Step 2 | Notifications, `SendInvitationEmailHandler` (step 3, calls the email service) | No |
| `InvitationEmailTimedOut` | Saga, scheduled | ControlPlane, `TenantCreationSaga` | Yes |
| `SendInvitationEmailReply` | Step 3 | ControlPlane, `TenantCreationSaga` | Yes |
| `CancelTenant` | Saga | ControlPlane, `CancelTenantHandler` (compensation) | Yes |
| `CancelTenantReply` | `CancelTenantHandler` | ControlPlane, `TenantCreationSaga` | Yes |
| `CancellationTimedOut` | Saga, scheduled | ControlPlane, `TenantCreationSaga` | Yes |
| `CancelInvitation` | Saga | ControlPlane, `CancelInvitationHandler` (compensation) | Yes |
| `RaiseTenantCreationAlarm` | Saga | ControlPlane, `TenantCreationAlarmHandler` | No |
| `Fault<T>` of a step or a compensation | Wolverine, when the message goes to the dead letter queue | ControlPlane, `TenantCreationSaga` | Yes |

The saga's record is the table `control_plane.tenant_creation_sagas`. The alarm is the error log event `TenantCreationNeedsAttention` and the counter `tenant_creation.needs_attention`.

- If step 2 fails for good, the tenant is cancelled. A reason code is stored on the tenant and logged. The tenant list for the system admin shows the status `cancelled`, not the reason.
- If step 3 fails for good, or a wait after step 1 times out, the process goes to "needs attention" and the invitation is cancelled. The alarm is raised once.
- The invitation link travels to Notifications inside the message. See the deviations list.
- The invitation link carries one invitation code: the tenant id and a secret, `<tenantId>.<secret>`. Only the hash of the secret is stored. Accepting declares the tenant from the code, then finds the invitation by the hash in that tenant. A missing or empty code answers 400. A wrong tenant, a wrong secret and a malformed code all answer 404. This is the only place where a request names its tenant outside the path. The secret gives the right, not the tenant id.
- Accepting an invitation needs two things: the secret in the code, and a verified email address of the signed-in user that is the same as the invited address. The verified addresses come from the identity provider's server, not from the request.
- The email handler is in `Notifications.Application`. The event type is in `ControlPlane.Contracts`. ControlPlane does not know how to send email.
- Invitation expiry is checked when the invitation is read. There is no nightly job.
- The first system admin comes from an email address in configuration. The application writes it only while the staff list is empty.

Source: Golding, Chapter 4. Richardson, Chapter 4.

### Tenant suspension

A system admin suspends a tenant and reactivates it. This is not a saga: one module changes one row (S1). The command runs in the tenant's own transaction, with the tenant declared from the admin route (W3), and locks the tenant row.

| Stored status | `POST .../suspend` | `POST .../reactivate` |
| --- | --- | --- |
| `Active` | Becomes `Suspended`, 200, answered as `suspended` | No change, 200, answered as `active` |
| `Suspended` | No change, 200, answered as `suspended` | Becomes `Active`, 200, answered as `active` |
| `Provisioning`, `Cancelled` | 409 `tenant.not_active` | 409 `tenant.not_active` |

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `SuspendTenant` | `POST /v1/admin/tenants/{tenantId}/suspend` | ControlPlane, `SuspendTenantHandler` | Yes |
| `ReactivateTenant` | `POST /v1/admin/tenants/{tenantId}/reactivate` | ControlPlane, `ReactivateTenantHandler` | Yes |
| `TenantSuspended` | `SuspendTenantHandler`, on a change of status | Audit, `RecordTenantSuspendedHandler` (`tenant.suspended`) | Yes |
| `TenantReactivated` | `ReactivateTenantHandler`, on a change of status | Audit, `RecordTenantReactivatedHandler` (`tenant.reactivated`) | Yes |

- A request that changes nothing publishes nothing, so Audit records each change once.
- A member who calls a tenant route gets 403 `tenant.suspended`. A non-member still gets 404 (T3): only a member learns that the tenant is suspended.
- `GET /v1/me/tenants` lists active and suspended tenants, each with its status. Provisioning and cancelled tenants stay hidden.
- Accepting an invitation to a suspended tenant answers 409 `tenant.suspended` only for a valid invitation and the right person, as T3. The order is: not found or cancelled (404), staff (422 `invitation.staff_cannot_join`), already a member (409 `membership.exists`), the invitation's own checks (`invitation.not_pending`, `invitation.expired`, `invitation.email_mismatch`), then `tenant.suspended`, then the change. A stranger with the link keeps the invitation's own answer, and an expired invitation stays dead after a reactivation. The invitation's checks are a pure method (`CheckAcceptance`) that `Accept` also uses, so the refusal comes before any change (W7). The invitation stays pending and can be accepted after the reactivation.
- Messages already in flight for the tenant still run.
- A suspended tenant is past the pivot: a late compensation does not cancel it.
- Reactivation gives the members their access back as before.

Source: Golding, Chapter 2 (control plane, tenant management).

## 7. Side tools and configuration

Only email goes into the first template. Hangfire, SignalR and cache code do not.

- Notifications does not know the email service directly. Our interface is between them, and the service is an adapter. Source: Freeman and Pryce, GOOS Chapter 8.
- Each email carries an idempotency key. Example: `invite/` and the invitation id. The same key does not produce a second email.
- Tests send no real email. They use a hand-written fake adapter.
- The messaging tool's own dispatcher runs the outbox. A scheduler is not used for this.

Three helper tools are in scope. The OpenAPI document and Scalar are on only in the development environment. OpenTelemetry sends logs, metrics and traces over OTLP. The target address comes from configuration. If there is no address, no data is sent. A test checks the trace id rule: the id on the request is the same in the handler of the event that the request caused.

The build writes the OpenAPI document to openapi/v1.json, and the file is committed. A change to the API then shows in the pull request. A frontend client can be generated from it later.

### Configuration

1. Each module has its own configuration section. A module does not read another module's configuration.
2. Configuration is validated at startup. If a required setting is missing, the application does not start and names the missing key.
3. A value that changes between environments comes from an environment variable.
4. Passwords and keys are not written in `appsettings.json`.

Source: Twelve-Factor, Factor III. Nygard, Chapter 5 (Fail Fast).

### Container image

The API has one container image, built from `apps/api/Dockerfile`. Every process runs from it, and only the configuration and the command change. `Host:Role` selects the web or the worker role. The migration is a command of the same image: `docker run <image> migrate`. The web process listens on port 8080 for the API and on port 8081, the management port, for the health endpoints. A worker listens only on port 8081. Both listen on all interfaces (section 8). The process runs as the base image's non-root user. The image holds no secret, no connection string and no `appsettings.Development.json`. The runtime base image is chiseled: it has no shell and no package manager. It runs in invariant globalization mode, because the code needs no ICU and no time zone data. The image has no Kerberos library, so the API turns GSSAPI encryption off on each database connection (`docs/configuration.md`). CI builds the image and scans it. Then it runs the migration, the web and the worker against PostgreSQL. It checks that each process is ready and that no process tries to load the Kerberos library.

Source: Twelve-Factor, Factors V, VII, IX and XII. CIS Docker Benchmark 4.1. Humble and Farley, *Continuous Delivery*, Chapter 5.

## 8. Resilience and security

### Resilience

| Situation | Rule | Source |
| --- | --- | --- |
| An external service does not answer | Each external call has a time limit | Nygard, Chapter 5 (Timeouts) |
| An external service is down for a long time | The gap between tries grows. When the limit is reached, the step fails for good: the saga compensates, or goes to "needs attention" | Nygard, Chapter 5 |
| The same request arrives twice | The host's idempotency layer claims the key and saves the handler result in the business transaction. A second request replays it (I1 to I7) | Stripe, "Idempotent requests". Brandur Leach, atomic phases |
| A pod is shutting down | The pod takes no new work and finishes the work it has | Twelve-Factor, Factor IX |
| The health of a pod is not known | There are two check endpoints: "I am up" and "I can reach the database" | Industry practice |
| Someone looks for a failure | The trace id stays the same in the log, in the event and in the handler | Nygard, Chapter 8. Twelve-Factor, Factor XI |

### Security

| Attack | Protection | OWASP |
| --- | --- | --- |
| Another tenant's id is put in the path | Membership check and RLS | API1 |
| A forged or expired token | Signature, expiry and issuer are verified on each request. Only a session token is accepted: a token without the session's id (`sid`), such as a JWT template's token, is refused | API2 |
| A forged or replayed webhook delivery | Svix's library checks the signature with the endpoint's signing secret before the body is parsed, and refuses a timestamp more than five minutes from now. A refused delivery answers 400 and changes nothing | API2 |
| Extra fields are added to a request | An endpoint reads only the defined fields | API3 |
| Too many requests | A rate limit for each user, kept in each pod. A caller without a user, such as a webhook, is limited by its client address. Accepting an invitation has a stricter limit of its own. Lists are paged and the page size has a limit | API4 |
| A tenant user calls a system endpoint | The system door checks the staff list | API5 |
| A suspended user or tenant | A suspended user gets 403 `user.suspended` on every `me` and tenant endpoint, checked in one place on each request (A9). A member of a suspended tenant gets 403 `tenant.suspended` at the tenant door; a non-member still gets 404, so a stranger learns nothing (T3). A system admin cannot be suspended | API1, API5 |
| Internal structure is read from an error message | The error response has one shape and returns no internal detail. A 401 does not say why the token failed. A request the server refuses while it reads it, such as a body over its endpoint's size limit, keeps the server's status (413, 400 or 408); any other exception is a 500 | API8 |
| A browser request from another site | Only permitted origins can call | API8 |

All business endpoints start with `/v1`. The only exception is the two health endpoints: `/health/live` and `/health/ready`. These endpoints serve the infrastructure, need no identity and return only "healthy" or "unhealthy". Neither health endpoint is reachable from outside. The outside sees no health information at all.

### Two ports

| Port | Default | Serves | Reached from outside |
| --- | --- | --- | --- |
| Public | 8080 in the container image, which sets `ASPNETCORE_HTTP_PORTS=8080`. The process reads `ASPNETCORE_URLS`, else `ASPNETCORE_HTTP_PORTS` and `ASPNETCORE_HTTPS_PORTS`, else uses `http://localhost:5000` | The business endpoints (`/v1/...`) and, in Development, the OpenAPI document and Scalar | Yes, through the platform's domain |
| Management | 8081 (`Host:ManagementPort`) | `/health/live` and `/health/ready` only | No. Only the platform's health check and Kubernetes probes reach it |

- The web process listens on both ports. A worker listens only on the management port: it has no business endpoints.
- A health path on the public port answers 404. Any other path on the management port answers 404.
- The process chooses by the port that the connection arrived on (`HttpContext.Connection.LocalPort`), never by the `Host` header. A client can put any port in the `Host` header.
- The health endpoints are not endpoint routes. A branch of the pipeline on the management port answers them, before authentication runs.
- Kestrel ignores `ASPNETCORE_URLS` and `ASPNETCORE_HTTP_PORTS` when it is given an endpoint in code. So the host adds the management port to the public addresses that those settings name.
- The process does not start when the management port is a public port.
- The platform routes public traffic to 8080 only. On Railway, `PORT` is 8081 for the health check, and the domain targets 8080. On Kubernetes, both probes use port 8081, and the Service exposes only 8080 (`setup.md`).

Source: Microsoft Learn, "Health checks in ASP.NET Core", "Require host": restrict the health endpoints to a port for monitoring services, and check `HttpContext.Connection.LocalPort`, because the `Host` header can be spoofed. Microsoft Learn, "Configure endpoints for the ASP.NET Core Kestrel web server". Railway docs, "Healthchecks", "Configure the healthcheck port". Spring Boot's `management.server.port` is the same practice.

## 9. Testing

The test is written first. Tests run with real PostgreSQL. We do not fake our own database.

| Kind | Example | Runs with |
| --- | --- | --- |
| Unit | "A cancelled tenant cannot be activated" | Code only |
| Integration | "An Acme user cannot read a Globex row" | Real PostgreSQL |
| Architecture | "No module references the inside of another module", "there is no endpoint without an access state" | Compiled code |
| End to end | A system admin creates a tenant. The Owner gets an email, accepts it and sees the tenant in the list. Another user gets 404 | The full system, real PostgreSQL |

1. The end-to-end test is written first. Source: Freeman and Pryce, GOOS Chapters 4 and 5.
2. Real PostgreSQL runs in a container. An in-memory fake database is not used. Source: Khorikov, Chapter 10.
3. Tests connect with the application account. Migrations run with the separate account. If not, RLS tests have no meaning.
4. Only external services are faked: the identity service and email. A fake is a hand-written implementation of our interface. Source: Khorikov, Chapter 9. Time is faked with FakeTimeProvider. Tokens in tests are signed with a test key the host is given.
5. Each test creates its own tenant.

Unit test standard:

- Each test kind is in its own project: .UnitTests, .IntegrationTests, .EndToEndTests and .ArchitectureTests (N9).
- Each project carries a `TestClassification` trait.
- The structure is Arrange, Act, Assert.
- A test has no `if` and no loop.
- The name pattern is `Operation_Scenario_ExpectedOutcome`. `Operation` is the name of the work, not the method name. Example: `ActivateTenant_WhenCancelled_IsRejected`.

An architecture test enforces the trait, the name pattern and the rule against branches and loops. The branch check does not see loops written with LINQ. Use a theory for several cases.

An architecture test fails when a module DbContext has model changes without a migration (`PendingModelChangesTests`). Source: Microsoft, "Managing Migrations" (EF Core), check for pending model changes.

Module integration tests call a handler with the module's DbContext on real PostgreSQL, inside a transaction the test opens. The Wolverine pipeline is tested in Api.IntegrationTests. The spike in apps/api/spikes is outside the test standard.

The order for a fix is: first a red test that checks the rule, then the fix.

## 10. Names and places

One concept has one name in the code, the route, the table and the documents. A name says what a thing is. A folder says what the code is about and who uses it. Where a rule names a test, the test checks the part of the rule that the note names. The rest of the rule is checked in review.

Sources: Evans, *Domain-Driven Design*, Chapter 2 (Ubiquitous Language). Richards and Ford, *Fundamentals of Software Architecture*, "Component-Based Thinking" (domain partitioning). Robert C. Martin, "Screaming Architecture". Microsoft Learn, "Route handlers in Minimal API apps", route groups. OWASP API Security Top 10 2023, API5. Richardson, *Microservices Patterns*, Chapter 4 (command and reply).

### Names

| # | Rule | Source |
| --- | --- | --- |
| N1 | A feature has one root word. An action (endpoint, command, handler) uses its verb form: `CreateTenant`, `CreateTenantHandler`. A record (saga, table) uses its noun form: `TenantCreationSaga`, `tenant_creation_sagas`. The documents use the noun form: "the tenant creation process". A configuration key keeps its name when a type is renamed | Evans, Chapter 2 |
| N2 | A class that derives from Wolverine's `Saga` ends with `Saga`. Its table ends with `_sagas`. Every table name is plural. Test of the saga class and table names: `SagaNameTests` | Evans, Chapter 2 |
| N3 | A saga handles three kinds of message only: the replies to its steps (`...Reply`), its own timeouts (`...TimedOut`) and Wolverine's `Fault<T>`. It handles no public event. Only a saga handles a `...Reply`. A reply is named after the work it answers: `ActivateTenantReply` answers `ActivateTenant`. The name makes S13 visible. Test of both sentences: `SagaMessageTests` | Richardson, Chapter 4 |
| N4 | A module's schema is the module's name in snake case: `control_plane`, `audit`. The host's schema is `api`, and its DbContext is `ApiDbContext`. A module's DbContext is `{Module}DbContext`. A name that describes a concept, not the module, stays: `ITenantCatalog` is the tenant catalog. Test of the schema name: `NamesAndPlacesTests` | Evans, Chapter 2. Richards and Ford, "Component-Based Thinking" |
| N5 | An audience is who calls an endpoint. Its folder, its route prefix and its permission prefix are the same word (table below). A new audience gets a new folder, a new route prefix and a new host file. Example: `Webhooks/`, `/v1/webhooks`, `Api/Webhooks/WebhooksRoutes.cs`. A webhook endpoint is not in the OpenAPI document: the document is the contract for our own clients, and a webhook's sender does not read it. Test of the endpoint folder against the route prefix: `EndpointPlaceTests` | Microsoft Learn, route groups. OWASP API5 |
| N6 | A permission is `audience.resource.action`, in lower snake case. The audience is `admin` or `tenant`. The action is coarse: `read`, `create`, `update`, `delete`, or a business verb. `read` covers a list and a single record. The `me` audience has no permissions: being signed in is enough, and the user reaches only their own data. The `webhooks` audience has no permissions either: a signature proves the sender. The system pool holds `admin.tenants.create`, `admin.tenants.read`, `admin.tenants.suspend`, `admin.tenants.reactivate`, `admin.users.read`, `admin.users.suspend` and `admin.users.reactivate`; `suspend` and `reactivate` are business verbs. The tenant pool holds `tenant.members.read`. Test of the format and the two audiences: `NamesAndPlacesTests` | OWASP API5 |

| Audience | Route prefix | Folder | Host file and method | Permission prefix |
| --- | --- | --- | --- | --- |
| System admin | `/v1/admin` | `Admin/` | `Api/Admin/AdminRoutes.cs`, `MapAdmin` | `admin.` |
| The signed-in user, acting for themselves | `/v1/me` | `Me/` | `Api/Me/MeRoutes.cs`, `MapMe` | none |
| A tenant member | `/v1/tenants/{tenantId}` | `Tenant/` | `Api/Tenant/TenantRoutes.cs`, `MapTenant` | `tenant.` |
| An external system, proven by a signature | `/v1/webhooks` | `Webhooks/` | `Api/Webhooks/WebhooksRoutes.cs`, `MapWebhooks` | none |

The word for the system admin audience is `Admin`, not `System`. A folder named `System` makes the namespace `...Api.System`, and that namespace hides .NET's `System` namespace. The role keeps its name, "system admin", and it still passes the system door (A6). `Me` is a Visual Basic keyword, so CA1716 does not check namespaces (`.editorconfig`). It still checks types and members.

### Places

| # | Rule | Source |
| --- | --- | --- |
| N7 | In a module's `Api` project, the endpoints sit in the audience folder, one file for each resource: `Admin/Tenants.cs`, `Admin/Users.cs`, `Me/MyTenants.cs`, `Me/Invitations.cs`, `Tenant/Members.cs`, `Webhooks/ClerkEvents.cs`. The module entry point (`ControlPlaneModule.cs`) calls the files. An endpoint is a method with the action's name, the same name as its handler in `Application`. A webhook endpoint receives several event types, so it is named after what it receives: `ReceiveClerkEvent` sends `UpdateUserProfile` or `DeleteUser`. The request and response types sit at the end of the file that uses them. A request is named by its action (`{Action}Request`), a response by its resource (`{Resource}Response`). A second shape of the same resource carries its audience in the name (`MyTenantResponse`). A status value in a response is one lowercase word: `provisioning`, `active`, `suspended`, `cancelled` or `deleted`. The database and ControlPlane keep their own names (`Active`); the module's `Api` project maps them in one place (`ResponseStatus`) when it builds each response, so a saved idempotent result replays to the same answer. Every request and response name is unique in the API, because the OpenAPI document builds its schema names from the type names. Tests of the audience folder (`EndpointPlaceTests`) and of the unique names (`NamesAndPlacesTests`) | Martin, "Screaming Architecture" |
| N8 | The folders of `Application` follow the work. The folders of `Domain` follow the aggregate. The folders of `Infrastructure` follow the external system: `Persistence/`, `Clerk/`, `Email/`. A feature with more than one file gets its own folder, named by N10. A feature with one file stays in its resource folder. Code that several features of one module use goes into a folder named by what it is: `Ports/`, `Persistence/`. No project has a folder named `Helpers`, `Utils`, `Utilities`, `Common` or `Misc`. Code that several modules use lives only in `SharedKernel` or `Tenancy`. Test of the folder names: `NamesAndPlacesTests` | Richards and Ford, "Component-Based Thinking". Martin, "Screaming Architecture" |
| N9 | A test project that tests one module sits in the module's folder. A test project that tests a shared project sits next to it. Tests of the whole system (host, architecture, end to end) sit in `apps/api/tests/`. A test project is named `{Project}.{Kind}Tests`, where Kind is Unit, Integration, EndToEnd or Architecture. Inside a test project the folders follow the project it tests. A test class is named `{Feature}Tests`. A test that checks several features goes into the folder of the feature it checks last. Helper files go into `Fixtures/`. Test of the project name: `NamesAndPlacesTests` | Section 9. Industry practice |
| N10 | A feature with more than one file always gets a folder in the noun form of its root, never the action name: `Tenants/TenantCreation/` holds `CreateTenant`, and `Invitations/InvitationEmail/` holds `SendInvitationEmailHandler`. The reason: the folder becomes a namespace, and a folder named after the action hides the command type of the same name (CS0118). For the same reason, no folder has the name of a type its project declares or uses from a project it references. Test of that last sentence: `NamesAndPlacesTests` | Cwalina, Barton, Abrams, *Framework Design Guidelines*, "Names of Namespaces" |

The ControlPlane module after these rules:

| Project | Folders |
| --- | --- |
| `ControlPlane.Api` | `Admin/`, `Me/`, `Tenant/`, `Webhooks/`, `Authentication/` (the signed-in user's id), `ControlPlaneModule.cs` |
| `ControlPlane.Application` | `Tenants/TenantCreation/` (command, handler, validator, saga, steps, messages, settings, alarm), `Tenants/ListMyTenants.cs`, `Tenants/ListTenants.cs`, `Tenants/SuspendTenant.cs`, `Tenants/ReactivateTenant.cs`, `Tenants/TenantDetails.cs` (the admin's shape of a tenant, which four features use), `Invitations/`, `Members/`, `Users/UpdateUserProfile.cs`, `Users/DeleteUser.cs`, `Users/ListUsers.cs`, `Users/SuspendUser.cs`, `Users/ReactivateUser.cs`, `Users/UserDetails.cs` (the admin's shape of a user, which three features use), `Ports/`, `Errors.cs` |
| `ControlPlane.Domain` | One folder for each aggregate. `PermissionPools.cs`, which several aggregates use, sits at the root |
| `ControlPlane.Infrastructure` | `Persistence/` (DbContext, its design-time factory, migrations, `TenantCatalog`, `TenantDirectory`, `UserTenants`, `UserCatalog`, `UserDirectory`, `SystemAdminDirectory`, `SystemAdminSettings`), `Clerk/` (the Backend API and the webhook), `ControlPlaneInfrastructure.cs` |

## Deviations from the books

These deviations were discussed openly and approved.

| Deviation | The way in the books | Reason |
| --- | --- | --- |
| The tenant is in the API path, not in the token | Golding, Chapter 6: the tenant comes in the token | The identity provider stays authentication only. A cancelled membership takes effect immediately. The Azure guidance accepts this way |
| The isolation unit is a schema, not a database | Richardson: a database for each service | The rule is for microservices. It is adapted to a monolith. The change back is cheap |
| There is one shared outbox | Richardson, Chapter 3: the outbox is in the service's own database | An outbox row is a delivery record, not business data. The wolverine tables are not under RLS. They hold message bodies, invitation links included, until Wolverine removes handled messages. |
| Each module is five projects | Khononov, Chapter 10 and Fowler, PoEAA Chapter 2: a simple structure for a simple module | One pattern was requested. The cost is some nearly empty projects |
| The test name pattern is `Operation_Scenario_ExpectedOutcome` | Khorikov, Chapter 3: a plain sentence | Common in .NET. `Operation` is the name of the work |
| The `Api` and `Application` projects see Wolverine types | Khononov, Chapter 8 (Ports and Adapters): business logic does not see the infrastructure tool, a port is between them | The only reason for our own interface was "what if the tool changes". There is no concrete limitation. `Domain` and `Contracts` stay clean |
| Handler, saga, message and DbContext types are public | Ousterhout, Chapter 5: a module hides its internal detail | Wolverine compiles handler code in a separate assembly. Project references and the architecture test protect the module boundary |
| The invitation link goes to Notifications inside the message | OWASP, Forgot Password Cheat Sheet: a token is stored only as a hash | The link cannot be produced again later. The secret travels in ActivateTenant and OwnerInvitationReady and stays in the messaging tables until Wolverine removes handled messages. Protection: the verified email of the person who accepts must be the same as the invited address, the link is single use and expires, and an invitation whose email does not go out is cancelled. If a strict audit requires it, change to a signed token |
| The rate limit is kept in each pod | OWASP API4: a limit for each user | A shared store (Redis or the database) is infrastructure the first template does not need. The real limit is the setting times the number of pods |
| In Development, email without an email service is written to the log, link included | OWASP Logging Cheat Sheet: secrets are not logged | Local development and the smoke run need the link without an email account. Outside Development the application does not start without the email service |
| The API image has no `HEALTHCHECK` | CIS Docker Benchmark 4.6: add a `HEALTHCHECK` instruction to the image | The chiseled image has no shell and no `curl` to run a check. The platform checks `/health/live` and `/health/ready` over HTTP on the management port: its own HTTP health check or Kubernetes probes (`setup.md`). Docker ignores `HEALTHCHECK` under Kubernetes |

## Deferred

These parts are not in the first template. Each one comes in when its written condition is true.

| What | Entry condition |
| --- | --- |
| Hangfire | The first recurring job tied to a clock appears and the messaging tool cannot do it |
| SignalR | In-app notifications come into scope |
| Cache code | There is a measured slowness. The rules are ready |
| Circuit breaker | A request that a user waits for goes directly to an external service |
| A separate database account for ControlPlane | A product or audit requirement asks for it |
| Slug in addresses | The frontend wants the tenant name in the address. The slug field is ready. The API path continues to use the id |
| Read model | A report appears that needs a join across modules |
| Pre-generated handler code | Startup time becomes a problem or the RuntimeCompilation package is not wanted in production. Try it first |
| Features | Member invitation, custom roles, member removal, tenant deletion, support access, in-app notifications, a second system admin |
| An audit log of system admin actions above tenants, such as a user's suspension | A product or audit requirement asks for it. An audit entry belongs to a tenant (`AuditEntry` is a tenant entity, under RLS), and a user is above tenants, so the log needs a table outside RLS of its own |
| The tenant creation state ("needs attention") in the admin's tenant list | The admin app needs it. The saga record belongs to the tenant (RLS), and the list reads across tenants, so the state needs a copy outside RLS or a read that declares each tenant in turn |
| Member counts in the admin's tenant list (R12) | The admin app needs them. They come from ControlPlane's own summary data |

## Open items

None.
