# Member management

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

An Owner or an Admin changes a member's role and removes a member (api-catalog, K1). This is not a saga: ControlPlane changes the tenant aggregate in one transaction (S1).

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `ChangeMemberRoleCommand` | `PATCH /v1/tenants/{tenantId}/members/{userId}` | ControlPlane, `ChangeMemberRoleHandler` | Yes |
| `MemberRoleChangedEvent` | `ChangeMemberRoleHandler`, only when the role changed | Audit, `RecordMemberRoleChangedHandler` (`member.role_changed`, tenant log) | Yes |
| `RemoveMemberCommand` | `DELETE /v1/tenants/{tenantId}/members/{userId}` | ControlPlane, `RemoveMemberHandler` | Yes |
| `MemberRemovedEvent` | `RemoveMemberHandler` | Audit, `RecordMemberRemovedHandler` (`member.removed`, tenant log) | Yes |

- Both commands lock the tenant row (K6). They then read the actor's and the target's memberships and count the Owners, in this order:
  1. The actor is no longer a member, because a change that held the lock before removed them: 404 `tenant.not_found`. The tenant door answers a non-member the same 404 `tenant.not_found` (T3). `InviteMember` and `RevokeInvitation` check the same first (`invitations.md`, section 2, "Member invitation").
  2. The tenant is no longer active, because a suspension or a closure held the lock before: 403 `tenant.suspended` or `tenant.closed`, the door's answer (`tenant-lifecycle.md`). `InviteMember` and `RevokeInvitation` check the same second.
  3. No target membership in the tenant: 404 `member.not_found`. Row level security hides another tenant's members.
  4. `RemoveMember` on yourself: 403 `member.cannot_remove_self`.
  5. An Admin who acts on an Owner, or who gives the Owner role: 403 `member.owner_requires_owner`.
  6. The member's role, or the new role (`ChangeMemberRole`), holds a permission the actor's role does not: 403 `role.escalation` (`custom-roles.md`).
- `ChangeMemberRole` refuses a custom role while the tenant's `roles.custom` is off (403 `plan.feature_unavailable`, with the key), right after it finds the member, under the lock; a built-in role is always given, and a member keeps the custom role they have (ARCHITECTURE.md, E5; `plans-and-entitlements.md`).
  7. The change would leave no Owner: 409 `member.last_owner`.
  8. The same role (`ChangeMemberRole` only): 200, and nothing is published (I1).
- `ChangeMemberRole` refuses a role id the tenant has not, another tenant's included, with 404 `role.not_found`, before check 3.
- The rules of checks 3 to 8 are the tenant's (M1): `Tenant.ChangeRole` and `Tenant.Remove`, pure until the change. The handler reads their facts under the lock (`ITenantRepository.FindMemberFactsAsync`: the target's membership and the Owner count). Check 2 is the tenant's too (`Tenant.CheckMemberAccess`). Every rejection returns before any change (W7).
- The Owner count holds every Owner membership, a deleted user's included (api-catalog, K1).
- A member may change their own role under the same rules. Nobody removes themselves, so a removal never meets check 7 one request at a time: the actor is an Owner, and a second Owner is the target. Check 7 guards the count.
- `ChangeMemberRole` answers the member, as `ListMembers` shows them. `RemoveMember` answers 204. Neither takes an idempotency key: both set a state (I1).
- A removed member loses access at once: the tenant door reads the membership on every request (T7). No cache holds it.
- Each new message type comes with all its handlers in the same release, so the gap of W12 does not apply.

Source: Evans, Chapter 6, "Aggregates". Vernon, "Effective Aggregate Design". Kleppmann, Chapter 7, "Write Skew and Phantoms".
