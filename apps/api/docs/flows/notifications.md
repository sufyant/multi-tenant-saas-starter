# Notifications

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` (U1 to U8) and `api-catalog.md`.

Notifications hears an event that another module publishes and tells people. The module that raises the event knows nothing of notifications (Hohpe and Woolf, "Publish-Subscribe Channel"). A notification belongs to one user, and carries the tenant it is about, or none. It reaches the user in the bell, and by email when the type is required or the user's preferences allow it. An open bell hears of it at once, through a live signal that carries no content.

## 1. The seven types

| Type | Event | Recipients | Bell | Category | Required |
| --- | --- | --- | --- | --- | --- |
| `member.role_changed` | `MemberRoleChangedEvent`, `OwnerMadeEvent` | the member, not when they changed their own role | the tenant's | `access` | no |
| `member.removed` | `MemberRemovedEvent` | the removed person, not when they removed themselves | personal: about no tenant, because the person cannot enter it | `access` | yes |
| `invitation.accepted` | `MemberJoinedEvent` with `InvitedBy` | the member who invited, while still a member: the acceptance names them only then | the tenant's | `member_invitations` | no |
| `tenant.plan_changed` | `TenantPlanChangedEvent`, `EntitlementOverrideSetEvent`, `EntitlementOverrideRemovedEvent` | the tenant's Owners at that moment, the holders of `tenant.owners.manage` (A1), but the actor | the tenant's | `tenant_plans` | no |
| `tenant.owner_joined` | `MemberJoinedEvent` with `FirstOwner` | every system admin but the new Owner | the platform's | `platform_tenants` | no |
| `tenant.status_changed` | `TenantSuspendedEvent`, `TenantReactivatedEvent`, `TenantClosedEvent`, `TenantReopenedEvent` | every system admin but the actor | the platform's | `platform_tenants` | no |
| `tenant.purged` | `TenantPurgedEvent` | every system admin: the platform acted | the platform's | `platform_tenants` | no |

- Nobody is told of their own action, and a deleted or suspended user is told nothing (`NotificationFanOut`).
- A failure, such as a stuck purge or a dead letter, is no notification: the error tracker alerts it (section 7, "Error tracking").
- A plan's edit tells nobody. It would tell the Owners of every tenant on the plan; the platform's audit log records it, the known limit of `reading-the-audit-logs.md`.
- A step 33 event field is optional, so both versions read each other's events: `TenantName` on the three member events, and `InvitedBy` and `FirstOwner` on `MemberJoinedEvent`. An event of the version before has none, and tells nobody of these types. `FirstOwner` is true when the member joined with a role that holds `tenant.owners.manage` and the tenant had no Owner: a new tenant's first Owner, or an Owner the platform invited into a tenant without one.

## 2. From the event to the bell

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| the event of a type (section 1) | ControlPlane, through the outbox (O1) | Notifications, `Notify{Type}Handler` in the resource folder of the event | Yes, ControlPlane's, in the event's tenant or in none: it reads the recipients through `IUserDirectory` and `IMemberDirectory` |
| `DeliverNotificationCommand`, one for each recipient, sent without a tenant | the handler of the type | Notifications, `DeliverNotificationHandler`, the dispatcher | Yes, Notifications', which declares the recipient (R4) |
| `SendNotificationEmailCommand` | the dispatcher, when the email goes | Notifications, `SendNotificationEmailHandler` | No: it calls the email service (section 3, "Handlers and pipeline") |

1. ControlPlane commits a command and its event (O1).
2. The handler of each type names the recipients, through ControlPlane's contracts, and the values the texts need, as they are at that moment. It never names a channel. It sends one `DeliverNotificationCommand` for each recipient (U3; Kleppmann, Chapter 1, fan-out on write). Someone who becomes a recipient later sees no older notification.
3. The dispatcher declares the recipient, and writes the notification under an id that the event, the type and the recipient decide, so a copy of the event writes nothing more (O4). In-app cannot be turned off.
4. The dispatcher reads the recipient's email preferences. The email goes when the type is required, or when the main switch is on and the category's switch is on, or, without a choice, when the catalog's default for the type is on (U4). Today every default is off.
5. The commit signals the recipient's bell on the live channel (U6). A failed signal is a warning; the next read is right.
6. The email is the one generic notification email: the title and the body that the bell shows, in the recipient's language, and a button to the web app, or to the platform app for the platform's notifications (`Notifications:Links`). An email that fails is tried again and goes to the dead letter queue (W11); the notification stays.

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
- An item is `{ id, type, category, title, body, link: { kind, tenantId }, createdAt, readAt }`. The title and the body are rendered at the read, in the reader's language, from the type and the stored values (U2). The link is a kind (`none`, `tenant`, `tenant_members`, `tenant_plan`, `platform_tenant`) and the tenant; the web builds the URL.
- Marking read writes no audit entry: it is the reader's own view (K3). Its commit signals the reader's bell, so their other tabs read again.

## 4. The live stream

1. The web's BFF opens `GET /v1/me/notifications/stream` for the signed-in user, with the user's token. The browser never reaches the API.
2. The stream answers `text/event-stream`. Each change of one of the user's bells is one event, `notifications`, whose data is the bell only: `{"scope":"tenant","tenantId":"..."}`, `{"scope":"personal"}` or `{"scope":"platform"}`. The client then reads the list and the count through the endpoints of section 3, which authorize the read. A personal change concerns every tenant bell.
3. A comment line, `: keep-alive`, goes out every `Notifications:LiveStream:KeepAliveInterval` (20 s), so no proxy closes the stream. Railway closes a response after 5 minutes without data.
4. Before each send, the stream asks again whether the user may read (A9, through the cache). A deleted or a suspended user's stream closes at its next send.
5. The stream closes after `Notifications:LiveStream:Lifetime` (5 minutes), so the client opens a new one with a fresh token. Railway ends a request after 15 minutes, even while data flows.

The live channel is `ILiveChannel` (U6). The worker writes the notification, and its commit publishes the signal on Valkey's channel `api:live`. Every web process that holds a stream of the user hears it, and writes it to that user's streams. Without a cache connection, the signal stays in the process, which is enough in the role `all`.

## 5. Email preferences

`GET` and `PUT /v1/me/email-preferences` (U5). The preferences are the user's, the same in every tenant.

- A main switch, and under it one switch for each category. A required type ignores both, and no category of the screen holds it.
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

Tests: `NotificationFanOutTests` (each type, its recipients and none for the actor, a later Owner, a copy of an event), `StreamNotificationsTests`, `LiveChannelTests`, `HostRoleTests` (`StreamNotifications_OnAWebAndAWorkerHostWithValkey_TheWorkersNotificationReachesTheWebStream`), `ListNotificationsTests`, `MarkAllNotificationsReadTests`, `MarkNotificationReadTests`, `ListPlatformNotificationsTests`, `GetEmailPreferencesTests`, `ChangeEmailPreferencesTests`, `NotificationRetentionTests`, `TenantPurgeTests`, `DeleteUserTests`, `OwnNotificationsPolicyTests`, `NotificationCatalogTests`, `NotificationTextTests`, `SendNotificationEmailHandlerTests`.

Sources: Hohpe and Woolf, *Enterprise Integration Patterns*, "Publish-Subscribe Channel". Vernon, *Implementing Domain-Driven Design*, Chapter 8. Kleppmann, *Designing Data-Intensive Applications*, Chapter 1 (fan-out on write). Freeman and Pryce, *GOOS*, Chapter 8 (adapters). HTML Living Standard, "Server-sent events". Novu, "Notification Preferences Concepts". Linear, "Notifications".
