# Plans and entitlements

A system admin decides from the platform what each tenant may use, with no deploy. The code holds the keys; the database holds the plans, each plan's value of each key, each tenant's plan and the tenant's overrides. The rules are E1 to E7 in `ARCHITECTURE.md`, section 6. This file says what happens, step by step.

## 1. The keys of this step

| Key | Kind | Counts | Free (default) | Team | Business |
| --- | --- | --- | --- | --- | --- |
| `roles.custom` | feature | the tenant's custom roles, which use it | off | on | on |
| `roles.custom.max` | limit | the tenant's custom roles | 0 | 5 | 20 |
| `members.max` | limit | the tenant's seats: its members and its pending invitations that have not expired, without the Billing manager's | 3 | 15 | 50 |
| `audit.retention.days` | limit | nothing: the days the tenant's Activity log keeps an entry (K14) | 365 | 365 | 365 |

- The migration `AddPlansAndEntitlements` seeds Free and Pro, marks Free default and moves every existing tenant to Pro. Pro holds what every tenant had before: custom roles on, 20 of them, members without a limit. The migration takes nothing away.
- Step 40's migration `AddPlanPricesAndMeters` renames Pro to Business with 50 seats, adds Team and gives Free 3 seats, so a new and an existing database hold the same three plans. It takes Pro's unlimited members away, on purpose: a tenant over 50 keeps everything (E4). A plan a system admin named Team or Business keeps its name with " (old)" after it. The seeded prices are 0 (the maintainer's decision of 2026-11-15, `docs/roadmap.md`, section 8).
- A Billing manager takes no seat (step 40, frame 10.01). Its members and its pending invitations are not counted, so a Billing manager is invited when the seats are full. Example: 11 members, 1 of them a Billing manager, and 2 pending invitations use 12 seats.
- A pending invitation counts toward `members.max`. Example: a limit of 10 and 9 members. Without the invitations, 5 invitations could go out and 14 members could join. With them, one invitation can go out.
- The migration `AddRetentionLimit` (step 37c) gives every plan 365 days of `audit.retention.days`. A plan that a system admin creates later gets the value its editor sends.
- The Activity log period is a whole number of at least 30 days, `AuditRetention.Floor`, or unlimited. A value under 30 answers 400 `plan.entitlement_invalid` with the key, from `CreatePlan`, `UpdatePlan`, `GetPlanUpdatePreview` and `SetEntitlementOverride`. The CHECK constraints `ck_plan_entitlements_audit_retention_floor` and `ck_entitlement_overrides_audit_retention_floor` refuse it too (E1, R15).
- `roles.custom.max` depends on `roles.custom` (E1). A plan's create or edit that gives the limit another value while the plan's `roles.custom` is off after the request answers 400 `plan.feature_required` with `roles.custom.max`. Example: a plan with the feature off and a limit of 3. An edit that keeps 3 is saved. An edit that sends 5 is refused. An edit that turns the feature on and sends 5 is saved. An edit that leaves the limit out removes it, because that only closes it.
- An override of `roles.custom.max` is set only while the tenant's effective `roles.custom` is on: its plan's value, or an override of the feature that has not expired. Else 400 `plan.feature_required`. Removing the override is always allowed.
- Turning `roles.custom` off, on the plan or by an override, deletes no value and no override of `roles.custom.max`. When the feature turns on again, the stored limit applies again. While the feature is off, its gate refuses a new custom role (E5).
- The screens read the pair from `LimitFeatures` of `@repo/api-client`, a file that the generator of the keys writes (D1).
- A key that a plan has no value for is closed: a feature is off, a limit is 0 (E3). One key is not: a missing `audit.retention.days` keeps every entry, as unlimited does, because a period of 0 would delete the whole log.
- The OpenAPI document lists the keys as two enums, `FeatureKey` and `LimitKey` (N11i). The floor and the hold of the Activity log period, 30 days each, and the period a new plan starts with, 365 days, reach the clients as `AuditRetention.Floor`, `AuditRetention.Grace` and `AuditRetention.Default` of `@repo/api-client`, a file that the generator of the keys writes (D1). A client reads them from the generated client. So a client that lists every key shows a key added in code at once, closed until a system admin gives it a value.

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
- `InviteMember` refuses a new invitation of a role that takes a seat when the seats, the members and the pending invitations that have not expired, each of a role that takes a seat, less the one the new invitation replaces, are `members.max` or more: 409 `plan.limit_reached`, `entitlement` `members.max`. An invitation of the Billing manager is not limited.
- `ChangeMemberRole` refuses to give a Billing manager a role that takes a seat when the seats are `members.max` or more, with the same 409. Giving a member the Billing manager role frees a seat, and is not limited.
- `InviteOwner` and `MakeOwner`, from the platform, are not limited: a tenant always gets an Owner, a Billing manager made an Owner included (`owner-recovery.md`).
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
- There is no deletion of a plan, no plan version, no trial and no billing in this step. Billing (step 40) changes a tenant's plan through ControlPlane. The trial came with step 40d as a state of the tenant's plan (`tenant-creation.md`, section 3).

## 5a. Prices and meters (step 40)

- A plan has prices: `prices` `{ baseMonthly, perPersonMonthly, baseYearly, perPersonYearly }`, each a whole number of the minor unit of the deployment's one currency, 0 to 10 000 000 000 (400 `plan.price_invalid`; `ck_plans_prices`). The currency is `ControlPlane:Billing:Currency`, EUR by default, and `PlanResponse` answers it as `currency` (the maintainer's decision of 2026-11-14; Fowler, *Patterns of Enterprise Application Architecture*, "Money"). The fee per person counts the seats, so a Billing manager pays none.
- A plan sets some meters of the code: `meters[]` `{ key, quota, overage, unitPrice, unitSize }`. `overage` is `stop` or `charge` at the quota; the extra costs `unitPrice` minor units for each `unitSize` units. The meters are the code's (`MeterCatalog`), as the keys are; the template defines none, so a plan of the template sets none, and a test's host defines one of its own (`MeterCode.Of`). A key the code does not define, a key sent twice, a quota out of 0 to 10^12, a unit size out of 1 to 10^12 or a unit price out of the prices' range answers 400 `plan.meter_invalid`. The key has no CHECK constraint: a product adds a meter without a migration. A plan without a meter shows no Usage.
- `CreatePlan` takes both, optional: prices left out are 0, meters left out are none. `UpdatePlan` sets exactly the prices and the meters it sends, as it does the values; left out, the plan keeps its own, so a client of the version before step 40 changes neither. `GetPlan` answers both, the meters by key.
- `plan.created` and `plan.updated` keep the prices and the meters in their details (an entry from before step 40 has none). An edit of only the prices or the meters writes `plan.updated`.
- Not in this step: the prices in Stripe and a price change (40b), the meters' counts and the Usage page (40c).

## 5b. The use of a meter (step 40c)

1. A product's module asks ControlPlane before a use: `IUsageMeter.UseAsync(tenantId, meterKey, quantity)`, a quantity of at least 1. It invokes `UseMeterCommand` inline in the tenant, in a short transaction of its own (ARCHITECTURE.md, E8, K6q).
2. The handler holds the tenant row `FOR SHARE` and reads the meter's setting from the tenant's plan, from the database. A tenant that is not active answers `Stopped`, and so does a meter the plan does not set: it is not on the plan. A stop meter's use that alone goes over the quota answers `Stopped` before any row is written.
3. It inserts the row of the tenant, the meter and this calendar month of UTC when it is missing, and locks it. Of two uses at once, the second waits and sees the first's count.
4. A stop meter: a use that would take the count over the quota answers `Stopped` and counts nothing. A use up to the quota is counted.
5. A charge meter: every use is counted, and the part over the quota is the extra. The row keeps the unit price and the unit size of the use, and the first moment the count reached the quota.
6. The answer is `Used`. A new month starts from a new row, so the count starts again from nothing.

`GetUsage` (Usage, 11.01) and the platform's `GetTenantUsage` (4.37) read the same: this month for each meter of the plan, with the plan's setting, and up to three months before this one, from the month the tenant was created, the latest first. A month without a use answers 0. Each month's extra keeps the price of its last use, this month's too. A past month also lists a meter that the plan no longer sets but that it has a use of. The tenant's purge deletes the counts (`tenant-purge.md`).

## 5c. The spending cap (step 40c)

1. A member who manages billing sets the cap, an amount in minor units a month, or removes it with none: `PUT /v1/tenants/{tenantId}/usage/spending-cap`, body `{ "amount" }` (ARCHITECTURE.md, E9).
2. The endpoint asks Billing first whether the tenant pays by card. A tenant that does not answers 409 `usage.spending_cap_unavailable`.
3. The handler checks the amount, locks the tenant row, then the cap's row, and adds, changes or removes it. The amount it already has changes nothing. A change clears the cap's marks.
4. It publishes `SpendingCapChangedEvent`, and Audit records `tenant.spending_cap_changed` in the tenant's log.
5. A use of a charge meter while the tenant pays by card locks the cap after the tenant row and before the month's row (K6q). When the use adds extra, it adds this month's extra amount of every charge meter. A use that lands on the cap is used and marks it reached. A use that goes over is stopped, counts nothing and marks it reached.

`GetUsage` answers the cap, this month's extra amount and the moment the cap was reached this month, and `spendingCapOffered`, whether the tenant pays by card. The tenant's purge deletes the cap.

The first use of a month that takes a meter's count to 80% or to all of its quota, or the extra to 80% or to all of the cap, tells the members who manage billing and the billing email once (ARCHITECTURE.md, E10; `notifications.md`).

## 5d. The extra at Stripe (step 40c)

1. The first use of a month that costs extra schedules the month's close for the first moment of the next month in UTC (ARCHITECTURE.md, E11).
2. At that moment the close locks the month's row and publishes `MeterMonthClosedEvent`: the month's extra and what it costs at the price of its last use, rounded, as Usage shows it. An amount of 0 publishes nothing.
3. Billing hears it in the tenant. A Contract tenant, a tenant that never paid by card and one whose subscription has ended are charged nothing: a system admin invoices a Contract's extra from 4.37.
4. Billing asks Stripe for one invoice item of the amount on the subscription, with the month as its period and "{meter}, {month}: {extra} over the quota" as its line. Stripe adds it to the subscription's next invoice. The tenant, the meter and the month are the call's idempotency key.
5. A yearly payer's extra goes on the subscription's next invoice too, which can be the next year's.

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
| `SetSpendingCap` | `SpendingCapChangedEvent`, only when the amount changed | `RecordSpendingCapChangedHandler` | tenant | `tenant.spending_cap_changed`, by the member; details: the old and the new amount, null for none, and the currency |

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
