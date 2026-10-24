# Tenant summary

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

The platform's tenant list reads across tenants, where row level security hides the memberships and the invitations (R1, R5). A table of its own, `tenant_summaries`, keeps a summary for it, one row for each tenant (R12; api-catalog, K10).

- `ITenantRepository.SaveAsync` saves the transaction's changes, reads the summary from the tenant's memberships and invitations as the transaction leaves them, and keeps it with `TenantSummary.Summarize`: the members, the Owners, and the latest Owner invitation (by its creation, then its id). It keeps the invitation's address, expiry, status and language only while the tenant has no Owner, because the address is a person's. It is the only code that writes the summary.
- `CreateTenant`, `AcceptInvitation`, `InviteMember`, `InviteOwner`, `MakeOwner`, `RevokeInvitation`, `ChangeMemberRole`, `RemoveMember`, `RemoveUserMembershipCommand`, `CloseTenant` and `RecordSignUpCommand` save through it under the tenant's lock, in their transaction (K6). `RecordSignUpCommand` changes only the invitation's sign-up, which the summary does not show; it saves through it all the same, because the guard asks it of every change to an invitation.
- A command that saves a change to a membership or an invitation and then no summary fails at the commit, and its transaction rolls back (`TenantSummaryInterceptor`).
- `ListTenants` reads the tenant rows with their summaries joined: one query for the page, and one for the total. `GetTenant` reads the tenant with its summary joined, and every Owner of the tenant, in its own transaction (W3). The state `expired` of a pending invitation comes from its expiry at the moment of the read. `reopenableUntil` is `closedAt` plus the reopen period (`Tenant.ReopenableUntil`).
- Tests: `TenantSummaries` (the summary against the rows after each command of the platform and the tenant web, the race tests included; not after `RecordSignUpCommand`), `GetTenantTests`, `ListTenantsTests`.

Source: Evans, Chapter 6, "Aggregates". Kleppmann, Chapter 11 (derived data). Golding, Chapter 2.
