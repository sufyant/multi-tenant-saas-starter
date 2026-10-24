# Invitations

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

The three flows of an invitation: the identity provider's sign-up that every invitation takes, a member's invitation, and the platform's invitation of an Owner.

## 1. Invitation sign-up

The identity provider is invite-only: only its own invitation lets a person without an account sign up. Our invitation stays ours: its tenant, role, lifetime, audit and email. The identity provider's invitation only lets the invited address sign up (A7). This is not a saga: it follows the invitation through events and retried handlers (S1).

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `OpenSignUpCommand` | `CreateTenantHandler`, `InviteMemberHandler`, `InviteOwnerHandler` | ControlPlane, `OpenSignUpHandler` (calls the identity provider) | No |
| `RecordSignUpCommand` | `OpenSignUpHandler` | ControlPlane, `RecordSignUpHandler` | Yes |
| `InvitationReadyEvent` | `RecordSignUpHandler` | Notifications, `SendInvitationEmailHandler` (calls the email service) | No |
| `ExpireSignUpCommand` | `RecordSignUpHandler`, scheduled at the invitation's expiry | ControlPlane, `ExpireSignUpHandler` | Yes |
| `WithdrawSignUpCommand` | `RecordSignUpHandler`, `ExpireSignUpHandler`, `InviteMemberHandler` and `InviteOwnerHandler` (a replaced invitation), `RevokeInvitationHandler`, `CloseTenantHandler` | ControlPlane, `WithdrawSignUpHandler` (calls the identity provider) | No |

- The invitation and its event commit first, with the command that opens the sign-up, through the outbox. No call to the identity provider runs inside a transaction (`ARCHITECTURE.md`, section 3).
- Every invitation takes one pair of messages: a tenant's first Owner, an Owner the platform invites and a person a member invites. `OpenSignUpCommand` (`open-sign-up`) and `InvitationReadyEvent` (`invitation-ready`) replaced the owner's and the member's pairs in step r3; nothing was in production, so the old names are not kept (W12).
- `OpenSignUpHandler` asks the identity provider to let the address sign up until our invitation's expiry, with our accept link, which carries the invitation code. An address with an account needs no invitation: the answer is the accept link itself. Otherwise the answer is the identity provider's link and its invitation's id.
- `RecordSignUpHandler` locks the tenant row and then the invitation (K6), and decides with `Invitation.RecordSignUp`. A pending invitation without an identity provider invitation keeps the id, and the link goes to Notifications. An answer that arrives twice changes nothing; for an address with an account, which has no identity provider invitation to compare, it publishes the email's event again, and the email's idempotency key (`invite/` and the invitation id) sends one email. An answer for an invitation that was revoked, replaced, cancelled or accepted meanwhile, or for one that already has another identity provider invitation, sends no email, and its identity provider invitation is withdrawn. So no email carries a link to an invitation that ended, and nobody can sign up with a link nobody received.
- With the link, `RecordSignUpHandler` schedules `ExpireSignUpCommand` at the invitation's expiry. The identity provider counts an invitation's life in whole days and rounds ours up, so this withdraws its invitation when ours ends. The module's clock decides: a message that arrives before the expiry withdraws nothing. An invitation that ended before withdrew it then; an accepted one needs nothing.
- Revoking, replacing and cancelling an invitation, and closing its tenant, withdraw its identity provider invitation through `WithdrawSignUpCommand`, after the commit. Accepting withdraws nothing.
- The calls have the retries of `ControlPlane:IdentityProvider:RetryDelays`, as the invitation emails have theirs (W11). Each of these messages has one handler, so a retry may be scheduled (W5). After the last try the message goes to the dead letter queue: no email goes out, and the invitation stays pending. The inviter can invite again, which replaces it (K2); for a tenant's Owner the platform does (K9).
- The identity provider redirects to our accept link and adds `__clerk_ticket`. The web signs the person up with the ticket, which verifies the invited address, and then accepts with the code (step 21).
- Each new message type comes with all its handlers in the same release, so the gap of W12 does not apply.

Sources: Microsoft, Azure Architecture Center, "Federated Identity pattern". Richardson, Chapter 3 (transactional outbox). Clerk, "createInvitation" and "Sign-up with application invitations".

## 2. Member invitation

An Owner or an Admin invites someone to the tenant, lists the pending invitations and revokes one (api-catalog, K1 and K2). This is not a saga: ControlPlane changes the tenant aggregate in one transaction, and the identity provider's sign-up and the email follow it (S1; section 1, "Invitation sign-up").

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `InviteMemberCommand` | `POST /v1/tenants/{tenantId}/invitations` | ControlPlane, `InviteMemberHandler` | Yes |
| `MemberInvitedEvent` | `InviteMemberHandler` | Audit, `RecordMemberInvitedHandler` (`member.invited`, tenant log) | Yes |
| `OpenSignUpCommand` | `InviteMemberHandler` | ControlPlane, `OpenSignUpHandler` (section 1, "Invitation sign-up") | No |
| `InvitationReadyEvent` | `RecordSignUpHandler` | Notifications, `SendInvitationEmailHandler` (calls the email service) | No |
| `RevokeInvitationCommand` | `DELETE /v1/tenants/{tenantId}/invitations/{invitationId}` | ControlPlane, `RevokeInvitationHandler` | Yes |
| `InvitationRevokedEvent` | `RevokeInvitationHandler`, only for a pending invitation | Audit, `RecordInvitationRevokedHandler` (`invitation.revoked`, tenant log) | Yes |

- `InviteMember` locks the tenant row (K6). It then checks, in this order: an inviter who is no longer a member (404 `tenant.not_found`, T3; a removal that held the lock first took their membership), a tenant that is no longer active (403 `tenant.suspended` or `tenant.closed`; a suspension or a closure that held the lock first changed it), a role id the tenant has not (404 `role.not_found`), only an Owner invites an Owner (403 `invitation.owner_requires_owner`, a Domain rule, `Invitation.CheckInviter`), a role that holds a permission the inviter's role does not (403 `role.escalation`, `custom-roles.md`), an email that belongs to a member (409 `member.exists`), and the daily limit (429 `invitation.daily_limit_reached`). The last four are the tenant's rules (`Tenant.Invite`, M1): the handler reads their facts under the lock (`ITenantRepository.FindInvitationFactsAsync`) and passes the inviter, a member with their role. Every rejection returns before any change (W7).
- The email address is checked with FluentValidation's `EmailAddress`, as a tenant's first owner's is. It then takes its one form; the rule is in `ARCHITECTURE.md`, section 6, "Rules without an id", "The one form of an email address".
- An invitation keeps its address in every state: pending, expired, accepted and cancelled. Only a user's deletion erases it, from a finished invitation (A8; `user-deletion.md`). `invitations.email` is nullable, and `ck_invitations_pending_has_email` (`status <> 'Pending' OR email IS NOT NULL`) allows null only for the stored states `Accepted` and `Cancelled`. "Expired" is no stored state: a pending invitation is expired from its expiry on, read by the clock at the moment of a read (`Invitation.StateAt`), and a CHECK cannot read the clock. So an expired pending invitation keeps its address too. Every response that shows an invitation's address shows a pending one (`InvitationResponse`) or the summary's Owner invitation (`OwnerInvitationResponse`), so no response shows an erased address, and their `email` stays required.
- A pending invitation to the same address, in its one form, becomes `Cancelled` and is saved first. Then the new invitation is added with a new secret. The partial unique index never sees two pending invitations.
- `MemberInvitedEvent` carries only the ids and the role, by its name, its id and whether it is built in, for Audit (K3). `OpenSignUpCommand` carries the address, the tenant's name and the accept link, which leads to the email once the identity provider has answered (section 1, "Invitation sign-up").
- The invitation keeps the language of its email: the request's `language`, else the inviter's own (`language-and-time-zone.md`).
- `InvitationReadyEvent` has one handler, with the retries of the invitation email (`Notifications:InvitationEmails:RetryDelays`). After the last try the event goes to the dead letter queue, and the invitation stays pending. The email's idempotency key is the invitation's (`invite/` and its id), so an event that arrives twice sends one email.
- A replaced invitation's identity provider invitation is withdrawn, and so is a revoked one's (`WithdrawSignUpCommand`).
- `RevokeInvitation` locks the tenant row. A revoker who is no longer a member answers 404 `tenant.not_found` (T3), and a tenant that is no longer active 403 `tenant.suspended` or `tenant.closed`. It then locks the invitation. A pending invitation, expired or not, becomes `Cancelled`. A pending invitation whose role holds a permission the revoker's role does not answers 403 `role.escalation`, and stays pending. A cancelled one answers 204 and publishes nothing (I1). An accepted one answers 409 `invitation.not_pending`. Row level security hides another tenant's invitation, so its id answers 404 `invitation.not_found`.
- Each new message type comes with all its handlers in the same release, so the gap of W12 does not apply.

Source: Golding, Chapter 2 (control plane, tenant management). Hohpe and Woolf, "Command Message" and "Dead Letter Channel".

## 3. Owner invitation

A tenant has no Owner only when its first Owner never accepted, because the last Owner always stays (api-catalog, K1). The tenant is then active, has no member, and its Owner invitation is pending, expired or cancelled. A tenant with members and an Owner nobody can reach takes the path of `owner-recovery.md`. A system admin invites an Owner (api-catalog, K9). This is not a saga: ControlPlane changes the tenant aggregate in one transaction, and the identity provider's sign-up and the email follow it (section 1, "Invitation sign-up").

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `InviteOwnerCommand` | `POST /v1/platform/tenants/{tenantId}/invitations` | ControlPlane, `InviteOwnerHandler` | Yes |
| `OwnerInvitedEvent` | `InviteOwnerHandler` | Audit, `RecordOwnerInvitedHandler` (`tenant.owner_invited`, both logs) | Yes |
| `OpenSignUpCommand` | `InviteOwnerHandler` | ControlPlane, `OpenSignUpHandler` (section 1, "Invitation sign-up") | No |
| `WithdrawSignUpCommand` | `InviteOwnerHandler`, for the replaced invitation | ControlPlane, `WithdrawSignUpHandler` | No |
| `InvitationReadyEvent` | `RecordSignUpHandler` | Notifications, `SendInvitationEmailHandler` | No |

- `InviteOwner` locks the tenant row (K6) and counts the Owners under the lock. It then checks, in this order: an id no tenant has (404 `tenant.not_found`), a tenant that is not active (409 `tenant.not_active`), and an Owner (409 `tenant.has_owner`). The rule is the tenant's (`Tenant.Invite`, M1), the same method a member's invitation calls: the inviter is the platform, which is not held to the daily limit and replaces every pending invitation. Every rejection returns before any change (W7).
- At most one platform Owner invitation is pending. In a tenant without an Owner nobody else invites, so every pending invitation is the platform's, the one `CreateTenant` sent included. Each becomes `Cancelled` and is saved first, also when the new invitation goes to another address; then the new one is added with the role `owner` and a new secret, and the partial unique index never sees two pending invitations. A cancelled invitation's link answers 404 `invitation.not_found` (K2). Its identity provider invitation is withdrawn.
- An acceptance locks the tenant row first too (`tenant-lifecycle.md`). An acceptance that holds the lock first gives the tenant its Owner, and the new invitation is refused with 409 `tenant.has_owner`. A new invitation that holds the lock first cancels the old one, and the acceptance answers 404. So two Owner invitations are never pending, and a cancelled link never joins. Tests: `InviteOwnerTests`, and `InviteOwner_OnAWebAndAWorkerHost_RecordsInBothLogsAndEmailsOnceInTheWorker` for the web and worker layout (`HostRoleTests`).
- The invitation takes the sign-up and the email of every invitation: `OpenSignUpCommand`, then `InvitationReadyEvent`, with their retries (W11). Tenant creation takes the same path, so an email that fails ends the same way after both: the invitation stays pending, and the platform invites again. The email is in the invitation's language: the request's `language`, else the system admin's own (K8).
- It creates a resource, so it takes `Idempotency-Key` and answers 201 with the invitation, `InvitationResponse` with the role `owner` (I1, K5).
- `OwnerInvitedEvent` carries the ids, the tenant's name and the replaced invitation's id, and no address (K3).
- A member never invites into a tenant without an Owner: such a tenant has no member, and the tenant door lets in members only (T7). No rule is needed there.
- Each new message type comes with all its handlers in the same release, so the gap of W12 does not apply.

Source: Golding, Chapter 2 (control plane, tenant management). Kleppmann, Chapter 7, "Write Skew and Phantoms".
