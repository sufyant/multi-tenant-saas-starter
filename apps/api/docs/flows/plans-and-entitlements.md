# Plans and entitlements

A system admin decides from the platform what each tenant may use, with no deploy. The code holds the keys; the database holds the plans, each plan's value of each key, each tenant's plan and the tenant's overrides. The rules are E1 to E7 in `ARCHITECTURE.md`, section 6. This file says what happens, step by step.

## 1. The keys of this step

| Key | Kind | Counts | Free (default) | Pro |
| --- | --- | --- | --- | --- |
| `roles.custom` | feature | the tenant's custom roles, which use it | off | on |
| `roles.custom.max` | limit | the tenant's custom roles | 0 | 20 |
| `members.max` | limit | the tenant's members and its pending invitations that have not expired | 5 | unlimited |
| `audit.retention.days` | limit | nothing: the days the tenant's Activity log keeps an entry (K14) | 365 | 365 |

- The migration `AddPlansAndEntitlements` seeds Free and Pro, marks Free default and moves every existing tenant to Pro. Pro holds what every tenant had before: custom roles on, 20 of them, members without a limit. The migration takes nothing away.
- A pending invitation counts toward `members.max`. Example: a limit of 10 and 9 members. Without the invitations, 5 invitations could go out and 14 members could join. With them, one invitation can go out.
- The migration `AddRetentionLimit` (step 37c) gives every plan 365 days of `audit.retention.days`. A plan that a system admin creates later gets the value its editor sends.
- The Activity log period is a whole number of at least 30 days, `AuditRetention.Floor`, or unlimited. A value under 30 answers 400 `plan.entitlement_invalid` with the key, from `CreatePlan`, `UpdatePlan`, `GetPlanUpdatePreview` and `SetEntitlementOverride`. The CHECK constraints `ck_plan_entitlements_audit_retention_floor` and `ck_entitlement_overrides_audit_retention_floor` refuse it too (E1, R15).
- A key that a plan has no value for is closed: a feature is off, a limit is 0 (E3). One key is not: a missing `audit.retention.days` keeps every entry, as unlimited does, because a period of 0 would delete the whole log.
- The OpenAPI document lists the keys as two enums, `FeatureKey` and `LimitKey` (N11i). The floor and the hold of the Activity log period, 30 days each, reach the clients as `AuditRetention.Floor` and `AuditRetention.Grace` of `@repo/api-client`, a file that the generator of the keys writes (D1). A client reads them from the generated client. So a client that lists every key shows a key added in code at once, closed until a system admin gives it a value.

## 2. The effective value

1. The tenant's override of the key applies while it has not expired.
2. Else the plan's value applies.
3. Else the key is closed, apart from `audit.retention.days`, which is then unlimited: `GetTenantPlan` answers its effective value as unlimited and its `planValue` as null.

The value is computed at each read, with the moment of the read (E3). An override that expires stops at once, and stays, shown expired, until a system admin removes it.

How each answer shows a value:

- The platform reads `{ enabled, maximum, unlimited }`: a feature's state, a limit's number, or `unlimited` true.
- The tenant web reads `GetMyTenant.entitlements`, every key with its effective value: `true` or `false`, a number, or `null` for an unlimited limit. `GetMyTenant.usage` gives each limit's count, so the web adds no list up.

## 3. The feature check

1. A tenant endpoint names its feature as an authorization policy: `POST /roles` and `PATCH /roles/{roleId}` name `roles.custom` (E5).
2. The tenant door resolves the membership. Authorization checks the permission first.
3. The host then reads the tenant's entitlements through `IEntitlementDirectory`, from two cached reads: `tenant-entitlements` (the plan and the overrides, tag `tenant:{id}`) and `plan-entitlements` (the plan's values, tag `plan:{id}`) (C1, C5).
4. A feature that is off answers 403 `plan.feature_unavailable`, with `entitlement` the key.

Every other tenant endpoint carries `NotPlanGated` (`PlanGateTests`). With `roles.custom` off:

- `GET /roles` lists every role, custom roles included.
- `DELETE /roles/{roleId}` deletes a custom role.
- A member who holds a custom role keeps its permissions.
- `ChangeMemberRole` refuses a custom role, and `InviteMember` refuses an invitation with one, with the same 403. Both check it in the tenant's method under the lock (E5, E6). A built-in role is always given.

## 4. The limits

The tenant's method takes the effective entitlements, which the handler reads under the tenant's lock from the database, not the cache (E6).

- `CreateRole` refuses a new custom role when the tenant has `roles.custom.max` or more: 409 `plan.limit_reached`, `entitlement` `roles.custom.max`.
- `InviteMember` refuses a new invitation when the members and the pending invitations that have not expired, less the one the new invitation replaces, are `members.max` or more: 409 `plan.limit_reached`, `entitlement` `members.max`.
- `InviteOwner`, from the platform, is not limited: a tenant without an Owner always gets one.
- `AcceptInvitation` is not limited: the invitation counted already, so an acceptance adds nothing to the count and never fails on the limit.
- Two creations at the edge run one after the other under the tenant's lock, so only one passes (`CreateRole_TwoAtOnceWithOnePlaceLeft_NeverPassTheLimit`).

Over a limit (E4): nothing is deleted, what exists keeps working, nothing new that counts is added until the count is under the limit, and deleting stays allowed. `audit.retention.days` counts nothing and is never over. It is the one limit that deletes: the daily job deletes the entries outside the period, after the hold of 30 days that a shorter period starts (K14, `reading-the-audit-logs.md`).

## 5. The platform

| Step | Endpoint | Permission |
| --- | --- | --- |
| List the plans, with each plan's tenant count | `GET /v1/platform/plans` | `platform.plans.read` |
| Read one plan | `GET /v1/platform/plans/{planId}` | `platform.plans.read` |
| Create a plan | `POST /v1/platform/plans` | `platform.plans.create` |
| Preview a plan edit | `POST /v1/platform/plans/{planId}/preview` | `platform.plans.update` |
| Edit a plan: its name, default mark and values | `PATCH /v1/platform/plans/{planId}` | `platform.plans.update` |
| Read a tenant's plan and entitlements | `GET /v1/platform/tenants/{tenantId}/plan` | `platform.tenants.read` |
| Preview a tenant's plan change | `POST /v1/platform/tenants/{tenantId}/plan/preview` | `platform.tenants.change_plan` |
| Change a tenant's plan | `PUT /v1/platform/tenants/{tenantId}/plan` | `platform.tenants.change_plan` |
| Set an override | `PUT /v1/platform/tenants/{tenantId}/entitlement-overrides/{key}` | `platform.tenants.override_entitlements` |
| Remove an override | `DELETE /v1/platform/tenants/{tenantId}/entitlement-overrides/{key}` | `platform.tenants.override_entitlements` |

- A plan's name is 1 to 100 characters, trimmed, and unique without regard to case (409 `plan.name_taken`). The unique index on `lower(name)` is the second guard (K6b).
- A plan becomes the default by marking it. The mark leaves the previous default plan in the same transaction. The default plan keeps its mark until another plan takes it (409 `plan.default_required`).
- An edit gives the plan exactly the values it sends: a key left out loses its value and is closed. It applies at once to every tenant on the plan. A plan edit locks the plan row, then the default mark.
- `CreateTenant` takes an optional `planId`; without one, the tenant gets the default plan.
- A closed, purging or purged tenant's plan and overrides are read-only (409 `tenant.not_active`). A suspended tenant's can change. The purge deletes the overrides; the tombstone keeps its plan, because a tenant always has one.
- There is no deletion of a plan, no plan version, no trial and no billing in this step. A trial is an override with an expiry; billing (step 40) will change a tenant's plan through ControlPlane.

## 6. The previews

A preview reads and changes nothing, and never blocks the change. It lists:

- each key whose new effective value is a limit under the tenant's count, with the count, the new value and how far over the tenant goes;
- each feature that turns off while the tenant uses it, with what uses it: `roles.custom` off for a tenant with 14 custom roles;
- the keys that an override of the tenant still holds, which the change does not touch;
- a shorter Activity log period, `retentionShortened`, or null (K14). A tenant's plan change gives `{ fromDays, toDays }`, the tenant's period now and on the new plan under its overrides at this moment; `fromDays` is null for no limit. A plan edit gives `{ count, fromDays, toDays }`: the plan's period before and after, and how many tenants on the plan follow it, those that are not purging or purged and have no override of the key that has not expired. An expired override holds nothing (E3). The count is the edit's whole, which no search or page narrows. The entries outside the new period stay 30 days, then go; the dialogs say so with `AuditRetention.Grace`.

The preview of a plan edit is a page in the list contract (K5), `PlanUpdatePreviewPageResponse`: the list's fields and `retentionShortened`. It lists only the tenants on the plan that would go over, a purged tenant never. It searches the tenant's name or slug, and orders the tenant that goes furthest over first, then by name, with the total. The counts come from the tenants' summaries (R12): the custom roles and the expiry of each pending invitation, written with every change under the tenant's lock. Row level security hides the roles and the invitations across tenants; the summaries and the overrides are outside it (R6).

## 7. Audit

| Command | Event | Audit handler | Log | Operation |
| --- | --- | --- | --- | --- |
| `CreateTenant` | `TenantCreatedEvent`, with the plan the tenant got | `RecordTenantCreatedHandler` | platform and tenant | `tenant.created`; details: the plan, the default plan when the request named none |
| `CreatePlan` | `PlanCreatedEvent` | `RecordPlanCreatedHandler` | platform | `plan.created`; the target is the plan, with its name; details: the default mark and the values |
| `UpdatePlan` | `PlanUpdatedEvent`, only when something changed | `RecordPlanUpdatedHandler` | platform | `plan.updated`; the target keeps the new name; details: the old and the new name, default mark and values, and the previous default plan when the mark moved to this plan |
| `ChangeTenantPlan` | `TenantPlanChangedEvent`, only for another plan | `RecordTenantPlanChangedHandler` | platform and tenant | `tenant.plan_changed`; details: the old and the new plan |
| `SetEntitlementOverride` | `EntitlementOverrideSetEvent`, only when something changed | `RecordTenantEntitlementOverrideSetHandler` | platform and tenant | `tenant.entitlement_override_set`; details: the key, the old and the new value and expiry; the reason in the platform's entry only |
| `RemoveEntitlementOverride` | `EntitlementOverrideRemovedEvent` | `RecordTenantEntitlementOverrideRemovedHandler` | platform and tenant | `tenant.entitlement_override_removed`; details: the key and the value and expiry it had |

- A plan in the details is Audit's own `AuditPlanResponse`, `{ id, name }`, the plan as it was at that moment.
- `TenantPlanChangedEvent`, `EntitlementOverrideSetEvent` and `EntitlementOverrideRemovedEvent` carry the optional `retentionShortened`, `{ fromDays, toDays, goesAt }`, when the change makes the tenant's Activity log period shorter. ControlPlane reads the effective values before and after the change, in the tenant's transaction under its lock. `fromDays` is null for no limit, and `goesAt` is the event's moment plus 30 days. Audit's handler starts the tenant's hold at the event's moment, in the transaction that writes the entry (K14).
- The reason of an override is the provider's own note, so only the platform's entry keeps it, as for `tenant.suspended`.
- A plan edit writes no entry in the logs of the tenants on the plan (`reading-the-audit-logs.md`).
- A tenant's purge deletes its overrides with its other data (`tenant-purge.md`). The platform's log keeps its own entries about them.

## 8. A new feature step

Every feature step starts with the plan questions (E7):

1. Is the feature plan-gated?
2. Which keys does it add, a feature, a limit or both?
3. What is each plan's value of each key?
4. What does each limit count, written in `EntitlementUsage`?

A new key is added to `FeatureKeys` or `LimitKeys`, with a migration for the CHECK constraints. It is closed until a system admin gives a plan a value for it. A new tenant endpoint names its feature or carries `NotPlanGated`. Step 41 decides how a module other than ControlPlane adds its own keys.
