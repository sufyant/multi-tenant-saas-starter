# Notifications

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` (U1 to U9) and `api-catalog.md`.

Notifications hears an event that another module publishes and tells people. The module that raises the event knows nothing of notifications (Hohpe and Woolf, "Publish-Subscribe Channel"). A notification belongs to one user, and carries the tenant it is about, or none. It reaches the user on each channel, the bell and the email, when its category is required or the user's preferences allow it. An open bell hears of it at once, through a live signal that carries no content.

## 1. The types

A type tells of one event and has the name of its event's audit operation, so the same event carries one label in the audit log and in the bell (U9). The one exception, `tenant.owner_joined`, says why in `NotificationCatalog.NamedApart`.

| Type | Event | Recipients | Bell | Category | Required | Values | Link |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `member.role_changed` | `MemberRoleChangedEvent`, `OwnerMadeEvent` | the member, not when they changed their own role | the tenant's | `access` | no | the actor, none when the platform made them an Owner; the old role and the new role | `tenant_settings`, which shows "Your role" to every role |
| `member.removed` | `MemberRemovedEvent` | the removed person, not when they removed themselves | personal: about no tenant, because the person cannot enter it | `account_security` | yes | the actor | `start`: the start page, which opens the one tenant left, the choice of tenants, or "No access yet" |
| `member.joined` | `MemberJoinedEvent` with `InvitedBy` | the member who invited, while still a member: the acceptance names them only then | the tenant's | `member_invitations` | no | the new member and the role they joined as | `tenant_members` |
| `tenant.plan_changed` | `TenantPlanChangedEvent` | the tenant's Owners at that moment, the holders of `tenant.owners.manage` (A1), but the actor | the tenant's | `tenant_plans` | no | the old plan and the new plan | `tenant_settings`, which shows the plan |
| `tenant.entitlement_override_set` | `EntitlementOverrideSetEvent` | as `tenant.plan_changed` | the tenant's | `tenant_plans` | no | the key, the value, and the end, none for an override without one | by the key |
| `tenant.entitlement_override_removed` | `EntitlementOverrideRemovedEvent` | as `tenant.plan_changed` | the tenant's | `tenant_plans` | no | the key and the plan's value, which applies again | by the key |
| `tenant.owner_joined` | `MemberJoinedEvent` with `FirstOwner` | every system admin but the new Owner | the platform's | `platform_tenants` | no | the new Owner | `platform_tenant` |
| `tenant.suspended`, `tenant.reactivated`, `tenant.reopened` | `TenantSuspendedEvent`, `TenantReactivatedEvent`, `TenantReopenedEvent` | every system admin but the actor | the platform's | `platform_tenants` | no | the actor | `platform_tenant` |
| `tenant.closed` | `TenantClosedEvent` | every system admin but the actor | the platform's | `platform_tenants` | no | the actor and the date the purge deletes the data (`ReopenableUntil`) | `platform_tenant` |
| `tenant.purged` | `TenantPurgedEvent` | every system admin: the platform acted | the platform's | `platform_tenants` | no | none beyond the tenant | `platform_tenant` |

Every type also keeps the tenant's name as it was at that moment.

- An override leads to the page of its key: `members.max` to the members (`tenant_members`), `roles.custom` and `roles.custom.max` to the roles (`tenant_roles`), any other key to the tenant's general settings (`tenant_settings`).
- A value keeps a person by user id only (K3). The read names them now, so a deleted user reads as "Deleted user" in the reader's language, and a renamed one by the new name (U2). A tenant's, a plan's and a role's name stay as they were at that moment.
- An entitlement's value is a number, `unlimited`, `on`, `off`, or `not_set` when the plan has no value of the key; each word is written in the language. A date is written in the reader's language and time zone.
- Nobody is told of their own action, and a deleted or suspended user is told nothing (`NotificationFanOut`).
- The platform's Owner invitation tells no member: its acceptance sends `tenant.owner_joined` to the system admins and no `member.joined`. The platform removes a member only in the user's own deletion, which publishes no `MemberRemovedEvent`, so `member.removed` needs no text of the platform.
- A failure, such as a stuck purge or a dead letter, is no notification: the error tracker alerts it (section 7, "Error tracking").
- A plan's edit tells nobody. It would tell the Owners of every tenant on the plan; the platform's audit log records it, the known limit of `reading-the-audit-logs.md`.
- An event field that a type needs is optional, so both versions read each other's events: `TenantName` on the three member events, `InvitedBy`, `FirstOwner`, `Role` and `RoleBuiltIn` on `MemberJoinedEvent`, `ReopenableUntil` on `TenantClosedEvent`, and `PlanValue` on `EntitlementOverrideRemovedEvent`. An event of a version without the field tells nobody of the type that needs it. `FirstOwner` is true when the member joined with a role that holds `tenant.owners.manage` and the tenant had no Owner: a new tenant's first Owner, or an Owner the platform invited into a tenant without one.

## 2. From the event to the bell

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| the event of a type (section 1) | ControlPlane, through the outbox (O1) | Notifications, `Notify{Type}Handler` in the resource folder of the event | Yes, ControlPlane's, in the event's tenant or in none: it reads the recipients through `IUserDirectory` and `IMemberDirectory` |
| `DeliverNotificationCommand`, one for each recipient, sent without a tenant | the handler of the type | Notifications, `DeliverNotificationHandler`, the dispatcher | Yes, Notifications', which declares the recipient (R4) |
| `SendNotificationEmailCommand` | the dispatcher, when the email goes | Notifications, `SendNotificationEmailHandler` | No: it calls the email service (section 3, "Handlers and pipeline") |

1. ControlPlane commits a command and its event (O1).
2. The handler of each type names the recipients, through ControlPlane's contracts, and the values the texts need: a person by user id, any other value as it is at that moment. It never names a channel. The fan-out reads the names of the people once, for the email, which renders at its send; the row never keeps them. It sends one `DeliverNotificationCommand` for each recipient (U3; Kleppmann, Chapter 1, fan-out on write). Someone who becomes a recipient later sees no older notification.
3. The dispatcher declares the recipient and reads their preferences as a value, without a lock. A channel goes when the type's category is required, or when the channel's main switch is on and the category's switch is on, or, without a choice, when the category's default in the catalog is on (U4). Today every category but the required one has the web on and the email off.
4. When the web goes, the dispatcher writes the notification under an id that the event, the type and the recipient decide, so a copy of the event writes nothing more (O4). When the web is off, it writes no row; the email still follows its own channel, and the email's key, the notification's id, keeps a copy of the event to one email.
5. The commit of a written notification signals the recipient's bell on the live channel (U6). A failed signal is a warning; the next read is right.
6. The email is the one generic notification email: the title and the body that the bell shows, in the recipient's language and time zone, with the names of the event's moment, and a button to the web app, or to the platform app for the platform's notifications (`Notifications:Links`). Its footer names the type's category, with a "Manage email notifications" link to `/settings/notifications` of the same app (U2). A required type's footer says that it always goes, without the link. An email that fails is tried again and goes to the dead letter queue (W11); the notification stays.

A notification reaches an open bell within about one to two seconds of the commit: the outbox delivers the event, then the delivery, through the worker's queue (W5).

## 3. The bells

| Bell | Endpoint | Shows | Access |
| --- | --- | --- | --- |
| The tenant web's | `GET /v1/tenants/{tenantId}/notifications` | that tenant's and the personal ones | `tenant.profile.read`, every member; NotPlanGated |
| The personal one | `GET /v1/me/notifications` | the personal ones, for a signed-in user without a tenant (the "no access" and the select-organisation pages) | signed in |
| The platform's | `GET /v1/platform/notifications` | the platform's | `platform.tenants.read`, every system admin |

- A read declares the reader, never the tenant (U7). The tenant door proved the membership, and the route's tenant only selects the reader's own rows. A person removed from a tenant fails the tenant door, so they no longer see that tenant's notifications. A personal one stays in every tenant's bell.
- Each bell has its unread count (`.../unread-count`) and marks everything it shows read (`POST .../read`), and nothing of another tenant. `POST /v1/me/notifications/{notificationId}/read` opens one notification of any bell; another user's answers 404 `notification.not_found`. There is no "mark unread", no archive and no single delete.
- A list pages by a cursor, newest first, as the audit logs do (K5).
- An item is `{ id, type, category, title, body, link: { kind, tenantId }, createdAt, readAt }`. The title and the body are rendered at the read, in the reader's language and time zone, from the type and the stored values (U2). The read is two queries, as an audit log's (K3): Notifications lists the page as stored, then `GetNotificationNamesQuery` names every person of the page in one call to `IUserDirectory.FindNamesAsync` and renders it. The link is a kind (`start`, `tenant_members`, `tenant_roles`, `tenant_settings`, `platform_tenant`) and the tenant, none for `start`; the web builds the URL.
- Marking read writes no audit entry: it is the reader's own view (K3). Its commit signals the reader's bell, so their other tabs read again.

## 4. The live stream

1. The web's BFF opens `GET /v1/me/notifications/stream` for the signed-in user, with the user's token. The browser never reaches the API.
2. The stream answers `text/event-stream`. Each change of one of the user's bells is one event, `notifications`, whose data is the bell only: `{"scope":"tenant","tenantId":"..."}`, `{"scope":"personal"}` or `{"scope":"platform"}`. The client then reads the list and the count through the endpoints of section 3, which authorize the read. A personal change concerns every tenant bell.
3. A comment line, `: keep-alive`, goes out every `Notifications:LiveStream:KeepAliveInterval` (20 s), so no proxy closes the stream. Railway closes a response after 5 minutes without data.
4. Before each send, the stream asks again whether the user may read (A9, through the cache). A deleted or a suspended user's stream closes at its next send.
5. The stream closes after `Notifications:LiveStream:Lifetime` (5 minutes), so the client opens a new one with a fresh token. Railway ends a request after 15 minutes, even while data flows.

The live channel is `ILiveChannel` (U6). The worker writes the notification, and its commit queues the signal, which one loop of the process publishes on Valkey's channel `api:live`; the commit never waits for Valkey. A web or a worker process without `ConnectionStrings:Cache` logs one warning at start. Every web process that holds a stream of the user hears it, and writes it to that user's streams. Without a cache connection, the signal stays in the process, which is enough in the role `all`.

## 5. Notification preferences

`GET` and `PUT /v1/me/notification-preferences` (U5). The preferences are the user's, the same in every tenant. They are a table: the rows are the categories, and the columns are the channels, `web` and `email`. Push joins as a third column in step 48, with no change to a row.

- Each channel has a main switch, "All": off sends nothing on that channel, except a required category.
- Each category has one switch for each channel. Its types cannot be chosen one by one (Linear, "Notifications"); a type that needs a switch of its own goes into a category of its own.
- The required category, `account_security`, is on in every channel and locked. The read lists it to everyone, with `required: true`, so a screen can show it locked with its description as its reason, and a person who turned the email off is not surprised by an email. Today's page of both apps shows the email column only, without the locked row, until a later frontend step shows the table. A change to it is refused: 422 `notification_preferences.category_required` (Novu, "Notification Preferences Concepts", critical workflows).
- A category's name and description are written once, in `packages/emails/messages/*.json` (`notificationCategories`). The email's footer reads them from the generated file, and `pnpm generate` writes them into `packages/ui/messages/*.json` (`notificationCategories`), where the preferences form reads them; the endpoint answers the categories by their ids only (U5, D1). Each row also names its types, by the type and its short name in the reader's language (`name` of each type under `notifications`), so the screen can list them under the row, read only.
- Only the categories that can reach the user are listed. Each category declares its audience in the catalog: `account_security` and `access` everyone, `member_invitations` a holder of `tenant.invitations.create` in some tenant, `tenant_plans` a holder of `tenant.owners.manage` in some tenant, `platform_tenants` a system admin. The endpoint reads the user's tenants, then the permissions in each, each in that tenant's transaction (R4).
- Only the choices that differ from the defaults are stored, per user, category and channel, and the main switch of a channel that is off (`ck_notification_preferences_not_default`). A user who never chose has no row. A choice of a category that cannot reach the user is not kept, and a kept choice stays when its category stops reaching the user. A change writes `user.email_preferences_changed` in the platform's log, with no value (K3); the operation keeps its stored name.
- Step 33-backend-4's migration copied each stored email choice into `notification_preferences`: the email's main switch off, and each category whose email was on. The table of the version before, `email_preferences`, keeps its rows until a later release drops it (`ARCHITECTURE.md`, section 13); the code never reads or writes it, but a user's deletion empties it too.

## 6. Deletion

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `DeleteExpiredNotificationsCommand` | the worker, every hour, and the handler itself after a full batch | Notifications, `DeleteExpiredNotificationsHandler` | Yes, Notifications'; it declares nobody |
| `PurgeTenantDataCommand` | the tenant purge (`tenant-purge.md`) | Notifications, `PurgeTenantDataHandler` | Yes, the tenant's |
| `UserDeletedEvent` | ControlPlane, after a user's deletion (`user-deletion.md`) | Notifications, `DeleteUserNotificationsHandler` | Yes, Notifications', which declares the user |

- The retention deletes the notifications older than `Notifications:Retention:Days` (90), read or not, in batches of 1000, one transaction each (U8).
- The tenant purge deletes every notification about the tenant, in every recipient's list, the platform's included. A personal notification is about no tenant and stays.
- A user's deletion deletes their notifications and their notification preferences, and their row of `email_preferences` until that table goes.

Tests: `NotificationFanOutTests` (each type, its recipients and its values, a person by user id, none for the actor, a later Owner, no `member.joined` of the platform's Owner invitation, no `member.removed` of a user's deletion, a copy of an event), `StreamNotificationsTests`, `LiveChannelTests`, `HostRoleTests` (`StreamNotifications_OnAWebAndAWorkerHostWithValkey_TheWorkersNotificationReachesTheWebStream`), `ListNotificationsTests`, `MarkAllNotificationsReadTests`, `MarkNotificationReadTests`, `ListPlatformNotificationsTests`, `GetNotificationPreferencesTests`, `ChangeNotificationPreferencesTests`, `NotificationPreferenceTests`, `NotificationRetentionTests`, `TenantPurgeTests`, `DeleteUserTests`, `OwnNotificationsPolicyTests`, `NotificationCatalogTests`, `NotificationTextTests`, `SendNotificationEmailHandlerTests`, `MigrationStepTests` (`Migrate_OneNotificationTypePerEvent_DeletesEveryNotificationAndRefusesAnOldType`, `Migrate_NotificationPreferencesPerChannel_KeepsTodaysEmailChoicesAndTheOldRows`).

Sources: Hohpe and Woolf, *Enterprise Integration Patterns*, "Publish-Subscribe Channel". Vernon, *Implementing Domain-Driven Design*, Chapter 8. Kleppmann, *Designing Data-Intensive Applications*, Chapter 1 (fan-out on write). Freeman and Pryce, *GOOS*, Chapter 8 (adapters). HTML Living Standard, "Server-sent events". Novu, "Notification Preferences Concepts". Linear, "Notifications".
