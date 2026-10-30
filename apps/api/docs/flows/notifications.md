# Notifications

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` (U1 to U9) and `api-catalog.md`.

Notifications hears an event that another module publishes and tells people. The module that raises the event knows nothing of notifications (Hohpe and Woolf, "Publish-Subscribe Channel"). A notification belongs to one user, and carries the tenant it is about, or none. It reaches the user in the bell, and by email when the type is required or the user's preferences allow it. An open bell hears of it at once, through a live signal that carries no content.

## 1. The types

A type tells of one event and has the name of its event's audit operation, so the same event carries one label in the audit log and in the bell (U9). The one exception, `tenant.owner_joined`, says why in `NotificationCatalog.NamedApart`.

| Type | Event | Recipients | Bell | Category | Required | Values | Link |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `member.role_changed` | `MemberRoleChangedEvent`, `OwnerMadeEvent` | the member, not when they changed their own role | the tenant's | `access` | no | the actor, none when the platform made them an Owner; the old role and the new role | `tenant` |
| `member.removed` | `MemberRemovedEvent` | the removed person, not when they removed themselves | personal: about no tenant, because the person cannot enter it | `access` | yes | the actor | `none` |
| `member.joined` | `MemberJoinedEvent` with `InvitedBy` | the member who invited, while still a member: the acceptance names them only then | the tenant's | `member_invitations` | no | the new member and the role they joined as | `tenant_members` |
| `tenant.plan_changed` | `TenantPlanChangedEvent` | the tenant's Owners at that moment, the holders of `tenant.owners.manage` (A1), but the actor | the tenant's | `tenant_plans` | no | the old plan and the new plan | `tenant_plan` |
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
3. The dispatcher declares the recipient, and writes the notification under an id that the event, the type and the recipient decide, so a copy of the event writes nothing more (O4). In-app cannot be turned off.
4. The dispatcher reads the recipient's email preferences as a value, without a lock. The email goes when the type is required, or when the main switch is on and the category's switch is on, or, without a choice, when the catalog's default for the type is on (U4). Today every default is off.
5. The commit signals the recipient's bell on the live channel (U6). A failed signal is a warning; the next read is right.
6. The email is the one generic notification email: the title and the body that the bell shows, in the recipient's language and time zone, with the names of the event's moment, and a button to the web app, or to the platform app for the platform's notifications (`Notifications:Links`). Its footer names the type's category, or says that a required type always goes. An email that fails is tried again and goes to the dead letter queue (W11); the notification stays.

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
- An item is `{ id, type, category, title, body, link: { kind, tenantId }, createdAt, readAt }`. The title and the body are rendered at the read, in the reader's language and time zone, from the type and the stored values (U2). The read is two queries, as an audit log's (K3): Notifications lists the page as stored, then `GetNotificationNamesQuery` names every person of the page in one call to `IUserDirectory.FindNamesAsync` and renders it. The link is a kind (`none`, `tenant`, `tenant_members`, `tenant_plan`, `tenant_roles`, `tenant_settings`, `platform_tenant`) and the tenant; the web builds the URL.
- Marking read writes no audit entry: it is the reader's own view (K3). Its commit signals the reader's bell, so their other tabs read again.

## 4. The live stream

1. The web's BFF opens `GET /v1/me/notifications/stream` for the signed-in user, with the user's token. The browser never reaches the API.
2. The stream answers `text/event-stream`. Each change of one of the user's bells is one event, `notifications`, whose data is the bell only: `{"scope":"tenant","tenantId":"..."}`, `{"scope":"personal"}` or `{"scope":"platform"}`. The client then reads the list and the count through the endpoints of section 3, which authorize the read. A personal change concerns every tenant bell.
3. A comment line, `: keep-alive`, goes out every `Notifications:LiveStream:KeepAliveInterval` (20 s), so no proxy closes the stream. Railway closes a response after 5 minutes without data.
4. Before each send, the stream asks again whether the user may read (A9, through the cache). A deleted or a suspended user's stream closes at its next send.
5. The stream closes after `Notifications:LiveStream:Lifetime` (5 minutes), so the client opens a new one with a fresh token. Railway ends a request after 15 minutes, even while data flows.

The live channel is `ILiveChannel` (U6). The worker writes the notification, and its commit queues the signal, which one loop of the process publishes on Valkey's channel `api:live`; the commit never waits for Valkey. A web or a worker process without `ConnectionStrings:Cache` logs one warning at start. Every web process that holds a stream of the user hears it, and writes it to that user's streams. Without a cache connection, the signal stays in the process, which is enough in the role `all`.

## 5. Email preferences

`GET` and `PUT /v1/me/email-preferences` (U5). The preferences are the user's, the same in every tenant.

- A main switch, and under it one switch for each category. A required type ignores both, and no category of the screen holds it.
- A category's name and description are written once, in `packages/emails/messages/*.json` (`notificationCategories`). The email's footer reads them from the generated file, and `pnpm generate` writes them into `packages/ui/messages/*.json` (`notificationCategories`), where the preferences form reads them; the endpoint answers the categories by their ids only (U5, D1).
- Only the categories that can reach the user are listed: `access` for everyone, `member_invitations` for a holder of `tenant.invitations.create` in some tenant, `tenant_plans` for an Owner of some tenant, `platform_tenants` for a system admin. The endpoint reads the user's tenants, then the permissions in each, each in that tenant's transaction (R4).
- Only the choices that differ from the defaults are stored, and a user who never chose has no row (`ck_email_preferences_not_default`). A change writes `user.email_preferences_changed` in the platform's log, with no value (K3).

## 6. Deletion

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `DeleteExpiredNotificationsCommand` | the worker, every hour, and the handler itself after a full batch | Notifications, `DeleteExpiredNotificationsHandler` | Yes, Notifications'; it declares nobody |
| `PurgeTenantDataCommand` | the tenant purge (`tenant-purge.md`) | Notifications, `PurgeTenantDataHandler` | Yes, the tenant's |
| `UserDeletedEvent` | ControlPlane, after a user's deletion (`user-deletion.md`) | Notifications, `DeleteUserNotificationsHandler` | Yes, Notifications', which declares the user |

- The retention deletes the notifications older than `Notifications:Retention:Days` (90), read or not, in batches of 1000, one transaction each (U8).
- The tenant purge deletes every notification about the tenant, in every recipient's list, the platform's included. A personal notification is about no tenant and stays.
- A user's deletion deletes their notifications and their email preferences.

Tests: `NotificationFanOutTests` (each type, its recipients and its values, a person by user id, none for the actor, a later Owner, no `member.joined` of the platform's Owner invitation, no `member.removed` of a user's deletion, a copy of an event), `StreamNotificationsTests`, `LiveChannelTests`, `HostRoleTests` (`StreamNotifications_OnAWebAndAWorkerHostWithValkey_TheWorkersNotificationReachesTheWebStream`), `ListNotificationsTests`, `MarkAllNotificationsReadTests`, `MarkNotificationReadTests`, `ListPlatformNotificationsTests`, `GetEmailPreferencesTests`, `ChangeEmailPreferencesTests`, `NotificationRetentionTests`, `TenantPurgeTests`, `DeleteUserTests`, `OwnNotificationsPolicyTests`, `NotificationCatalogTests`, `NotificationTextTests`, `SendNotificationEmailHandlerTests`, `MigrationStepTests` (`Migrate_OneNotificationTypePerEvent_DeletesEveryNotificationAndRefusesAnOldType`).

Sources: Hohpe and Woolf, *Enterprise Integration Patterns*, "Publish-Subscribe Channel". Vernon, *Implementing Domain-Driven Design*, Chapter 8. Kleppmann, *Designing Data-Intensive Applications*, Chapter 1 (fan-out on write). Freeman and Pryce, *GOOS*, Chapter 8 (adapters). HTML Living Standard, "Server-sent events". Novu, "Notification Preferences Concepts". Linear, "Notifications".
