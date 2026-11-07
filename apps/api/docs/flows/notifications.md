# Notifications

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` (U1 to U9) and `api-catalog.md`.

Notifications hears an event that another module publishes and tells people. The module that raises the event knows nothing of notifications (Hohpe and Woolf, "Publish-Subscribe Channel"). A notification belongs to one user, and carries the tenant it is about, or none. It reaches the user on the channels that the catalog fixes for its type: the bell, the email, or both. An open bell hears of it at once, through a live signal that carries no content.

A notification about the person's account or access is transactional: the product fixes its channels in the catalog. Any other notification is activity: the person chooses its channels. The template has only transactional types, so it has no notification settings (U5; the maintainer's decision of 2026-11-01). Notification settings come with a product's first activity type, such as a mention, an assignment or a comment (`ARCHITECTURE.md`, section 12).

## 1. The types

A type tells of one event and has the name of its event's audit operation, so the same event carries one label in the audit log and in the bell (U9). The exceptions, `tenant.owner_joined`, `user.data_export_ready` and the five status types of the Owners, `tenant.*_for_owners`, say why in `NotificationCatalog.NamedApart`.

| Type | Event | Recipients | Bell | Channels | Values | Link |
| --- | --- | --- | --- | --- | --- | --- |
| `member.role_changed` | `MemberRoleChangedEvent`, `OwnerMadeEvent` | the member, not when they changed their own role | the tenant's | web and email | the actor, none when the platform made them an Owner; the old role and the new role | `tenant_settings`, which shows "Your role" to every role |
| `member.removed` | `MemberRemovedEvent` | the removed person, not when they removed themselves | personal: about no tenant, because the person cannot enter it | web and email | the actor | `start`: the start page, which opens the one tenant left, the choice of tenants, or "No access yet" |
| `member.joined` | `MemberJoinedEvent` with `InvitedBy` | the member who invited, while still a member: the acceptance names them only then | the tenant's | web | the new member and the role they joined as | `tenant_members` |
| `tenant.plan_changed` | `TenantPlanChangedEvent` | the tenant's Owners at that moment, the holders of `tenant.owners.manage` (A1), but the actor | the tenant's | web and email | the old plan and the new plan | `tenant_settings`, which shows the plan |
| `tenant.entitlement_override_set` | `EntitlementOverrideSetEvent` | as `tenant.plan_changed` | the tenant's | web | the key, the value, and the end, none for an override without one | by the key |
| `tenant.entitlement_override_removed` | `EntitlementOverrideRemovedEvent` | as `tenant.plan_changed` | the tenant's | web | the key and the plan's value, which applies again | by the key |
| `tenant.owner_joined` | `MemberJoinedEvent` with `FirstOwner` | every system admin but the new Owner | the platform's | web | the new Owner | `platform_tenant` |
| `tenant.suspended`, `tenant.reactivated`, `tenant.reopened` | `TenantSuspendedEvent`, `TenantReactivatedEvent`, `TenantReopenedEvent` | every system admin but the actor | the platform's | web and email | the actor | `platform_tenant` |
| `tenant.closed` | `TenantClosedEvent` | every system admin but the actor | the platform's | web and email | the actor and the date the purge deletes the data (`ReopenableUntil`) | `platform_tenant` |
| `tenant.purged` | `TenantPurgedEvent` | every system admin: the platform acted | the platform's | web and email | none beyond the tenant | `platform_tenant` |
| `tenant.suspended_for_owners`, `tenant.closed_for_owners` | `TenantSuspendedEvent`, `TenantClosedEvent` | the tenant's Owners at that moment; the acting system admin is no member | personal: the tenant door refuses its members now | web and email | none beyond the tenant, and for the closure the date the purge deletes the data (`ReopenableUntil`); no system admin is named, as the tenant's Activity says "the platform" | `start` |
| `tenant.reactivated_for_owners`, `tenant.reopened_for_owners` | `TenantReactivatedEvent`, `TenantReopenedEvent` | as above | personal, where the Owners heard of the suspension or the closure; it keeps the tenant it tells of | web and email | none beyond the tenant | `tenant_settings`: the organisation is open again |
| `tenant.purged_for_owners` | `TenantPurgedEvent` with `OwnerIds` | the Owners the purge read before it took the memberships away | personal, which the purge keeps | web and email | none beyond the tenant | `start` |
| `user.data_export_ready` | `DataExportReadyEvent` | the person who asked for the export: the worker completed it, so the person is told | personal | web and email | the date until which the file can be downloaded (`flows/personal-data-export.md`) | `account_security` |

Every type about a tenant also keeps the tenant's name as it was at that moment. A personal notification is about no tenant, unless its type leads into the tenant: the Owners' reactivated and reopened keep the tenant they tell of, so the web builds the link to it; the CHECK `ck_notifications_tenant` and `Notification.Create` allow it for those types only (the maintainer's decision of 2026-11-06). The bells read by the bell, not by the tenant, and a personal signal names no tenant, so no read changes.

Each type also has an area and an actor in the catalog (U1). The list shows them beside an item; they are display labels only.

| Area | Words in English, Turkish, Arabic | Types | Actor |
| --- | --- | --- | --- |
| `members` | Members, Üyeler, الأعضاء | `member.joined` | the new member, who accepted the invitation |
| `your_access` | Your access, Erişiminiz, وصولك | `member.role_changed`, `member.removed`, and the Owners' five status types | the one who changed the role or removed the member; none when the platform made an Owner or changed the status |
| `organisation_plan` | Organisation plan, Kuruluş planı, خطة المؤسسة | `tenant.plan_changed`, `tenant.entitlement_override_set`, `tenant.entitlement_override_removed` | none: the platform acted |
| `organisations` | Organisations, Kuruluşlar, المؤسسات | `tenant.owner_joined` and every status type | the new Owner, who accepted; the system admin who changed the status; none for `tenant.purged` |
| `your_account` | Your account, Hesabınız, حسابك | `user.data_export_ready` | none: the worker completed the export |

- An override leads to the page of its key: `members.max` to the members (`tenant_members`), `roles.custom` and `roles.custom.max` to the roles (`tenant_roles`), any other key to the tenant's general settings (`tenant_settings`).
- A value keeps a person by user id only (K3). The read names them now, so a deleted user reads as "Deleted user" in the reader's language, and a renamed one by the new name (U2). A tenant's, a plan's and a role's name stay as they were at that moment.
- An entitlement's value is a number, `unlimited`, `on`, `off`, or `not_set` when the plan has no value of the key; each word is written in the language. A date is written in the reader's language and time zone.
- Nobody is told of their own action, and a deleted or suspended user is told nothing (`NotificationFanOut`).
- The platform's Owner invitation tells no member: its acceptance sends `tenant.owner_joined` to the system admins and no `member.joined`. The platform removes a member only in the user's own deletion, which publishes no `MemberRemovedEvent`, so `member.removed` needs no text of the platform.
- A failure, such as a stuck purge or a dead letter, is no notification: the error tracker alerts it (section 7, "Error tracking").
- A plan's edit tells nobody. It would tell the Owners of every tenant on the plan; the platform's audit log records it, the known limit of `reading-the-audit-logs.md`.
- An event field that a type needs is optional, so both versions read each other's events: `TenantName` on the three member events, `InvitedBy`, `FirstOwner`, `Role` and `RoleBuiltIn` on `MemberJoinedEvent`, `ReopenableUntil` on `TenantClosedEvent`, `PlanValue` on `EntitlementOverrideRemovedEvent`, and `OwnerIds` on `TenantPurgedEvent` (step 37a). An event of a version without the field tells nobody of the type that needs it. `FirstOwner` is true when the member joined with a role that holds `tenant.owners.manage` and the tenant had no Owner: a new tenant's first Owner, or an Owner the platform invited into a tenant without one.

## 2. From the event to the bell

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| the event of a type (section 1) | ControlPlane, through the outbox (O1) | Notifications, `Notify{Type}Handler` in the resource folder of the event | Yes, ControlPlane's, in the event's tenant or in none: it reads the recipients through `IUserDirectory` and `IMemberDirectory` |
| `DeliverNotificationCommand`, one for each recipient, sent without a tenant | the handler of the type | Notifications, `DeliverNotificationHandler`, the dispatcher | Yes, Notifications', which declares the recipient (R4) |
| `SendNotificationEmailCommand` | the dispatcher, when the email goes | Notifications, `SendNotificationEmailHandler` | No: it calls the email service (section 3, "Handlers and pipeline") |

1. ControlPlane commits a command and its event (O1).
2. The handler of each type names the recipients, through ControlPlane's contracts, and the values the texts need: a person by user id, any other value as it is at that moment. It never names a channel. The fan-out reads the names of the people once, for the email, which renders at its send; the row never keeps them. It sends one `DeliverNotificationCommand` for each recipient (U3; Kleppmann, Chapter 1, fan-out on write). Someone who becomes a recipient later sees no older notification.
3. The dispatcher declares the recipient and takes the type's channels from the catalog only (U4). The person chooses no channel. When a person's access or the organisation's status changes, the email goes too, because that person may not be able to open the app and see the bell. Team news goes to the bell only (U5). The table above names each type's channels.
4. When the web goes, the dispatcher writes the notification under an id that the event, the type and the recipient decide, so a copy of the event writes nothing more (O4). The email's key, the notification's id, keeps a copy of the event to one email.
5. The commit of a written notification signals the recipient's bell on the live channel (U6). A failed signal is a warning; the next read is right.
6. The email is the one generic notification email: the title and the body that the bell shows, in the recipient's language and time zone, with the names of the event's moment, and a button to the web app, or to the platform app for the platform's notifications (`Notifications:Links`). Its footer is its type's own sentence of why the email came, in `messages/*.json` beside the type's texts (U5): a change of the person's access, an Owner of the organisation, or a system admin and an organisation's status. It may name a value the type always has, such as the organisation where a role changed. The footer has no link to settings. An email that fails is tried again and goes to the dead letter queue (W11); the notification stays.

A notification reaches an open bell within about one to two seconds of the commit: the outbox delivers the event, then the delivery, through the worker's queue (W5).

## 3. The bells

| Bell | Endpoint | Shows | Access |
| --- | --- | --- | --- |
| The tenant web's | `GET /v1/tenants/{tenantId}/notifications` | that tenant's and the personal ones | `tenant.profile.read`, every member; NotPlanGated |
| The personal one | `GET /v1/me/notifications` | the personal ones, for a signed-in user without a tenant (the "no access" and the select-organisation pages) | signed in |
| The platform's | `GET /v1/platform/notifications` | the platform's | `platform.tenants.read`, every system admin |

- A read declares the reader, never the tenant (U7). The tenant door proved the membership, and the route's tenant only selects the reader's own rows. A person removed from a tenant fails the tenant door, so they no longer see that tenant's notifications. A personal one stays in every tenant's bell.
- Each bell has its unread count (`.../unread-count`) and marks everything it shows read (`POST .../read`), and nothing of another tenant. `POST /v1/me/notifications/{notificationId}/read` opens one notification of any bell; another user's answers 404 `notification.not_found`. There is no "mark unread", no archive and no single delete.
- A list pages by a cursor, newest first, as the audit logs do (K5). `?unread=true` reads only the unread ones, by the same cursor: the Unread tab. `?unread=false` reads only the read ones, and no filter the whole bell. A value that is no boolean answers 400 `request.invalid`.
- An item is `{ id, type, title, body, area, actorName, tenantName, link: { kind, tenantId }, createdAt, readAt, unread }`. `unread` is true while `readAt` is none; the read derives it and stores nothing more, as GitHub's notifications answer both `unread` and `last_read_at`. The title and the body are rendered at the read, in the reader's language and time zone, from the type and the stored values (U2). The area is the type's word in the reader's language. The actor is named now, as every person of the values: a deleted one reads as "Deleted user" in the reader's language; none when the platform acted. The tenant's name is the stored one, as it was at that moment, so the platform's list needs no read of the tenant for each item; a personal notification also names the organisation it tells of. The web keeps no word of these. The read is two queries, as an audit log's (K3): Notifications lists the page as stored, then `GetNotificationNamesQuery` names every person of the page in one call to `IUserDirectory.FindNamesAsync` and renders it. The link is a kind (`start`, `tenant_members`, `tenant_roles`, `tenant_settings`, `platform_tenant`) and the tenant, none for `start`; the web builds the URL.
- Marking read writes no audit entry: it is the reader's own view (K3). Its commit signals the reader's bell, so their other tabs read again.

## 4. The live stream

1. The web's BFF opens `GET /v1/me/notifications/stream` for the signed-in user, with the user's token. The browser never reaches the API.
2. The stream answers `text/event-stream`. Each change of one of the user's bells is one event, `notifications`, whose data is the bell only: `{"scope":"tenant","tenantId":"..."}`, `{"scope":"personal"}` or `{"scope":"platform"}`. The client then reads the list and the count through the endpoints of section 3, which authorize the read. A personal change concerns every tenant bell.
3. A comment line, `: keep-alive`, goes out every `Notifications:LiveStream:KeepAliveInterval` (20 s), so no proxy closes the stream. Railway closes a response after 5 minutes without data.
4. Before each send, the stream asks again whether the user may read (A9, through the cache). A deleted or a suspended user's stream closes at its next send.
5. The stream closes after `Notifications:LiveStream:Lifetime` (5 minutes), so the client opens a new one with a fresh token. Railway ends a request after 15 minutes, even while data flows.

The live channel is `ILiveChannel` (U6). The worker writes the notification, and its commit queues the signal, which one loop of the process publishes on Valkey's channel `api:live`; the commit never waits for Valkey. A web or a worker process without `ConnectionStrings:Cache` logs one warning at start. Every web process that holds a stream of the user hears it, and writes it to that user's streams. Without a cache connection, the signal stays in the process, which is enough in the role `all`.

## 5. Deletion

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `DeleteExpiredNotificationsCommand` | the worker, every hour, and the handler itself after a full batch | Notifications, `DeleteExpiredNotificationsHandler` | Yes, Notifications'; it declares nobody |
| `PurgeTenantDataCommand` | the tenant purge (`tenant-purge.md`) | Notifications, `PurgeTenantDataHandler` | Yes, the tenant's |
| `UserDeletedEvent` | ControlPlane, after a user's deletion (`user-deletion.md`) | Notifications, `DeleteUserNotificationsHandler` | Yes, Notifications', which declares the user |

- The retention deletes the notifications older than `Notifications:Retention:Days` (90), read or not, in batches of 1000, one transaction each (U8).
- The tenant purge deletes every notification about the tenant, in every recipient's list, the platform's included: the Owners' reactivated and reopened go too, because their link leads nowhere then. Any other personal notification is about no tenant and stays.
- A user's deletion deletes their notifications.

Tests: `NotificationFanOutTests` (each type, its recipients and its values, the Owners' five status types, a person by user id, none for the actor, a later Owner, no `member.joined` of the platform's Owner invitation, no `member.removed` of a user's deletion, a copy of an event, a type of the web only, a type of the web and the email, a changed role by email), `StreamNotificationsTests`, `LiveChannelTests`, `HostRoleTests` (`StreamNotifications_OnAWebAndAWorkerHostWithValkey_TheWorkersNotificationReachesTheWebStream`), `ListNotificationsTests`, `ListMyNotificationsTests` (`ListMyNotifications_AnOwnersReopenedAndSuspendedOrganisation_LeadToItsSettingsAndToTheStartPage`), `MarkAllNotificationsReadTests`, `MarkNotificationReadTests`, `ListPlatformNotificationsTests`, `NotificationRetentionTests`, `TenantPurgeTests`, `DeleteUserTests`, `OwnNotificationsPolicyTests`, `NotificationCatalogTests`, `NotificationTextTests`, `SendNotificationEmailHandlerTests`, `MigrationStepTests` (`Migrate_OneNotificationTypePerEvent_DeletesEveryNotificationAndRefusesAnOldType`, `Migrate_RemoveNotificationPreferences_DropsBothPreferenceTablesAndKeepsTheNotifications`).

Sources: Hohpe and Woolf, *Enterprise Integration Patterns*, "Publish-Subscribe Channel". Vernon, *Implementing Domain-Driven Design*, Chapter 8. Kleppmann, *Designing Data-Intensive Applications*, Chapter 1 (fan-out on write). Freeman and Pryce, *GOOS*, Chapter 8 (adapters). HTML Living Standard, "Server-sent events".
