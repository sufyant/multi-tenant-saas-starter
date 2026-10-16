# API catalog

The endpoints of the template's core API when it is complete. Each row says who may call it, the audit entry it writes, the notification it sends, the external system it reaches, and the step that builds it. Code follows this catalog. A change to an endpoint changes this file in the same pull request.

Status: **has** (exists, unchanged), **changes** (exists, changes), **new**.
Audit: **T** = the tenant's audit log, **P** = the platform audit log.

## Me: `/v1/me`

The signed-in user, acting for themselves. No permission; the user reaches only their own data (N6).

- `GetMe` has no status. A suspended user gets 403 `user.suspended` on every `me` endpoint (A9), and a deleted user cannot sign in, so the status would always be "active".
- A person becomes a catalog user only through an invitation or as the first system admin. `GetMe` answers a signed-in person the catalog does not know with 404 `user.not_found`. The web shows "no access yet".

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| new | GET | `/v1/me` | `GetMe` (catalog id, profile copy, system admin or not; no status; a person the catalog does not know gets 404 `user.not_found`) | signed in | none | none | Clerk, through the webhook's copy | 20c |
| changes | GET | `/v1/me/tenants` | `ListMyTenants` (list contract; `q`: name, slug; no filter; `sort`: `name`) | signed in | none | none | none | 20c |
| changes | POST | `/v1/me/invitations/accept` | `AcceptInvitation` (writes an audit entry) | signed in; the email must match | T `member.joined` | none | Clerk: verified emails | 20b |

## Tenant: `/v1/tenants/{tenantId}`

A member of the tenant. Each endpoint names a permission. To a non-member the tenant does not exist (T3).

- `ListMembers` keeps a member whose user was deleted, with an empty email address and empty names. The screen shows "Deleted user", as for audit (K3). What happens to a deleted Owner is a later decision.

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| new | GET | `/v1/tenants/{tenantId}` | `GetMyTenant` (answers `MyTenantResponse`) | `tenant.profile.read`; every role | none | none | none | 20c |
| new | PATCH | `/v1/tenants/{tenantId}` | `RenameTenant` (name only; the slug never changes) | `tenant.profile.update`; Owner, Admin | T `tenant.renamed` | none | none | 20h |
| changes | GET | `/v1/tenants/{tenantId}/members` | `ListMembers` (adds email and names; a deleted user stays, with an empty email and names; list contract; `q`: email, first name, last name; filter `role`; `sort`: `role`, `email`, `lastName`) | `tenant.members.read`; every role | none | none | Clerk, through the copy | 20c |
| new | PATCH | `/v1/tenants/{tenantId}/members/{userId}` | `ChangeMemberRole` (body `{ "role" }`, a `MemberRole`; answers 200 `MemberResponse`; no Idempotency-Key, it sets a state; locks the tenant row, then checks: an actor who is no longer a member (404 `tenant.not_found`), a member the tenant has not (404 `membership.not_found`), the Owner role from an Admin or an Admin on an Owner (403 `membership.owner_requires_owner`), the last Owner (409 `membership.last_owner`); the role the member has answers 200 and writes nothing) | `tenant.members.update`; Owner, Admin; the Owner role only from an Owner (K1) | T `member.role_changed` (details: the old and the new role) | none | none | 20e |
| new | DELETE | `/v1/tenants/{tenantId}/members/{userId}` | `RemoveMember` (204; no Idempotency-Key, it sets a state; locks the tenant row, then checks: an actor who is no longer a member (404 `tenant.not_found`), a member the tenant has not (404 `membership.not_found`), yourself (403 `membership.cannot_remove_self`), an Admin on an Owner (403 `membership.owner_requires_owner`), the last Owner (409 `membership.last_owner`); the member loses access at once (T7)) | `tenant.members.delete`; Owner, Admin; only an Owner removes an Owner; the last Owner cannot be removed (K1) | T `member.removed` (details: the role the member had) | none | none | 20e |
| new | POST | `/v1/tenants/{tenantId}/invitations` | `InviteMember` (Idempotency-Key; answers 201 `InvitationResponse`; locks the tenant row, then checks an inviter who is no longer a member (404 `tenant.not_found`), the role rule (403 `invitation.owner_requires_owner`), a member's address (409 `membership.exists`) and the daily limit (429 `invitation.daily_limit_reached`); replaces a pending invitation to the same email (K2)) | `tenant.invitations.create`; Owner, Admin; only an Owner invites an Owner | T `member.invited` | invitation email | Resend, through Notifications | 20d |
| new | GET | `/v1/tenants/{tenantId}/invitations` | `ListInvitations` (pending invitations, expired ones included; list contract; `q`: email; filter `role`; `sort`: `expiresAt`, `email`) | `tenant.invitations.read`; Owner, Admin | none | none | none | 20d |
| new | DELETE | `/v1/tenants/{tenantId}/invitations/{invitationId}` | `RevokeInvitation` (204; locks the tenant row; a revoker who is no longer a member answers 404 `tenant.not_found`; the link stops working at once; a cancelled invitation answers 204 again and writes nothing; an accepted one answers 409 `invitation.not_pending`; an id the tenant has not, 404 `invitation.not_found`) | `tenant.invitations.delete`; Owner, Admin | T `invitation.revoked` | none | none | 20d |
| new | GET | `/v1/tenants/{tenantId}/audit-log` | `ListAuditEntries` (cursor contract of K5; newest first; real names resolved on read, else null, and `deleted` for a user; no label and no email address; the screen calls it "Activity") | `tenant.audit.read`; Owner, Admin | none | none | none | 20f |

## Admin: `/v1/admin`

A system admin with a second factor (A6). Every action writes to the platform audit log.

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| changes | POST | `/v1/admin/tenants` | `CreateTenant` (saga; Idempotency-Key; adds the platform entry) | `admin.tenants.create` | P `tenant.created`, T `tenant.created` | owner invitation email | Resend | 20b |
| changes | GET | `/v1/admin/tenants` | `ListTenants` (list contract; `q`: name, slug; filter `status`; `sort`: `name`, `slug`) | `admin.tenants.read` | none | none | none | 20c |
| new | GET | `/v1/admin/tenants/{tenantId}` | `GetTenant` (answers `TenantResponse`; 404 `tenant.not_found` for an id no tenant has) | `admin.tenants.read` | none | none | none | 20c |
| changes | POST | `/v1/admin/tenants/{tenantId}/suspend` | `SuspendTenant` (platform entry in 20b; reason in 20h) | `admin.tenants.suspend` | P `tenant.suspended`, T `tenant.suspended` | none | none | 20b, 20h |
| changes | POST | `/v1/admin/tenants/{tenantId}/reactivate` | `ReactivateTenant` (adds the platform entry) | `admin.tenants.reactivate` | P `tenant.reactivated`, T `tenant.reactivated` | none | none | 20b |
| new | POST | `/v1/admin/tenants/{tenantId}/close` | `CloseTenant` (members cannot enter; data stays; K4) | `admin.tenants.close` | P `tenant.closed`, T `tenant.closed` | none | none | 20h |
| new | POST | `/v1/admin/tenants/{tenantId}/reopen` | `ReopenTenant` (only before the period ends) | `admin.tenants.reopen` | P `tenant.reopened`, T `tenant.reopened` | none | none | 20h |
| changes | GET | `/v1/admin/users` | `ListUsers` (list contract; `q`: email, first name, last name; filter `status`; `sort`: `email`, `lastName`) | `admin.users.read` | none | none | Clerk, through the copy | 20c |
| changes | POST | `/v1/admin/users/{userId}/suspend` | `SuspendUser` (adds the platform entry) | `admin.users.suspend` | P `user.suspended` | none | none; Clerk is not told, the API refuses the user on each request (A9) | 20b |
| changes | POST | `/v1/admin/users/{userId}/reactivate` | `ReactivateUser` (adds the platform entry) | `admin.users.reactivate` | P `user.reactivated` | none | none | 20b |
| new | GET | `/v1/admin/audit-log` | `ListPlatformAuditEntries` (cursor contract of K5; newest first; real names resolved on read, else null, and `deleted` for a user; no label and no email address) | `admin.audit.read` | none | none | none | 20f |

## Webhooks: `/v1/webhooks`

An external system, proven by its signature. Not in the OpenAPI document (N5).

| Status | Method | Path | operationId | Access | Audit | Notification | External | Step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| changes | POST | `/v1/webhooks/clerk` | `ReceiveClerkEvent` (`user.updated` → `UpdateUserProfile`; `user.deleted` → `DeleteUser`; adds platform entries, actor "Clerk") | Svix signature, 1 MB | P `user.profile_updated`, P `user.deleted` | none | Clerk | 20b |

## How audit and notifications connect

ControlPlane changes its data and writes an event to the outbox in the same transaction. Audit and Notifications take the event from their own queues. An endpoint never writes an email or an audit entry itself. When one event gives an entry in both logs, Audit writes both in one transaction.

- `SuspendTenant` → ControlPlane (state and event) → `TenantSuspended` → Audit: platform log "Sufyan suspended Acme", and Acme's log "The platform suspended this tenant".
- `InviteMember` → ControlPlane (invitation and event) → `MemberInvited` → Audit: tenant log; Notifications → its own command `SendMemberInvitationEmail` → Resend: the invitation email. The event has two handlers, so a scheduled retry of it could run in a web host (W5); the command has one, and it has the retries of the owner invitation email.

## Decided rules

### K1. Roles

- An Owner and an Admin invite members, change roles and remove members.
- Only an Owner gives or takes the Owner role. Only an Owner removes an Owner.
- A tenant always has at least one Owner. The last Owner cannot be removed or lose the role.
- A member cannot leave a tenant by themselves. An Owner or an Admin removes them. A member cannot remove themselves.
- A member may change their own role under the same rules. An Owner may step down when another Owner exists. An Admin may become a Member. An Admin cannot become an Owner.
- The Owner count includes every Owner membership, also one whose user the identity provider deleted. So a tenant whose other Owners are all deleted users still lets its last active Owner step down or be removed, and can be left with only deleted Owners. What happens to a deleted Owner is a later decision.
- Setting the role a member already has answers 200 and publishes nothing, so it writes no audit entry (I1).
- A removed member loses access at once: the tenant door reads the membership on every request (T7). No cache is added.
- These rules live in Domain, on the tenant (`Tenant.ChangeMemberRole`, `Tenant.CheckRemoval`), because "at least one Owner" is the tenant's rule (M1). A permission says who passes the door; a Domain rule says what can happen to an Owner.
- Every command that changes the tenant, a membership or an invitation locks the tenant row first (K6).

### K2. Invitations

- A new invitation to an email with a pending invitation in the same tenant replaces it. The old link stops working at once. The emails are compared without case.
- A replaced or a revoked invitation becomes `Cancelled`. Accepting it answers 404 `invitation.not_found`. There is no other status.
- A replacement is one command and one audit record: `member.invited`, with the replaced invitation's id in its details. It writes no `invitation.revoked` record (K3).
- "Send again" is not an endpoint of its own.
- An invitation lives `ControlPlane:Invitations:Lifetime`, 7 days today.
- A tenant has at most one pending invitation to an email. A partial unique index on the tenant and `lower(email)` of pending invitations keeps it. The tenant row lock is the first guard; the index is the second (K6).
- Each tenant may create `ControlPlane:Invitations:DailyLimitPerTenant` invitations in any 24 hours, 50 by default. The window rolls: it counts every invitation the tenant created in the last 24 hours, cancelled and replaced ones included, so a replacement does not get around it. Over the limit, `InviteMember` answers 429 `invitation.daily_limit_reached` (RFC 6585), without a `Retry-After` header. It may become a platform setting later.
- An email that already belongs to a member, in the catalog's copy of the profile and compared without case, answers 409 `membership.exists`.
- The invitation email has the retries of the owner invitation email. After the last try the message goes to the dead letter queue. The invitation stays pending; the inviter can invite again, which replaces it.
- Closing a tenant cancels its pending invitations. An invitation to a suspended tenant cannot be accepted (409).

### K3. Audit

- Every command that changes state writes one audit record in each log it belongs to. An architecture test enforces it. Reads write nothing.
- An audit entry stores no personal name or email address. It stores the actor's catalog user id, the target's kind and id, and the target's name only when the target is not a person. A tenant's name stays in the entry.
- A `member.invited` entry stores the invitation id, not the email address.
- The read side resolves names in one batch through an interface in `ControlPlane.Contracts` (API composition). Audit never reads `control_plane` tables directly.
- When a user is deleted, `DeleteUser` erases the catalog's copy of their profile, so the read side gives the user no name and marks them deleted, and the screen shows "Deleted user". No audit entry changes, and the entry stays (GDPR Article 17).
- Every action of a system admin goes to the platform log. When it changes a tenant, it also goes to that tenant's log, with "Platform" in place of the person. In the tenant log, a system admin's action has the actor kind "platform" and no actor id.
- A change that comes from Clerk goes to the platform log, with "Clerk" as the actor.
- The platform log is a table of its own. The row level security of the tenant table does not change.
- The code, the table and the endpoint say `audit`. The screen label is "Activity".
- Sources: GDPR Articles 4(5) and 25 (pseudonymisation). Richardson, *Microservices Patterns*, "API composition".

### K4. Tenant lifecycle

- provisioning → active ⇄ suspended → closed → deleted. A failed creation goes provisioning → cancelled.
- A suspension has a reason. Today it is text the system admin writes. A billing reason comes with billing.
- closed: members cannot enter, the data stays, and the tenant can be reopened within a configured period, 30 days by default.
- The slug never changes.
- deleted (removing the data) is not in this catalog. Export and deletion are designed in a later step.
- The billing state is not copied into the tenant's state. The tenant only reacts to a subscription event.

### K5. Contract

- An endpoint that creates a resource takes `Idempotency-Key`. One that sets a state does not (I1).
- Error codes are an enum in the OpenAPI document. No client writes them by hand.
- 401 and 429 are declared on every operation.
- A second shape of a resource carries its audience in the name: admin `GetTenant`, member `GetMyTenant`.
- Every list uses one contract: `page`, `pageSize` (50, at most 100), `sort`, `q`, and allowed filters. The answer is `{ items, page, pageSize, total }`. A test enforces it (`ListContractTests`).
- `page` starts at 1. `pageSize` is 1 to 100, 50 when left out.
- `sort` is one field from the list's allowed fields. A leading `-` sorts it descending: `sort=-name`. Left out or empty, the list's default applies, ascending. We allow one field, not a comma list. Every sort ends with the id as the last key, so two items with the same value never change places between two pages. A field that an item can leave empty (`email`, `lastName`) puts those items last in both directions. Source: JSON:API 1.1, "Sorting".
- `q` is a case-insensitive "contains" match on the list's search fields. `%`, `_` and `\` in it match themselves. It is trimmed. An empty `q` is no filter. It is at most 100 characters.
- A filter takes one value: the value of the same field in the response. An empty filter value is no filter, as an empty `sort` or `q` is.
- An unknown sort field answers 400 `paging.sort_invalid`, an unknown filter value 400 `paging.filter_invalid`, and a `q` over 100 characters 400 `paging.q_invalid`. They join the `paging.*` codes.
- `sort` and each filter are enums in the OpenAPI document. `SharedKernel` reads and checks `page`, `pageSize`, `sort` and `q` in one place (`ListRequest`, `ListSort`, `ListFilter`). Minimal API binds an enum by its C# name and ignores the JSON name the document shows, so the sort and filter parameters bind any text and `ListRequest` checks it. A test sends each value the document lists (`ListParameterBindingTests`).
- The audit logs are the exception: a cursor, and no total. `ListAuditEntries` and `ListPlatformAuditEntries` take `cursor` (opaque, optional) and `limit` (1 to 100, 50 when left out), and nothing else. The order is newest first, by `occurred_at`, then `id`, both descending; the cursor holds the last item's pair. The answer is `{ items, nextCursor }`, and `nextCursor` is null on the last page. A cursor that does not decode, or was changed, answers 400 `paging.cursor_invalid`; a limit out of range, 400 `paging.limit_invalid`. A test allows the cursor contract for these two operations only (`ListContractTests`).
- An audit log's order has a B-tree index: `(tenant_id, occurred_at, id)` on `audit.entries`, `(occurred_at, id)` on `audit.platform_entries`.
- Every field that is searched or sorted has an index. A searched field has a trigram GIN index (PostgreSQL `pg_trgm`), because a B-tree index cannot serve a "contains" match. A sorted field has a B-tree index. Source: PostgreSQL documentation, "pg_trgm", "Index Support".

| List | `q` matches | Filters | Sort fields (default first) | Indexes |
| --- | --- | --- | --- | --- |
| `ListMyTenants` | name, slug | none | `name` | tenants: trigram on name and slug; B-tree on name |
| `ListTenants` | name, slug | `status` | `name`, `slug` | tenants: trigram on name and slug; B-tree on name; the unique slug index |
| `ListUsers` | email, first name, last name | `status` | `email`, `lastName` | users: trigram on email, first name and last name; B-tree on email and last name |
| `ListMembers` | email, first name, last name | `role` | `role` (Owner, Admin, Member), `email`, `lastName` | the users indexes; memberships: the primary key and the B-tree on role id |
| `ListInvitations` | email | `role` | `expiresAt`, `email` | invitations: trigram on email; B-tree on email and expires_at; the partial unique index on tenant and `lower(email)` of pending invitations |

- The member search starts from the active tenant's memberships, which row level security keeps to the tenant, and joins each member's user. It never starts from the users table, which is not under row level security (R6).

### K6. Concurrency

- Every handler that changes data, a saga step included, first locks the root row of the aggregate it changes (`SELECT … FOR UPDATE`). Updating a tenant locks that tenant's row; updating an invoice locks that invoice's row, never the tenant's.
- In ControlPlane, memberships and invitations belong to the tenant aggregate, and "at least one Owner" is the tenant's rule. So a change to them locks the tenant row.
- A rule that only keeps a value unique uses a database constraint (a unique index), not a lock.
- Locks are taken in one order: the aggregate root first. A lock lives only inside one transaction, never across two HTTP requests.
- Sources: Kleppmann, *Designing Data-Intensive Applications*, chapter 7, "Write Skew and Phantoms". Fowler, *Patterns of Enterprise Application Architecture*, "Coarse-Grained Lock". Evans, *Domain-Driven Design*, chapter 6, "Aggregates". Vernon, "Effective Aggregate Design". PostgreSQL documentation, "Explicit Locking".

### K7. Permission names

- The new permission names are approved: `tenant.profile.*`, `tenant.invitations.*`, `tenant.members.update` and `tenant.members.delete`, `tenant.audit.read`, `admin.tenants.close` and `admin.tenants.reopen`, and `admin.audit.read` (N6).
- Each step adds to the code only the permissions it uses. Step 20c adds `tenant.profile.read`, for every built-in role. Step 20d adds `tenant.invitations.create`, `tenant.invitations.read` and `tenant.invitations.delete`, for the Owner and Admin roles only. Step 20e adds `tenant.members.update` and `tenant.members.delete`, for the Owner and Admin roles only. Step 20f adds `tenant.audit.read`, for the Owner and Admin roles only, and `admin.audit.read`, for system admins.

## Later

- What happens when Clerk deletes a tenant's last Owner. Until then, a deleted user's Owner membership counts as an Owner (K1).
- A lost update across two HTTP requests (two admins editing the same form): `ETag` and `If-Match` (RFC 9110).
- Search and filters beyond `q` and the allowed filters. Data export and deletion. API keys for tenants. Outgoing webhooks. Billing.

## Steps

Each step is one prompt and one pull request. They run in order, because each touches the permission list, N6 and `openapi/v1.json`.

| Step | Content |
| --- | --- |
| 20b | This catalog in the repository. The audit foundation: records with ids and no personal name or email address, the platform log table, the rule "every command writes audit" and its test, and names resolved on read, so a deleted user reads as "Deleted user" with no change to a record. Existing commands follow the rule. K6 is written into ARCHITECTURE.md. |
| 20c | Reads and the list contract: `GetMe`, `GetMyTenant`, admin `GetTenant`, email and names in `ListMembers`. Every list moves to the one contract. |
| 20d | Invitations: `InviteMember`, `ListInvitations`, `RevokeInvitation`. The daily limit and one pending invitation per email. |
| 20e | Member management: `ChangeMemberRole`, `RemoveMember`, the Owner rules of K1, the tenant row lock and a race test with two requests at once. |
| 20f | Reading the audit logs: the tenant's and the platform's, with a cursor. `audit.entries.actor_id` becomes uuid, as in the platform log. |
| 20g | The rest of the contract: the error code enum, 401 and 429. No endpoint. |
| 20h | Tenant lifecycle: `RenameTenant`, a reason for suspension, `CloseTenant`, `ReopenTenant`, the closed state. Closing cancels pending invitations. |
