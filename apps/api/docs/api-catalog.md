# API catalog

The endpoints of the template's core API when it is complete. Each row says who may call it, the audit entry it writes, the notification it sends, the external system it reaches, and the step that builds it. Code follows this catalog. A change to an endpoint changes this file in the same pull request.

Status: **has** (exists, unchanged), **changes** (exists, changes), **new**.
Audit: **T** = the tenant's audit log, **P** = the platform audit log.

## Me: `/v1/me`

The signed-in user, acting for themselves. No permission; the user reaches only their own data (N6).

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| new | GET | `/v1/me` | `GetMe` (profile copy, status, system admin or not) | signed in | none | none | Clerk, through the webhook's copy | 20c |
| changes | GET | `/v1/me/tenants` | `ListMyTenants` (list contract; `q`: name, slug) | signed in | none | none | none | 20c |
| changes | POST | `/v1/me/invitations/accept` | `AcceptInvitation` (writes an audit entry) | signed in; the email must match | T `member.joined` | none | Clerk: verified emails | 20b |

## Tenant: `/v1/tenants/{tenantId}`

A member of the tenant. Each endpoint names a permission. To a non-member the tenant does not exist (T3).

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| new | GET | `/v1/tenants/{tenantId}` | `GetMyTenant` (answers `MyTenantResponse`) | `tenant.profile.read`; every role | none | none | none | 20c |
| new | PATCH | `/v1/tenants/{tenantId}` | `RenameTenant` (name only; the slug never changes) | `tenant.profile.update`; Owner, Admin | T `tenant.renamed` | none | none | 20h |
| changes | GET | `/v1/tenants/{tenantId}/members` | `ListMembers` (adds email and names; list contract; `q`: email, name; filter `role`) | `tenant.members.read`; every role | none | none | Clerk, through the copy | 20c |
| new | PATCH | `/v1/tenants/{tenantId}/members/{userId}` | `ChangeMemberRole` | `tenant.members.update`; Owner, Admin; the Owner role only from an Owner (K1) | T `member.role_changed` | none | none | 20e |
| new | DELETE | `/v1/tenants/{tenantId}/members/{userId}` | `RemoveMember` | `tenant.members.delete`; Owner, Admin; the last Owner cannot be removed (K1) | T `member.removed` | none | none | 20e |
| new | POST | `/v1/tenants/{tenantId}/invitations` | `InviteMember` (Idempotency-Key; replaces a pending invitation to the same email; daily limit) | `tenant.invitations.create`; Owner, Admin; only an Owner invites an Owner | T `member.invited` | invitation email | Resend, through Notifications | 20d |
| new | GET | `/v1/tenants/{tenantId}/invitations` | `ListInvitations` (pending invitations) | `tenant.invitations.read`; Owner, Admin | none | none | none | 20d |
| new | DELETE | `/v1/tenants/{tenantId}/invitations/{invitationId}` | `RevokeInvitation` (the link stops working at once) | `tenant.invitations.delete`; Owner, Admin | T `invitation.revoked` | none | none | 20d |
| new | GET | `/v1/tenants/{tenantId}/audit-log` | `ListAuditEntries` (cursor; the screen calls it "Activity") | `tenant.audit.read`; Owner, Admin | none | none | none | 20f |

## Admin: `/v1/admin`

A system admin with a second factor (A6). Every action writes to the platform audit log.

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| changes | POST | `/v1/admin/tenants` | `CreateTenant` (saga; Idempotency-Key; adds the platform entry) | `admin.tenants.create` | P `tenant.created`, T `tenant.created` | owner invitation email | Resend | 20b |
| changes | GET | `/v1/admin/tenants` | `ListTenants` (list contract; `q`: name, slug; filter `status`) | `admin.tenants.read` | none | none | none | 20c |
| new | GET | `/v1/admin/tenants/{tenantId}` | `GetTenant` (answers `TenantResponse`) | `admin.tenants.read` | none | none | none | 20c |
| changes | POST | `/v1/admin/tenants/{tenantId}/suspend` | `SuspendTenant` (platform entry in 20b; reason in 20h) | `admin.tenants.suspend` | P `tenant.suspended`, T `tenant.suspended` | none | none | 20b, 20h |
| changes | POST | `/v1/admin/tenants/{tenantId}/reactivate` | `ReactivateTenant` (adds the platform entry) | `admin.tenants.reactivate` | P `tenant.reactivated`, T `tenant.reactivated` | none | none | 20b |
| new | POST | `/v1/admin/tenants/{tenantId}/close` | `CloseTenant` (members cannot enter; data stays; K4) | `admin.tenants.close` | P `tenant.closed`, T `tenant.closed` | none | none | 20h |
| new | POST | `/v1/admin/tenants/{tenantId}/reopen` | `ReopenTenant` (only before the period ends) | `admin.tenants.reopen` | P `tenant.reopened`, T `tenant.reopened` | none | none | 20h |
| changes | GET | `/v1/admin/users` | `ListUsers` (list contract; `q`: email, name; filter `status`) | `admin.users.read` | none | none | Clerk, through the copy | 20c |
| changes | POST | `/v1/admin/users/{userId}/suspend` | `SuspendUser` (adds the platform entry) | `admin.users.suspend` | P `user.suspended` | none | none; Clerk is not told, the API refuses the user on each request (A9) | 20b |
| changes | POST | `/v1/admin/users/{userId}/reactivate` | `ReactivateUser` (adds the platform entry) | `admin.users.reactivate` | P `user.reactivated` | none | none | 20b |
| new | GET | `/v1/admin/audit-log` | `ListPlatformAuditEntries` (cursor) | `admin.audit.read` | none | none | none | 20f |

## Webhooks: `/v1/webhooks`

An external system, proven by its signature. Not in the OpenAPI document (N5).

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| changes | POST | `/v1/webhooks/clerk` | `ReceiveClerkEvent` (`user.updated` → `UpdateUserProfile`; `user.deleted` → `DeleteUser`; adds platform entries, actor "Clerk") | Svix signature, 1 MB | P `user.profile_updated`, P `user.deleted` | none | Clerk | 20b |

## How audit and notifications connect

ControlPlane changes its data and writes an event to the outbox in the same transaction. Audit and Notifications take the event from their own queues. An endpoint never writes an email or an audit entry itself. When one event gives an entry in both logs, Audit writes both in one transaction.

- `SuspendTenant` → ControlPlane (state and event) → `TenantSuspended` → Audit: platform log "Sufyan suspended Acme", and Acme's log "The platform suspended this tenant".
- `InviteMember` → ControlPlane (invitation and event) → `MemberInvited` → Audit: tenant log; Notifications → Resend: the invitation email.

## Decided rules

### K1. Roles

- An Owner and an Admin invite members, change roles and remove members.
- Only an Owner gives or takes the Owner role. Only an Owner removes an Owner.
- A tenant always has at least one Owner. The last Owner cannot be removed or lose the role.
- A member cannot leave a tenant by themselves. An Owner or an Admin removes them.
- These rules live in Domain. A permission says who passes the door; a Domain rule says what can happen to an Owner.
- Every command that changes the tenant, a membership or an invitation locks the tenant row first (K6).

### K2. Invitations

- A new invitation to an email with a pending invitation in the same tenant replaces it. The old link stops working at once.
- "Send again" is not an endpoint of its own.
- An invitation lives `ControlPlane:Invitations:Lifetime`, 7 days today.
- A tenant has at most one pending invitation to an email. A unique index keeps it.
- Each tenant may send `ControlPlane:Invitations:DailyLimitPerTenant` invitations a day, 50 by default. It may become a platform setting later.
- Closing a tenant cancels its pending invitations. An invitation to a suspended tenant cannot be accepted (409).

### K3. Audit

- Every command that changes state writes one audit entry. An architecture test enforces it. Reads write nothing.
- An entry keeps the names of the actor and the target as they were at that moment.
- When a user is deleted, their name and email in audit entries become "Deleted user". The entry stays (GDPR Article 17).
- Every action of a system admin goes to the platform log. When it changes a tenant, it also goes to that tenant's log, with "Platform" in place of the person.
- A change that comes from Clerk goes to the platform log, with "Clerk" as the actor.
- The platform log is a table of its own. The row level security of the tenant table does not change.
- The code, the table and the endpoint say `audit`. The screen label is "Activity".

### K4. Tenant lifecycle

- provisioning → active ⇄ suspended → closed → deleted. A failed creation goes provisioning → cancelled.
- A suspension has a reason. Today it is text the system admin writes. A billing reason comes with billing.
- closed: members cannot enter, the data stays, and the tenant can be reopened within a configured period (proposed: 30 days).
- deleted (removing the data) is not in this catalog. Export and deletion are designed in a later step.
- The billing state is not copied into the tenant's state. The tenant only reacts to a subscription event.

### K5. Contract

- An endpoint that creates a resource takes `Idempotency-Key`. One that sets a state does not (I1).
- Error codes are an enum in the OpenAPI document. No client writes them by hand.
- 401 and 429 are declared on every operation.
- A second shape of a resource carries its audience in the name: admin `GetTenant`, member `GetMyTenant`.
- Every list uses one contract: `page`, `pageSize` (50, at most 100), `sort`, `q`, and allowed filters. The answer is `{ items, page, pageSize, total }`. A test enforces it.
- The audit logs are the exception: a cursor, and no total.
- Every field that is searched or sorted has an index.

### K6. Concurrency

- A command that changes data first locks the root row of the aggregate it changes (`SELECT … FOR UPDATE`). Updating a tenant locks that tenant's row; updating an invoice locks that invoice's row, never the tenant's.
- In ControlPlane, memberships and invitations belong to the tenant aggregate, and "at least one Owner" is the tenant's rule. So a change to them locks the tenant row.
- A rule that only keeps a value unique uses a database constraint (a unique index), not a lock.
- Locks are taken in one order: the aggregate root first. A lock lives only inside one transaction, never across two HTTP requests.
- Sources: Kleppmann, *Designing Data-Intensive Applications*, chapter 7, "Write Skew and Phantoms". Fowler, *Patterns of Enterprise Application Architecture*, "Coarse-Grained Lock". Evans, *Domain-Driven Design*, chapter 6, "Aggregates". Vernon, "Effective Aggregate Design". PostgreSQL documentation, "Explicit Locking".

## Proposals waiting for approval

- New permission names: `tenant.profile.*`, `tenant.invitations.*`, `tenant.members.update|delete`, `tenant.audit.read`, `admin.tenants.close|reopen`, `admin.audit.read`.
- The slug never changes.
- A closed tenant can be reopened for 30 days, a setting.

## Later

- What happens when Clerk deletes a tenant's last Owner.
- A lost update across two HTTP requests (two admins editing the same form): `ETag` and `If-Match` (RFC 9110).
- Search and filters beyond `q` and the allowed filters. Data export and deletion. API keys for tenants. Outgoing webhooks. Billing.

## Steps

Each step is one prompt and one pull request. They run in order, because each touches the permission list, N6 and `openapi/v1.json`.

| Step | Content |
| --- | --- |
| 20b | This catalog in the repository. The audit foundation: names kept at the moment, the platform log table, the rule "every command writes audit" and its test, anonymization on user deletion. Existing commands follow the rule. K6 is written into ARCHITECTURE.md. |
| 20c | Reads and the list contract: `GetMe`, `GetMyTenant`, admin `GetTenant`, email and names in `ListMembers`. Every list moves to the one contract. |
| 20d | Invitations: `InviteMember`, `ListInvitations`, `RevokeInvitation`. The daily limit and one pending invitation per email. |
| 20e | Member management: `ChangeMemberRole`, `RemoveMember`, the Owner rules of K1, the tenant row lock and a race test with two requests at once. |
| 20f | Reading the audit logs: the tenant's and the platform's, with a cursor. |
| 20g | The rest of the contract: the error code enum, 401 and 429. No endpoint. |
| 20h | Tenant lifecycle: `RenameTenant`, a reason for suspension, `CloseTenant`, `ReopenTenant`, the closed state. Closing cancels pending invitations. |
