# Feature flags

The code defines each feature flag. The worker stores a new flag, and archives a removed one, at its start. A system admin changes a flag's three rules from the platform. Any module asks whether a flag is on for an organisation, and the web reads its organisation's flags. Once a week the system admins hear which flags are ready to leave the code. Each environment's platform manages only its own flags, in its own database. The step is 39 of `docs/roadmap.md`, section 7. The rules are in `ARCHITECTURE.md` (G1 to G6, C1, C5, K3, K6p, R6, U1, U3, U5, U9). This file says what happens, step by step.

## 1. A flag in the code

- A flag is a key, a type and a description in `FeatureFlagCatalog.Template` (`ControlPlane.Domain/FeatureFlags/`), with its key in `FeatureFlagKeys` (G1). The type is `release` or `kill_switch`. The default value follows from the type: a release flag is off, a kill switch is on.
- `pnpm generate` writes the keys into `ControlPlane.Contracts/FeatureFlags/FeatureFlagKeys.g.cs`, for any module, and into `packages/api-client/src/feature-flags.gen.ts`, for the web (D1).
- A removed flag's key moves to the catalog's retired keys and stays there. `FeatureFlagCatalogTests` refuses two flags with one key, a retired key that a flag takes, and a key that is not lowercase and dotted.
- The template defines no flag. A product adds its own.

## 2. The sync at the worker's start

1. The worker sends `SyncFeatureFlagsCommand` once it has started (`FeatureFlagJob`, in the roles worker and all). Its handler takes one lock of its transaction first, so two workers that start at once store a flag once (G2, K6p).
2. A flag that the code defines and the environment does not store is stored, with the moment and the commit of the image (`ControlPlane:FeatureFlags:Revision`, which the image sets from `SOURCE_REVISION`). Its rules start at its default value: rule 3 is on for a kill switch and off for a release flag. History: `added_to_code`. Audit: `feature_flag.added_to_code` in the platform's log, by the platform, with the commit and the default value.
3. A stored flag whose key the code retires is archived, with the moment and the commit. Its rules stay as they were and no longer change (409 `feature_flag.removed_from_code`). History: `removed_from_code`. Audit: `feature_flag.removed_from_code`.
4. A stored flag that the code still defines takes the code's description.
5. A stored flag that the code neither defines nor retires stays as it is. Example: a rollback to a version before the flag; the next deploy of the newer version finds the flag as the rollback left it.
6. A second sync finds everything done and writes nothing.

## 3. The three rules

A flag is on for an organisation when one of its rules turns it on, in this order (G3):

1. The organisation is listed.
2. The organisation's bucket is under the percentage. The bucket is from 0 to 99: SHA-256 of `{key}:{tenant id}`, its first four bytes as a big-endian number, modulo 100. The same organisation keeps its bucket at every percentage, so an organisation that is in stays in as the percentage grows. The percentage counts organisations, not people. A percentage of 0 turns the rule off.
3. Everyone else: on or off.

A release flag and a kill switch have the same rules.

## 4. The platform's changes

Each change sets a state, so it takes no idempotency key, and a value the flag has writes nothing (I1). Each answers the flag's page. Each records a line of the flag's history and an entry of the platform's log, by the system admin (G5, K3).

| Change | Endpoint | Locks | History | Audit |
| --- | --- | --- | --- | --- |
| List an organisation | `AddFeatureFlagTenant` | the tenant row `FOR SHARE`, then the flag row | `listed` | `feature_flag.tenant_listed` |
| Remove a listed organisation | `RemoveFeatureFlagTenant` | as above | `unlisted` | `feature_flag.tenant_unlisted` |
| Set the percentage | `SetFeatureFlagPercentage` | the flag row | `percentage_changed` | `feature_flag.percentage_set` |
| Turn everyone else on or off | `SetFeatureFlagEveryoneElse` | the flag row | `everyone_else_changed` | `feature_flag.everyone_else_on` or `_off` |

- A closed, purging or purged organisation is not listed (409 `tenant.not_active`). An organisation of any status leaves the rule.
- Before a change, the screen asks `GetFeatureFlagChangePreview` how many organisations it turns on or off: every organisation that is neither purging nor purged and whose value differs before and after. It reads without a lock and never blocks.
- The organisation's purge takes it off every flag's first rule, in the purge's transaction, with no history line (`flows/tenant-purge.md`).

## 5. The evaluation

1. A module asks `IFeatureFlags.IsOnAsync(key, tenantId)`; the web's server asks `GetMyTenantFeatureFlags`, which answers every flag of the code for the route's organisation (G4).
2. The evaluation reads the organisation's rows of every stored flag that the code has not removed, through the shared cache (`tenant-feature-flags`, C1). Any change of a flag or a listing removes every organisation's entry, by the tag `feature-flags`, at the commit and durably (C5, C6).
3. A flag that the code defines and the environment does not store yet answers its default value. So does every flag when the rows cannot be read: the evaluation logs a `Warning` and answers the defaults.
4. A key the code does not define is a mistake of the caller, and throws.

## 6. The cleanup and the weekly bell

1. A release flag on for everyone for 30 days is `ready_to_remove`. Any flag off for every organisation for 30 days is `unused`. "On for everyone" is rule 3 on, or a percentage of 100. "Off for every organisation" is no listed organisation, a percentage of 0 and rule 3 off. The 30 days count from the moment the flag's rules last moved it into that state (`cleanupSince`). An archived flag has no cleanup (G6).
2. The worker sends `CheckFeatureFlagCleanupCommand` at its start and every 24 hours. Its handler counts both kinds and publishes `FeatureFlagCleanupDueEvent` when one count is not 0. The event's id is a hash of the ISO week, so every copy of one week's event writes each notification once (U3).
3. Notifications tells every system admin in the platform's bell, on the web only: `feature_flags.ready_to_remove` and `feature_flags.unused`, each with its count, which leads to the platform's Feature flags. No email goes (U5).
4. The check writes no audit entry: it changes no data (K3).

## 7. The messages

| Message | Sent by | Handled by |
| --- | --- | --- |
| `SyncFeatureFlagsCommand` | `FeatureFlagJob` | ControlPlane's `SyncFeatureFlagsHandler` |
| `CheckFeatureFlagCleanupCommand` | `FeatureFlagJob` | ControlPlane's `CheckFeatureFlagCleanupHandler` |
| `FeatureFlagAddedToCodeEvent`, `FeatureFlagRemovedFromCodeEvent` | the sync | Audit's `RecordFeatureFlagAddedToCodeHandler`, `RecordFeatureFlagRemovedFromCodeHandler` |
| `FeatureFlagTenantListedEvent`, `FeatureFlagTenantUnlistedEvent`, `FeatureFlagPercentageSetEvent`, `FeatureFlagEveryoneElseSetEvent` | the platform's changes | Audit's `RecordFeatureFlag...Handler` |
| `FeatureFlagCleanupDueEvent` | the cleanup check | Notifications' `NotifyFeatureFlagsReadyToRemoveHandler`, `NotifyFeatureFlagsUnusedHandler` |
