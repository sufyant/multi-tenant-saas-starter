# AGENTS.md

This is the API of a multi-tenant SaaS starter template: a .NET modular monolith on PostgreSQL. Every future product is built on it, so correctness, clear boundaries and tenant isolation matter more than speed.

## Source of truth

- `docs/ARCHITECTURE.md` holds every architectural rule, each with an id (`R4`, `W2`, `S13`), except the few in section 6, "Rules without an id". It is authoritative. If this file and it disagree, it wins.
- Read the section of `ARCHITECTURE.md` for the area before you change anything in it. Cite a rule by its id; do not copy rules into code comments.
- Read `ARCHITECTURE.md` and only the flow files of your task. A flow file in `docs/flows/` says what happens, step by step, and cites the rules by id; it defines no rule (`RuleIdTests`). The flow files:
  - `tenant-creation.md`: a system admin creates a tenant and invites its first Owner in one transaction.
  - `invitations.md`: the identity provider's sign-up of every invitation, a member's invitation, and the platform's invitation of an Owner.
  - `tenant-lifecycle.md`: a system admin renames, suspends, reactivates, closes and reopens a tenant.
  - `tenant-purge.md`: the saga that deletes a closed tenant's data when its reopen period ends.
  - `owner-recovery.md`: the platform makes an existing member of a tenant an Owner.
  - `tenant-summary.md`: the summary row that the platform's tenant list reads across tenants.
  - `what-the-screens-read.md`: the fields and the endpoints that the tenant web and the platform app read.
  - `user-deletion.md`: a system admin deletes a user, in steps across the user's tenants.
  - `system-admins.md`: the root system admins from configuration, the system door, and the grant and revocation of system admins.
  - `member-management.md`: an Owner or an Admin changes a member's role and removes a member.
  - `custom-roles.md`: a tenant creates, updates and deletes its own roles.
  - `language-and-time-zone.md`: the languages of the web and the emails, and a user's language and time zone.
  - `reading-the-audit-logs.md`: an Owner, an Admin or a system admin reads an audit log.
- Other documents: `docs/api-catalog.md` (every endpoint: who may call it, its audit entry, its notification), `docs/local-run.md` (run it locally), `docs/configuration.md` (every setting), `docs/setup.md` (provider notes), `docs/licenses.md` (every package and its license).

## Commands

Run from `apps/api/`.

- Build: `dotnet build Api.slnx`. No new warnings.
- Test: `dotnet test --solution Api.slnx`. Needs Docker: tests run on PostgreSQL 18 in a container.
- Format C#: `dotnet format whitespace Api.slnx`.
- Write Wolverine's handler code after a change to a handler, a message, a saga, a middleware or a registration: `pnpm codegen` (`pnpm api:codegen` from the repository root). Commit `src/Api/Internal/Generated`. `HandlerCodeFileTests` fails otherwise. (W10)
- Run every generator after a change to a source of generated code: `pnpm generate` from the repository root. It writes the CSS of the design tokens, the permission list, Wolverine's handler code, `openapi/v1.json`, the API client and the emails of `packages/emails`, in that order. Commit what it changes; the CI job `generated` fails otherwise. A version change of a generator package, such as Wolverine or Orval, is a change to a source of generated code too. (D1, "Generated files" in section 2)
- Add a migration to module X: `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/Modules/X/X.Infrastructure --output-dir Persistence/Migrations`.
- Run the system: follow `docs/local-run.md`. One build output; `Host:Role` is `web`, `worker` or `all`.

## Closed decisions

These were decided with the maintainer, with their sources. Do not reopen one, and do not propose to, unless the maintainer asks or you have new information. Then say what the new information is.

- Tenant creation is one transaction, not a saga: its first write and its pivot are both writes in ControlPlane's own database, and only retryable steps follow. It takes the path of `InviteOwner`. The template's reference use of a saga is the tenant purge (step r7). (The maintainer's decision D1 in step r3; `docs/flows/tenant-creation.md`; rules S1 to S13 in section 3)
- The tenant id is in the API path and is proven by membership. It never comes from the token, the body, the query or a header. (T1, T2, Deviations)
- The identity provider only authenticates. Tenants, roles and permissions live in our tables. (T6, A1 to A6)
- The identity provider only signs people in. It is invite-only: the API lets an invited address without an account sign up through the provider's invitation, and the Owner signs up like any invited person. Tenant creation creates no tenant, role or user in the provider. ControlPlane owns the names; the API sends the provider none and reads none, and no webhook flows back. (A7, A8; `docs/flows/invitations.md`, section 1, "Invitation sign-up"; the maintainer's decisions in step 20i)
- RLS is the only tenant filter. No EF Core query filters as a second layer. (R1 to R11)
- Wolverine is used as it is: dispatcher, EF Core transactions, outbox, PostgreSQL transport, saga. No custom transaction wrapper, mediator, outbox or saga store. No Wolverine.Http. (Section 3, W1 to W10)
- One build output, two process roles: web and worker. (Section 1, W5)
- The scope of the first template and the deferred list. Do not add a deferred feature or tool. (Purpose and scope, Deferred)
- No commercially licensed packages: MediatR, AutoMapper, MassTransit v9+, FluentAssertions. A new package needs a license check and a row in `docs/licenses.md`.

## Known traps

- Wolverine runs a saga alone at the endpoint its message arrives on. If another handler shares that message, it never runs in the web and worker layout. A saga hears its steps through replies sent to it. (S13, `SagaMessageTests`)
- A test that passes with `Host:Role=all` can fail with web and worker. Once, the Audit handler never ran in production layout while every test in the role `all` passed. Test message flows in the web and worker layout. (`HostRoleTests`)
- Wolverine commits a handler that returns a failed `Result`. Reject before any change. (W7)
- Handler, saga, message and DbContext types are public, because Wolverine generates code in another assembly. (W9)
- A scheduled retry on a message with several handlers can run in a web host. Use inline retries there. (W5, `ScheduledRetryTests`)
- The tenant is declared per transaction with `set_config(..., true)`. A transaction declares one tenant or one user, never both, and never a second tenant; a migration that fills derived data, or moves rows to their own tenant's rows, is the one exception (R4).
- A model change without a migration fails `PendingModelChangesTests`. Run `dotnet ef migrations add`.
- A package update changes no document unless it adds or removes a package, or changes a license. `LicenseTests` fails then.
- Health endpoints answer only on the management port (`Host:ManagementPort`, 8081). A `curl` to 8080 gets 404. (Section 8, `ManagementPortTests`)
- A migration runs while the previous version still serves. A migration that breaks the previous version breaks production during the deploy. Expand first; contract in a later release. (`docs/deployment.md` at the repository root)
- A folder named `System` hides .NET's `System` namespace. The audience of the system admins is `Platform` (`/v1/platform`, `platform.*`): in the tenant web, "Admin" is a role. The other rules for names and folders are in section 10 of `ARCHITECTURE.md` ("Names and places").
- Me is a Visual Basic keyword. CA1716 does not check namespaces (`.editorconfig`); it still checks types and members.

## How we design

Reason from these principles. When unsure, ask what the sources in the reading list would say.

- Simplicity first. Deep modules with small interfaces. Do not build what .NET or Wolverine already gives you. Add capability for a real need, not in anticipation. (Ousterhout)
- The domain speaks the business language. Aggregates protect their invariants. (Evans; Vernon; Khononov)
- Pure core, effects at the edges. A saga decides and returns messages; it calls no external service. (Khorikov)
- Read through contracts, tell through events. Never reach into another module's internals or schema. (Richardson; Kleppmann)
- Check permissions, not roles. Code asks "may this user do X in this tenant", never "is this user an admin".
- The host connects modules and holds no business rule.
- Assume many instances. Any message may arrive twice; handlers are idempotent. (Twelve-Factor; Nygard)
- Every external call has a time limit and a bounded retry. (Nygard)

## Decisions

If a task needs a decision that `ARCHITECTURE.md` does not cover, or contradicts one, stop. Do not decide silently.

- Give two or three options. For each: what it costs, and its source (book and chapter, or named industry guidance).
- Recommend one, once. Change the recommendation only for new information, and say what it is.
- A deviation from the books goes into the deviations table of `ARCHITECTURE.md`, with the reason. Propose the change; do not accept it yourself.

## How we work

- Test first. A feature starts from a failing test at the outer boundary. A fix starts from a red test that checks the rule. (Beck; Freeman and Pryce)
- Branch from `main`: `feat/<name>`, `fix/<name>` or `docs/<name>`. Small commits: the test, then the change, then the documents. Open a pull request; do not merge it.
- The pull request description is a report: what changed for each item, the red run of each new test, the test count before and after by project, and every choice a reviewer could question.
- Before you open the pull request, run the `architecture-reviewer` subagent and fix what it finds.

## How we test

The rules are in section 9 of `ARCHITECTURE.md`. In short:

- Test behaviour, not implementation. Never fake our own database: tests use real PostgreSQL and connect as the application account. Fake only systems we do not own, with a hand-written implementation of our interface. (Khorikov)
- Test names: `Operation_Scenario_ExpectedOutcome`. No `if`, loop, `?:` or `??` in a test body. An architecture test enforces both.
- Every tenant table is covered by the isolation tests automatically. Architecture and isolation tests always pass.
- Fix a flaky test at its root; never add a retry.

## Writing

- Code, comments, documents and commit messages are in English.
- Documents follow ASD-STE100: short sentences, active voice, one idea per sentence. No em dashes.
- Every document in `apps/api/docs/` and `docs/` has one shape:
  - A level 1 title names the document.
  - The purpose comes next: a short paragraph under the title, or section 1.
  - Level 2 sections are numbered from 1 (`## 1. Name`), because other files cite a section by its number. A document of one table has none. Sections that introduce the document, such as its codemap or its purpose and scope, may come before section 1 without a number.
  - A document has no date of its last change: git keeps it (Hunt and Thomas, *The Pragmatic Programmer*, Topic 9, DRY).
- No secrets in the repository. Configuration comes from the environment.

## Definition of done

- Build passes with no new warnings. All tests pass.
- New behaviour is covered by tests written first.
- No boundary, isolation, scope or licensing rule was bent.
- `ARCHITECTURE.md` says what the code does.

## Reading list

| Area | Sources |
| --- | --- |
| Design | Ousterhout, *A Philosophy of Software Design*; Hunt and Thomas, *The Pragmatic Programmer* |
| Architecture | Richards and Ford, *Fundamentals of Software Architecture* (2nd ed.); Ford, Parsons, Kua, *Building Evolutionary Architectures*; Fowler, *Patterns of Enterprise Application Architecture* |
| Domain modelling | Evans, *Domain-Driven Design*; Vernon, *Implementing Domain-Driven Design*; Khononov, *Learning Domain-Driven Design* |
| Multi-tenancy | Golding, *Building Multi-Tenant SaaS Architectures*; Azure Architecture Center multitenant guidance |
| Data and messaging | Kleppmann and Riccomini, *Designing Data-Intensive Applications* (2nd ed.); Richardson, *Microservices Patterns*; Hohpe and Woolf, *Enterprise Integration Patterns* |
| Testing | Freeman and Pryce, *Growing Object-Oriented Software, Guided by Tests*; Khorikov, *Unit Testing Principles, Practices, and Patterns* |
| Resilience and operations | Nygard, *Release It!*; The Twelve-Factor App |
| Security | OWASP API Security Top 10; OWASP ASVS |
