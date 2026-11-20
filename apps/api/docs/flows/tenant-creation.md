# Tenant creation process

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`. A system admin creates a tenant and invites its first Owner (section 1), or a user creates a tenant of their own while the product has sign-up on (section 2).

## 1. A system admin's tenant

Tenant creation is one transaction, not a saga (S1). Its first write and its pivot are both writes in ControlPlane's own database, so one transaction does both and rolls both back on a failure; the steps after it only retry. So it meets neither half of S2: it needs no compensation of its own, and no timeout. It takes the path of `InviteOwner` (`invitations.md`, section 3, "Owner invitation"), the same rule and the same messages (the maintainer's decision D1 in step r3). The template's reference use of a saga is the tenant purge, which comes in step r7. Source: Richardson, Chapter 4 (compensatable steps, pivot, retryable steps).

1. `CreateTenant` puts the tenant on the plan its request names, else on the default plan (404 `plan.not_found` for an id no plan has; ARCHITECTURE.md, E2), and writes the active tenant, its summary (R12), its Owner, Admin, Member and Billing manager roles (A2) and the Owner invitation in one transaction. The invitation code and its link are created here. It sends the identity provider's sign-up (`OpenSignUpCommand`) and the audit event (`TenantCreatedEvent`) through the outbox, in the same transaction (O1). The host saves the command's result in it too (I4). The answer is 201 with the tenant, whose status is `active`.
2. After the commit, the identity provider lets the Owner's address sign up, then Notifications sends the invitation email with its link (`invitations.md`, section 1, "Invitation sign-up"). Each call is tried a limited number of times, with growing pauses (W11).

The Owner signs up with the identity provider like any invited person, through its invitation. Tenant creation creates no tenant, role or user in the identity provider.

Tenant creation messages (N1):

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `CreateTenantCommand` | `POST /v1/platform/tenants` | ControlPlane, `CreateTenantHandler` | Yes |
| `TenantCreatedEvent` | `CreateTenantHandler` | Audit, `RecordTenantCreatedHandler` (`tenant.created`, both logs) | Yes |
| `OpenSignUpCommand` | `CreateTenantHandler` | ControlPlane, `OpenSignUpHandler` (calls the identity provider) | No |
| `RecordSignUpCommand` | `OpenSignUpHandler` | ControlPlane, `RecordSignUpHandler` (records the identity provider's invitation) | Yes |
| `InvitationReadyEvent` | `RecordSignUpHandler` | Notifications, `SendInvitationEmailHandler` (calls the email service) | No |

- `CreateTenant` adds the tenant with one statement, and its summary row with it; the slug's unique index refuses a taken slug, which answers 409 `tenant.slug_taken` and writes nothing (K6b). Its roles follow only when the tenant was added. It then locks the tenant it added and invites the Owner through `Tenant.Invite`, with the platform as the inviter, as `InviteOwner` does (M1). A new tenant has no Owner and no invitation, so the invitation is never refused. Every rejection comes before any change (W7).
- A reserved slug, a top-level route of the tenant web or a common word that a product adds as one later, answers 400 `request.validation_failed` with `slug_reserved` and writes nothing (`ARCHITECTURE.md`, section 4, `ReservedSlugs`). A reserved slug is matched exactly, with no prefix rule: `acme-settings` and `sign-inn` are normal slugs. So each open route of the web must match its whole segment, never a prefix: the matcher of `sign-in` matches `/sign-in` and the paths under it, not `/sign-inn` (the decision of step 33-frontend's review). A tenant that has a reserved slug already keeps it: a slug never changes, and the template was not in production when the list came.
- A failure inside the transaction leaves nothing: no tenant, no summary, no invitation, no message (`CreateTenant_TheInvitationCannotBeWritten_WritesNoTenantNoInvitationAndSendsNothing`).
- A failure after the commit undoes nothing. When the identity provider's call or the email goes to the dead letter queue, the tenant stays active and the Owner invitation stays pending, as for `InviteOwner`: the platform invites the Owner again, which replaces the invitation (K9). Tests: `CreateTenant_TheEmailKeepsFailing_LeavesTheTenantActiveAndTheInvitationPending`, `CreateTenant_TheIdentityProviderKeepsFailing_LeavesTheTenantActiveTheInvitationPendingAndSendsNoEmail`.
- The invitation link travels to Notifications inside the message. See the deviations list (`ARCHITECTURE.md`, section 11).
- The invitation code and what accepting needs: the rule is in `ARCHITECTURE.md`, section 6, "Rules without an id", "The invitation code".
- The email handler is in `Notifications.Application`. The event type is in `ControlPlane.Contracts`. ControlPlane does not know how to send email.
- Invitation expiry is checked when the invitation is read. There is no nightly job.
- The root system admins come from email addresses in configuration (`system-admins.md`).

Source: Golding, Chapter 4. Richardson, Chapter 4.

## 2. A user's own tenant

Step 40d. While the product has sign-up on (`ControlPlane:SignUp:Enabled`, off by default), a person who signed up at the identity provider becomes a user, then creates a tenant of their own. Each is one transaction, with no message to the identity provider.

1. The person signs up at the identity provider, which asks a captcha and verifies the address (`.clerk/config.json`). They are signed in, and not a user yet: `GetMe` answers 404 `user.not_found`.
2. `SignUp` (`POST /v1/me/sign-up`) reads the person's verified addresses from the identity provider, outside the transaction. Then, in one transaction, it takes the lock of the identity provider id (K6f), checks the address under its own lock (A7), and adds the user with the first verified address, no name and the language the person chose. When the person ticked the terms in force (1.27), the same transaction records the acceptance. A known user is answered as they are.
3. `CreateMyTenant` (`POST /v1/me/tenants`) runs inside the tenant it creates, whose id the server chose. It locks the user row first, so two creations of one person run one after the other, and counts the tenants the user created that were not purged: at `ControlPlane:SignUp:TenantLimit` it answers 409 `tenant.creation_limit_reached`. Then it writes the active tenant on the default plan, its summary, its built-in roles and the user's Owner membership, which counts on the user row (R13), in one transaction. No invitation is made. It sends `TenantCreatedEvent` with `ByItsOwner` through the outbox: Audit writes `tenant.created` in both logs, by the user.

A purge clears the tenant's creator (`Tenant.Purge`), so a purged tenant leaves the count. The tenant's trial and the welcome email come with 40d-backend's next part.
