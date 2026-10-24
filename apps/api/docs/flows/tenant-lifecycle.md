# Tenant lifecycle

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

A system admin renames a tenant, suspends it for a reason and reactivates it, closes it and reopens it (api-catalog, K4). The purge that follows a closure is a saga of its own (`tenant-purge.md`). These commands are not a saga: one module changes one row, and closing also cancels the tenant's pending invitations in the same transaction (S1). Each command runs in the tenant's own transaction, with the tenant declared from the platform route (W3), and locks the tenant row (K6). Each sets a state, so none takes an idempotency key (I1).

The moves (K4):

- a new tenant is active from its creation (`CreateTenant`, `tenant-creation.md`)
- active → suspended (`SuspendTenant`), suspended → active (`ReactivateTenant`)
- active → closed and suspended → closed (`CloseTenant`)
- closed → active (`ReopenTenant`), only while `closedAt` plus `ControlPlane:Tenants:ReopenPeriod` is in the future
- closed → purging when the reopen period ends (`StartTenantPurgeCommand`, which the closure scheduled), and purging → purged at the purge's end (`tenant-purge.md`). Purging and purged have no way back

| Stored status | `POST .../suspend` | `POST .../reactivate` | `POST .../close` | `POST .../reopen` | `PATCH` (rename) |
| --- | --- | --- | --- | --- | --- |
| `Active` | Becomes `Suspended`, with the reason | No change | Becomes `Closed` at this moment | No change | Renamed |
| `Suspended` | No change, the first reason stays | Becomes `Active`, the reason is cleared | Becomes `Closed`, the reason stays | 409 `tenant.not_closed` | Renamed |
| `Closed` | 409 `tenant.not_active` | 409 `tenant.not_active` | No change, the first moment stays | Becomes `Active`, `closedAt` and the reason are cleared; after the period 409 `tenant.reopen_period_ended` | 409 `tenant.not_active` |
| `Purging` | 409 `tenant.not_active` | 409 `tenant.not_active` | 409 `tenant.not_active` | 409 `tenant.not_closed` | 409 `tenant.not_active` |
| `Purged` | 409 `tenant.not_active` | 409 `tenant.not_active` | 409 `tenant.not_active` | 409 `tenant.not_closed` | 409 `tenant.not_active` |

Every success answers 200 with the tenant as the system admin sees it (`TenantResponse`). "No change" answers 200 too, and publishes nothing. Renaming to the name the tenant has is no change either.

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `RenameTenantCommand` | `PATCH /v1/platform/tenants/{tenantId}` | ControlPlane, `RenameTenantHandler` | Yes |
| `SuspendTenantCommand` | `POST /v1/platform/tenants/{tenantId}/suspend` | ControlPlane, `SuspendTenantHandler` | Yes |
| `ReactivateTenantCommand` | `POST /v1/platform/tenants/{tenantId}/reactivate` | ControlPlane, `ReactivateTenantHandler` | Yes |
| `CloseTenantCommand` | `POST /v1/platform/tenants/{tenantId}/close` | ControlPlane, `CloseTenantHandler` | Yes |
| `ReopenTenantCommand` | `POST /v1/platform/tenants/{tenantId}/reopen` | ControlPlane, `ReopenTenantHandler` | Yes |
| `TenantRenamedEvent` | `RenameTenantHandler`, on a change of name | Audit, `RecordTenantRenamedHandler` (`tenant.renamed`, both logs) | Yes |
| `TenantSuspendedEvent` | `SuspendTenantHandler`, on a change of status | Audit, `RecordTenantSuspendedHandler` (`tenant.suspended`, both logs) | Yes |
| `TenantReactivatedEvent` | `ReactivateTenantHandler`, on a change of status | Audit, `RecordTenantReactivatedHandler` (`tenant.reactivated`, both logs) | Yes |
| `TenantClosedEvent` | `CloseTenantHandler`, on a change of status | Audit, `RecordTenantClosedHandler` (`tenant.closed`, both logs) | Yes |
| `TenantReopenedEvent` | `ReopenTenantHandler`, on a change of status | Audit, `RecordTenantReopenedHandler` (`tenant.reopened`, both logs) | Yes |

- A request that changes nothing publishes nothing, so Audit records each change once.
- The name is trimmed and checked as at the creation: 1 to 100 characters. A name over 100 characters is a validation problem (400 `request.validation_failed`, `too_long`, N11); an empty one is 400 `tenant.name_invalid`. The slug never changes. Only the platform renames a tenant: the tenant path has no endpoint for the name.
- The suspension's reason is text the system admin writes: required, 1 to 500 characters, trimmed (`SuspendTenantValidator`, field codes `required` and `too_long`). It is stored on the tenant (`suspension_reason`) and shown on the platform side only (`GetTenant`, `ListTenants`). The tenant door answers a member `tenant.suspended`, without it. It holds no personal data: the platform's log keeps it after the tenant's purge (A8). Only the platform's audit entry holds it: the tenant's log is read by the tenant's Owners and Admins (`ListAuditEntries`), so its entry has no details.
- A member who calls a tenant route gets 403 `tenant.suspended` or `tenant.closed`. A non-member still gets 404 (T3): only a member learns that the tenant is suspended or closed. The rule is the tenant's (`Tenant.CheckMemberAccess`); the door asks it on every request (T7), and every tenant command asks it again under the tenant's lock (K6).
- `GET /v1/me/tenants` lists the user's tenants, active, suspended, closed and purging, each with its status.
- Closing stores `closedAt` and cancels every pending invitation of the tenant, expired ones included, in the same transaction. Accepting a cancelled invitation answers 404 `invitation.not_found`, as a revoked one (K2), also after a reopening: a reopening does not bring them back. A closure writes one audit entry in each log, `tenant.closed`, and no `invitation.revoked` entry.
- Accepting an invitation to a suspended tenant answers 409 `tenant.suspended` only for a valid invitation and the right person, as T3. The order is: not found or cancelled (404), a system admin (422 `invitation.system_admin_cannot_join`), already a member (409 `member.exists`), the invitation's own checks (`invitation.not_pending`, `invitation.expired`, `invitation.email_mismatch`), then `tenant.suspended`, then the change. The acceptance locks the tenant row before the invitation (K6), so a suspension or a closure commits before the check or after the change, never between them. A stranger with the link keeps the invitation's own answer, and an expired invitation stays dead after a reactivation. The rules are the tenant's (M1): `Tenant.CheckInvitationFound` refuses a cancelled invitation before the user's lock (K6f), and `Tenant.CheckAcceptance` holds the rest of the order, with the invitation's own checks (`Invitation.CheckAcceptance`). Both are pure, and `Tenant.Accept` asks `CheckAcceptance` again before it uses up the invitation and answers the new membership, so the refusal comes before any change (W7). The handler reads the facts of the person after the user's lock, through both repositories (M2), and checks the names of a person who is not a user yet between the two calls, as a validator would. The invitation of a suspended tenant stays pending and can be accepted after the reactivation.
- Messages already in flight for the tenant still run.
- Reactivation and reopening give the members their access back as before.
- Each new message type comes with all its handlers in the same release, so the gap of W12 does not apply.

Source: Golding, Chapter 2 (control plane, tenant management).
