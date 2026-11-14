# Roadmap

This document is the plan of the template's steps. Linear no longer tracks them. Each step has its scope, its decided points and its status: `done`, `in progress` or `todo`. A step's pull request marks its own row in section 1 and its own section. A decided point is closed: reopen it only when the maintainer asks. Section 17 holds the order of the work, the open work, and which threads may run at the same time. `docs/decisions.md` holds the decisions that belong to no step.

## 1. Status

| Step | Name | Status |
| --- | --- | --- |
| 34 | Design | done, but the handoff package is todo |
| 35 | Cleanup, the shell and the menu | done |
| 36 | Personal pages | done |
| 37a | The web's organisation pages | done |
| 37b | The platform's pages | done |
| 37c | Audit retention | done |
| 37d | Dependent limits | done |
| 38 | Terms of use | in progress |
| 39 | Feature flags | todo |
| 40 | Billing | todo |
| 41 | Extension points | todo |
| 42 | Observability | todo |
| 43 | Accessibility and end-to-end tests | todo |
| 44 | Marketing site | todo |
| 45 | Mobile app | todo |
| 46 | Environments and infrastructure | todo |
| 47 | Production | todo |
| Later | Template 1.1 and after | todo |

## 2. Step 34: Design

Status: done, but the handoff package is todo.

- One Figma file holds the design of the web app and the platform app, on desktop and phone, in light and dark, in English and Arabic. Its key is `3Ev111okX8xCBwR9B3hyBM`.
- The Figma seat stays one more month, while steps 38 to 40 still need frames: it ends about 29 Dec 2026, not 29 Nov (the maintainer's decision of 2026-11-14).
- The handoff package goes into `docs/design/` before the Figma seat ends, by about 24 Dec 2026.
- The handoff package holds the tokens, the patterns, the components, the sample data, and a spec and an image for each screen.

## 3. Step 35: Cleanup, the shell and the menu

Status: done.

35-backend:

- The notification preferences are removed. Every notification type of the template is transactional: it is about the person's account or access. The product fixes each type's channels in the catalog (the maintainer's decision of 2026-11-01).
- The channel rule: when a person's access or the organisation's status changes, the email goes too, because that person may not be able to open the app. Team news goes to the bell only. `member.removed`, `member.role_changed`, `tenant.plan_changed`, `tenant.suspended`, `tenant.reactivated`, `tenant.closed`, `tenant.reopened` and `tenant.purged` go by bell and email; `member.joined`, `tenant.owner_joined`, `tenant.entitlement_override_set` and `tenant.entitlement_override_removed` by bell only (the maintainer's decision of 2026-11-03).
- Each email type has its own footer that says why the email came, in three languages.
- This roadmap goes into the repository.
- The documents use the step numbers of this roadmap.

35-frontend:

- The new shell in both apps, on desktop and phone: the product-first sidebar, the Settings layout, the account menu with the app's version, the command menu, the environment strip and the connection bar.
- The account menu shows the image's commit as the version, as Sentry names it.
- The environment strip shows Staging, Preview or Local. Production shows no strip.
- The states: 403, 404, session ended, and changed while open.
- The shared patterns: dates with the time zone in a tooltip, an em dash for no value, truncation with a tooltip, the copy button, the Pill tones, and the neutral Label for roles.
- More shared patterns: "+" only on create and add buttons, one bar at a time, the rules for long text, and a pseudo-localization test.
- The Privacy link, and `privacyUrl` and `statusUrl` in the brand settings.

35-frontend-fix:

- The design foundation from the Figma file: the tokens, the fonts (Instrument Sans, JetBrains Mono, Noto Sans Arabic, from their Fontsource packages, so the build makes no request to Google) and the shared components of the Components page. The pages keep their layouts until steps 36, 37a and 37b (the maintainer's decision of 2026-11-04).
- The shell as the design draws it: the organisation's name and the role, search inside the sidebar, Notifications as a row, and "Staging" on the strip.
- Home: "Welcome" with the whole name and a card for each page the person can open (4.01, 4.02). Its terms notice (4.03) comes with step 38.
- CI keeps the screenshots of each pull request for seven days, for the review against the design.
- Only the design's breakpoints: Tailwind's own are removed, and the architecture test fails on them.

35-backend-2:

- The bell's list for the design's Notifications popover. Each item answers `unread`, derived from `readAt`. The three list reads take the filter `unread`: `true` for the Unread tab, `false` for the read ones, with the same cursor (the maintainer's decision of 2026-11-04, after N7h and K5).
- Each item answers its area ("Members", "Your access", "Organisation plan", "Organisations") in the reader's language, the person who acted, and the organisation's name. The area is a property of each type in the catalog, with its words in English, Turkish and Arabic. It is a display label only.
- The platform's item writes the actor's name only, without "accepted" (the maintainer's decision of 2026-11-04).

35-frontend-fix-2:

- The notifications list as the design draws it, in both apps: each item is its title and one line of the organisation and the relative time, without the body. Its states: empty, loading, error, offline and the end of the list, and the phone's full screen. Mark all as read and marking one read on open stay. Done.
- In the tenant web, "Page not found" and "You can't open this page" sit 120 px below the top bar (2.15, 2.40). The platform keeps its frames' 40 px (2.22). Done.
- Then the frontend adds the Unread and All tabs, the area and the actor from 35-backend-2. The Unread tab lists only unread items (the maintainer's decision of 2026-11-04). Done.

35-frontend-fix-3:

- The bells' Unread and All tabs in both apps. An item read while the Unread tab is open stays in its place, drawn as read, until the tab changes or the bell closes (the maintainer's decision of 2026-11-04). Done.
- Each item's line from the API's words: the organisation, the area and the time in the tenant web; the area, the person who acted and the time on the platform. Done.
- The platform's phone opens its page of notifications under the top bar (3.08). The tenant web keeps its full screen (3.14). Done.
- "You can't open this page" has its icon tile (2.15). A phone's content starts 16 px under the top bar in both apps (2.43, 2.25). Done.

35-skills:

- The repository's own agent skills, each kept only when its evaluation showed that it changes what an agent does: `building-screens-from-design` and `starting-roadmap-steps` (`docs/agent-skills.md`, `docs/agent-skill-evaluations.md`). Done.
- The rules for skills hold both kinds, upstream and own: the document, `check-agent-skills.sh` and the comment of `agent-skills.yml`. Done.

## 4. Step 36: Personal pages

Status: done.

- Profile: the name, the language and the time zone.
- Account and security: the email change goes through our API. The API owns the email and updates the identity provider.
- Account and security also shows the sign-in methods, the sessions and the optional two-step verification. The frontend reads them from the identity provider through one folder.
- Self-service account deletion.
- Personal data export: it runs in the background. An email and a notification say when it is ready. The person can download it until a date. The file is in object storage.

36-backend (the maintainer's decisions of 2026-11-03):

- File storage: our own port, `IFileStore`, with an adapter on the S3 API in the host. One private R2 bucket per environment, `starter-staging` and `starter-production`, in the EU jurisdiction. Local runs and CI use SeaweedFS in Docker, because MinIO publishes no image any more. The keys hold ids only: `users/{userId}/`, `exports/users/{userId}/{exportId}.zip` and `tenants/{tenantId}/{module}/{fileId}`. The database keeps the key and never a link; a download link works for 5 minutes. `.cloudflare/` holds the buckets and a lifecycle rule that deletes `exports/` after 7 days.
- The personal data export: a ZIP with one JSON file for each topic and a README, from each module's exporter. Ready for 7 days, with a notification and an email.
- Account deletion: immediate, after a 6-digit code to the person's address, through the existing user deletion path, and the user's files with it. Refused for the last Owner of an organisation and for a system admin. A confirmation goes to the address the person had.
- Email change: a 6-digit code to the new address; our record changes, the identity provider takes the address through the outbox, and the old address is told. A7 changes: we own the email, and the identity provider only signs people in.
- One verification code mechanism for both, with its numbers in `apps/api/docs/ARCHITECTURE.md`, section 6.
- The audit operations `user.data_export_requested`, `user.data_export_downloaded`, `user.deletion_requested` and `user.email_changed`.

36-backend-name (the maintainer's decision of 2026-11-06): a person has one name, `name`, trimmed, 1 to 100 characters, in place of a first and a last name. It changes the users table, every contract, the export and the screens. A greeting uses the whole name, and the avatar's initials are the first letters of the name's first and last word. Done.

36-fix-r2-upload (the maintainer's decisions of 2026-11-07): R2 refused the data export's upload, because the SDK signed its body in chunks. The upload now signs its whole body in one part, and a unit test pins the requests that R2 accepts. The readiness check of each API process writes, reads and deletes a file under `health/` of the environment's bucket, so a deploy that cannot use its bucket fails. Done.

## 5. Step 37: Organisation pages

Status: done. It has four parts.

37a, the web's organisation pages:

- The pages before sign-in: choose an organisation, a blocked organisation, a blocked account, a refused join and an ended invitation.
- The stale mentions of "Settings › Notifications" go.
- General, Members, Roles and Activity log in the new design.
- A CSV export of Members and of the Activity log. There is no import.
- HTML emails in three languages, from "Starter <no-reply@starter.example>".
- The words: "Address" (never "slug"), "Seats", "Activity log" and "See the activity log".
- The organisation's Owners also receive tenant.suspended, tenant.reactivated, tenant.closed, tenant.reopened and tenant.purged, by bell and email, with their own texts in three languages.
- The pages move to Pill and Label.

The parts of 37a, in their order. The frames are the Web app page of the design, unless a frame names the Platform app. The pull request of this plan holds the table of every frame with its part, and the table of every value with its field.

37a-design, two frames that the design does not have yet (the maintainer's decision of 2026-11-06):

- A blocked account: 403 `user.suspended` or `user.deleted` (A9).
- A refused join: 422 `invitation.system_admin_cannot_join` (T5).
- The frame 9.r16 takes the number 9.20 in Figma (the maintainer's decision of 2026-11-06).
- It comes before 37a-frontend-1.

37a-backend, the Owners' notifications, the sender, the exports' audit entries and the cities of the time zones, in one pull request (the maintainer's decision of 2026-11-06):

- Five new types tell an organisation's Owners of `tenant.suspended`, `tenant.reactivated`, `tenant.closed`, `tenant.reopened` and `tenant.purged`, with their own texts and footers in three languages. Each is named apart (U9), because the same event tells the system admins with other texts.
- They go to the personal bell and by email (U5). The suspended, closed and purged types lead to the start page; the reactivated and reopened types lead to the organisation (the maintainer's decisions of 2026-11-06).
- An email's sender takes its name from `packages/brand/brand.json` and its address from the setting `Notifications:Resend:FromAddress`, which replaces `Notifications:Resend:From`. `.railway/railway.ts` names the new variable (the maintainer's decision of 2026-11-06).
- Two small commands write the audit entries `members.exported` and `activity_log.exported` in the organisation's Activity log. The web calls each one before it writes the file (the maintainer's decision of 2026-11-06).
- One generated JSON holds each time zone's city in English, Turkish and Arabic, from the `exemplarCity` of Unicode CLDR. The web, the platform and the API's emails read the same file. The file is committed, and the CI job "Generated files: current" checks it (the maintainer's decision of 2026-11-06).
- The Unicode License v3 joins `.github/dependency-review-config.yml`. F18 gets an exception for the city names.
- The parts as built: 37a-backend-1 (the five types `tenant.*_for_owners`, and the sender), 37a-backend-2 (`ExportMembers` and Audit's `ExportAuditLog`) and 37a-time-zone-cities (the CLDR file, with `cldr-bcp47` for the zones' other IANA names). The web's Activity log has a first sentence of each new operation, which its types need; 37a-frontend-3 may rewrite them. Done.

37a-emails, the emails in the new design:

- The invitation, the removal, the data file, the generic notification, the closed organisation and the deletion warning, in light and dark, in three languages (13.01, 13.03 to 13.08, 13.10 to 13.15).
- It waits for 37a-backend.
- As built: one layout for every email, the account emails included. A changed role's footer names the organisation (13.05), and a footer may name a value its type always has. Every notification footer starts as 13.05 does: "You received this because …". The deletion warning has a "Contact support" button (13.07). `pnpm --filter @repo/emails screenshots` takes the shots, and CI keeps them with the apps' shots.
- The data file's email keeps the generic button and no greeting or request date: the recipient's name and the date of the request have no source, and the web has no Account and security page without an organisation (the maintainer's decisions of 2026-11-07). Done.

37a-frontend-sign-in, the sign-in screens of both apps:

- The web's sign-in frames (1.01 to 1.08, 1.20 to 1.22, 1.35 to 1.39, 1.52) and the platform's (1.01 to 1.14 of the Platform app). "Signing you in" (web 1.38, platform 1.13) is built if the code does not have it (the maintainer's decision of 2026-11-06), and its "taking longer" state after 10 seconds (web 1.52, platform 1.14; the maintainer's request of 2026-11-07).
- The code steps keep their submit button (WCAG 2.2, 3.2.2), the code step names no lifetime (F41), the platform's gate keeps Sign out under the card, and its no-access screen offers another account (the maintainer's decisions of 2026-11-06).
- Done.

37a-frontend-1, the pages before an organisation opens:

- Accept invitation, an ended invitation, the invitation for another email, Choose an organisation, No access yet, the blocked organisation, and the two frames of 37a-design (1.13, 1.16 to 1.19, 1.23, 2.13, 2.14, 8.24).
- An ended invitation writes its end date from `expiresAt`, in the person's time zone (F18), in place of the frame's "7 days". A cancelled invitation has its own text (the maintainer's decision of 2026-11-06).
- "Last used" comes from a cookie that holds only the organisation's slug: HttpOnly, Secure, SameSite=Lax. A slug of an organisation the person can no longer open is ignored, and the cookie is deleted (the maintainer's decision of 2026-11-06).
- The stale mentions of "Settings › Notifications" go.
- Step 36's leftover: the Profile's language and time zone fields have the chevron the frames draw, down and not up-down.
- It waits for 37a-design.
- The invitation's page signs a signed-out person in itself, with a title of its own and no organisation, inviter or address, because the preview answers only the invited address (1.13, 1.16, 1.23; the maintainer's decision C of 2026-11-07).
- "No access yet" has no Profile in its account menu until 37a-frontend-4 (1.19; the maintainer's decision A of 2026-11-07).
- Done.

37a-frontend-2, General and Members:

- General (7.01 to 7.05) and Members (8.01 to 8.23, 8.25 to 8.38, 8.41, 8.43, 8.45), with the CSV export of the members.
- The web's server writes the CSV file from the API's pages, as a stream, after the audit call of 37a-backend. A cell that starts with `=`, `+`, `-` or `@` is escaped (OWASP, "CSV Injection"; the maintainer's decision of 2026-11-06).
- "No free seat" (8.43, 8.45) has no billing manager clause. Its way to support is only its "Request a plan change" button, which links to the support contact of `brand.json`; the text holds no link, so both dialogs end alike and 8.43 drops "or ask us for more seats" (the maintainer's decisions of 2026-11-06 and 2026-11-07).
- Leave is not built: 8.18, 8.23 and 8.25 get no shot, because the API has no Leave yet. 37a-frontend-4 builds it (the maintainer's decision of 2026-11-07).
- It waits for 37a-backend.
- Done.

37a-backend-leave, a member leaves an organisation by themselves (the maintainer's decisions of 2026-11-07):

- `LeaveTenant`, `DELETE /v1/tenants/{tenantId}/membership`: a member leaves an active organisation. The last Owner cannot leave, and so neither can an organisation's only member: 409 `member.last_owner`. A suspended or a closed organisation answers 403, as the door does.
- Nobody is told. The Activity log records `member.left`, with the role the member had.
- The web's Activity log has a first sentence of `member.left`; 37a-frontend-3 may rewrite it.
- 37a-frontend-4 builds the Leave flow (8.18, 8.23, 8.25, and 8.24's "You left" toast) on it.

37a-frontend-3, Roles and Activity log:

- Roles (9.01 to 9.17, 9.20) and Activity log (12.01 to 12.08), with the CSV export of the log, written as the members' file is. The log has sentences for `members.exported` and `activity_log.exported`.
- Roles and the Activity log drop their Freshness line, as F59 says (the maintainer's decision of 2026-11-07).
- Roles and the Activity log grow with Load more, with the Loading more row and the live region, as F9 says (the maintainer's decision of 2026-11-07).
- The Activity log's file can be large. Measure the size that `apiDownload` holds in the browser's memory, and ask for a limit or a date range if it needs one (the maintainer's decision of 2026-11-07).
- It waits for 37a-backend.
- As built: a file row is about 105 bytes, so 100,000 entries are 10.5 MB in the browser and 1,000 pages of the API. The file holds the newest 100,000 entries, one constant (`activityFileCap`); the dialog names the cap, and the toast says when a file holds it (the maintainer's decision of 2026-11-07).
- A role in use keeps "Delete role", which opens "{name} is in use" with its members or invitations (9.13, under the maintainer's rule of 37a-frontend-2 that a stopped action stays and says why). A saved role stays open (9.16). The screen's sentence and the file's sentence come from one module, `activity-words.ts`.
- Not built: Billing manager (40a), the billing entries of the log (step 40), and the custom roles limit dialog (9.14, 40a, F56). Done.

37a-frontend-4, the personal pages without an organisation (the maintainer's decision of 2026-11-07):

- The web opens Profile and Account and security at `/settings/profile` and `/settings/account` too, without an organisation, as the platform does (decision 69). `settings` is a reserved slug already, so the API does not change.
- The old addresses under `/[tenantSlug]/settings/` lead to the new ones.
- The design session at the end of 37a draws their frames first.
- Then 1.19's account menu gets Profile.
- The email button and the bell's link of the `account_security` type lead to `/settings/account`.
- Account and security's `user.last_owner` text ends as Leave's does: "Make another member an Owner first, or contact support to close the organisation.", with "contact support" linking to `brand.json`'s `supportUrl`, in three languages (the maintainer's decision of 2026-11-07).
- The Leave flow: 8.18, 8.23, 8.25 and the "You left" notice of 8.24. It waits for 37a-backend-leave (the maintainer's decision of 2026-11-07).
- Settings in the organisation's shell opens General, because Profile opens without an organisation (the maintainer's decision of 2026-11-07).
- A notification of the kind `start`, or of a kind the web does not know, stays in the open organisation and leads to its Home: the bug of staging, where the shell unmounted.
- Done.

37a-shared-components, one Figma component for each shared part (the maintainer's decisions of 2026-11-07):

- First the design session at the end of 37a makes one Figma component for each of these parts of `packages/ui`, and links every frame that draws one to it: Dialog, RowMenu, Table (its card, its row height, its column widths, and ⋯ shown on hover or focus), Tabs (the line across the full width), Search (its icon and width), EmptyState (the gap above its action), Toast (no close button), Avatar (a tint for each person) and the role filter (a menu with `list-filter`, not the Combobox). The sizes are settled in that session. The start is the majority of the frames: a Dialog 440 wide, padding 20 with 16 at the bottom, and a footer band; a RowMenu 240 wide, with 16 px icons and 32 px items.
- Then a code pull request makes `packages/ui` follow them, with the shots of both apps.
- Until then the screens of 37a-frontend-2 keep these parts as `packages/ui` draws them.
- As built: `Dialog` takes `size="wide"` (480 px) where its frame draws Wide: Change role and every dialog of the platform's own features. `RowMenu` puts its one separator before the destructive group and draws the icon an item's frame gives it. No table header has an icon (the maintainer's decision of 2026-11-08). The role filter is `RoleFilter`, and the Combobox loses its filter variant. A person's tint comes from their id. A success or info toast has no close button; a failure keeps it and stays until the person closes it (WCAG 2.2, 2.2.1; the maintainer's decision of 2026-11-08). The platform's tables take the frames' widths only for the columns they share with them; 37b rebuilds those pages. Done.

37a-settings-nav, the organisation's Settings holds only the organisation's pages (the maintainer's decision of 2026-11-08):

- Since 37a-frontend-4, Profile and Account and security open without an organisation, so a click on them in the organisation's settings menu left its shell.
- The organisation's settings menu drops My account, in the design (the Sidebar's Settings layouts, its rail and right-to-left variants, and the phone's Settings drawer) and in the web. Profile and Account and security open from the account menu, as GitHub separates them.
- The command menu keeps "Settings · My account", as frames 2.04 and 2.05 draw it. The platform keeps My account in its settings menu (37b).
- Done.

37b, the platform's pages:

- Organisations, Users, Plans, Activity log and System admins in the new design.
- At its end, Status, Tag and SubNav are deleted.
- The platform's organisation initial (`apps/platform/src/shared/initial.tsx`) becomes the shared tile of `packages/ui` where its frames draw the same tile, or the pull request says how they differ (the maintainer's decision of 2026-11-07).
- The platform's lists drop their Freshness line too (F59). When no screen uses the Freshness component any more, it is deleted from `packages/ui` and from the Figma Components page (the maintainer's decision of 2026-11-07).
- The platform's lists follow the same F9 and F10 rule (a "Load more" button, no page in the URL), or 37b-start asks when their frames differ (the maintainer's decision of 2026-11-07).
- Not 37b: the plan's prices and meters, and the limits of a feature that is off (6.05's "Turn on custom roles to set this limit."): 40a. Billing: step 40 (the maintainer's decision of 2026-11-08).

The parts of 37b, in their order. The frames are the Platform app page of the design. The pull request of this plan holds the table of every frame with its part, and the table of every value with its field.

37b-backend, the fields the frames need, in one pull request (the maintainer's decisions of 2026-11-08):

- `ListTenants` and `ListUsers` answer a count for each status, and their `status` filter takes several values. The Organisations tabs are All (every status but `purged`), Active, Suspended, Closed (`closed` and `purging`) and Deleted (`purged`), each with its count (4.01, 4.05, 4.43). The Users tabs are All, Active and Suspended, each with its count (5.01).
- `GetTenant` answers `createdBy`, `suspendedAt`, `suspendedBy`, `closedBy` and `purgeStartedAt`. A name is `{ name, deleted }`, as `GetUser.suspendedBy` is, and null for a seed (4.09, 4.11, 4.20, 4.23, 4.25, 4.26).
- The Owner invitation answers `sentAt` (4.11).
- A system admin answers `systemAdminGrantedBy` `{ name, deleted }`, null for a root's first entry (10.15).
- `SuspendUser` takes `{ "reason" }`, required, with the rule of `SuspendTenant`. `GetUser` answers `suspensionReason`, which `ReactivateUser` clears (5.07, 5.10). The platform's `user.suspended` entry holds no reason: the reason is personal data and the platform's log stays after the user's deletion (A8; the maintainer's decision of 2026-11-08, in 37b-backend). The platform's suspend dialog of a user asks for it in 37b-backend, and 37b-frontend-3 draws it (the maintainer's decision of 2026-11-08).
- `ListTenants` and `GetTenant` answer `pendingInvitationCount`: the pending invitations that have not expired at the moment of the read, as the web counts them and as the seats count them. It is `TenantSummary.PendingInvitationsAt(now)` over the summary's `pending_invitation_expiries`, so it needs no new column and no write when an invitation expires (K10, R12; the maintainer's decision of 2026-11-08) (4.01, 4.09, 4.15).
- `ListTenants` answers each tenant's plan, `{ id, name }` (4.43).
- Each list stays one query for its page and one for its total (K5), and a test counts the SQL commands.
- The status counts are the totals of each status: the search text and the other filters do not change them, as the frames draw (web 8.06, platform 4.04) and as the web's Members counts. The page and its total follow every filter (the maintainer's decision of 2026-11-10, which replaces the line of 2026-11-08 that the counts follow the search and every other filter but the status). 37b-backend-counts made the change: the counts, the total and the page are one query each.
- Each "by" and "at" field is null for a seed and for every event before its column exists. The screen then shows no "by" part, or no row. Nothing is backfilled from the Activity log (the maintainer's decision of 2026-11-08).
- The suspension reason is personal data about the user. It is in the user's data export, and it goes when the account is deleted. The suspended person does not see it, as the platform's reason for a tenant ("Only the platform sees it") (the maintainer's decision of 2026-11-08).
- The migration is an expansion: the CHECKs that tie the new columns to the old ones come in 37b-backend-contract (`docs/deployment.md`, section 8).
- Done.

37b-backend-contract, the CHECKs of 37b-backend's columns (the maintainer's decision of 2026-11-08):

- It waits for one deploy: 37b-backend has run on staging and on production, so no running version writes the old columns without the new ones.
- One migration of ControlPlane. It first clears what the version before left during the deploy, then adds the CHECKs (R15):
  - `UPDATE control_plane.tenants SET suspended_at = NULL, suspended_by = NULL WHERE suspension_reason IS NULL`
  - `UPDATE control_plane.tenants SET closed_by = NULL WHERE closed_at IS NULL`
  - `UPDATE control_plane.tenants SET purge_started_at = NULL WHERE status <> 'Purging'`
  - `UPDATE control_plane.tenants SET created_by = NULL WHERE status = 'Purged'`
  - `UPDATE control_plane.users SET suspension_reason = NULL WHERE suspended_at IS NULL OR deleted_at IS NOT NULL`
  - `UPDATE control_plane.tenant_summaries SET owner_invitation_sent_at = NULL WHERE owner_invitation_status IS NULL`
- The CHECKs:
  - `ck_tenants_suspension`: `(suspended_at IS NULL) = (suspended_by IS NULL) AND (suspended_at IS NULL OR suspension_reason IS NOT NULL)`
  - `ck_tenants_closed_by_with_closure`: `closed_by IS NULL OR closed_at IS NOT NULL`
  - `ck_tenants_purge_started_while_purging`: `purge_started_at IS NULL OR status = 'Purging'`
  - `ck_tenants_purged_is_a_tombstone`: `status <> 'Purged' OR (purged_at IS NOT NULL AND closed_at IS NULL AND suspension_reason IS NULL AND created_by IS NULL)`
  - `ck_users_suspension_reason_while_suspended`: `suspended_at IS NOT NULL OR suspension_reason IS NULL`
  - `ck_users_deleted_keeps_no_personal_data`: `deleted_at IS NULL OR (email IS NULL AND name IS NULL AND time_zone IS NULL AND suspension_reason IS NULL)`
  - `ck_tenant_summaries_owner_invitation_sent_with_it`: `owner_invitation_sent_at IS NULL OR owner_invitation_status IS NOT NULL`
- `MigratedSchemaTests.WriteRow_BreakingAControlPlaneCheck_IsRefusedByThatCheck` gets a row that breaks each, and the open item of 37b-backend leaves section 13 of `apps/api/docs/ARCHITECTURE.md`.
- As built: the migration `TieWhoAndWhenToTheirEvents`. `MigrationStepTests.Migrate_TieWhoAndWhenToTheirEvents_ClearsWhatTheVersionBeforeLeftAndAddsTheChecks` seeds the rows the version before leaves, migrates, and shows them cleared and the CHECKs in place. Staging ran 37b-backend first; production did not exist yet. Done.

37b-emails, the platform's emails against their frames:

- 14.01, 14.03, 14.04 and 14.05, compared with the email shots of 37a-emails.
- The Owner invitation names the brand, not the system admin, and keeps its expiry as a moment with its zone (closed rules, 37a-emails). 14.02 is not built: `tenant.owner_joined` goes to the bell only (the maintainer's decision of 2026-11-03).
- 14.05 says "address", never "slug" (the words of 37a). The footers of 14.03 to 14.05 follow their frames, in three languages.
- It waits for nothing.
- As built: the Owner invitation already named the brand and wrote its expiry as a moment with its zone, so 14.01 changed no code. Every status type of the platform has the footer "You received this because you are a system admin.", reactivated and reopened too, which have no frame. `tenant.purged` says "address" ("adres", "العنوان", the words of the web's General page). `pnpm screenshots` takes 14.01 and 14.03 to 14.05. Done.

37b-frontend-1, Plans and the Activity log:

- Plans (6.01 to 6.12, 6.17, 6.18) without their prices and meters, and the Activity log (7.01 to 7.08) without its billing sentences.
- Saving a plan keeps the person on the plan, in its saved state (6.08); a new plan opens on its own page (6.12) (the maintainer's decision of 2026-11-07). Save is disabled until a change, as the web's role editor.
- `LoadMore` moves from `apps/web/src/shared/` to `packages/ui`, so both apps use one; F9 says so. A failed older page shows its text and Try again under the entries (7.06); a broken cursor shows a Banner, "Starting from the newest activity." (7.07).
- It waits for nothing.
- As built: `LoadMore` and the paging helpers are in `packages/ui` (`components/load-more/`, `lib/pages.ts`). The Plans list grows with Load more over `ListPlans`' infinite options and shows Created. The plan editor draws each area as a card of rows (Plan, Members, Roles), and "Members limit" is "Seats" on every screen of the platform. The impact dialog has no search. A page of one record names its section and the record in the breadcrumb ("Plans › Team"), the Users and Organisations pages too (5.05, 6.04). On a phone a plan opens as its summary with Edit plan. Done.

37b-frontend-2, Organisations:

- Organisations (4.01 to 4.26, 4.43, 4.44, 4.46) and "Organisation not found" (2.23), without the billing frames, the Usage tab and the Feature flags card.
- The list grows with Load more, with no page in the URL (F9, F10); the tabs and their counts come from 37b-backend.
- The organisation's Activity log tab shows the platform's entries about it only, because the platform never reads a tenant's own log (api-catalog, `ListPlatformAuditEntries`).
- The second tab is "Plan" until step 40 names it "Plan and billing".
- Suspend asks for the reason only, on every size (4.19; the maintainer's decision of 2026-11-08).
- It waits for 37b-backend and 37b-frontend-1.
- As built: the list shows Name, Status, Address and Members ("10 members · 2 invited"), and the tabs count from `ListTenants.statusCounts`: All asks every status but deleted, Closed asks closed and purging. On a phone the tabs are one choice ("All · 10"). An organisation has Back to Organisations, its menu beside its title, the banner of its state with the who and when of 37b-backend, and the tabs Overview, Plan and Activity log; Overview is a card of rows, and Plan a card of rows with Edit and Remove of an override. A "by" the API does not know is left out ("by the platform" of 4.09 is not drawn). The phone keeps the actions in the ⋯ menu (4.44). `useOrganisationAddress` is in `packages/ui`, and `Input` takes an addon, the host before the address (4.07). Done.

37b-frontend-3, Users and System admins:

- Users (5.01 to 5.19) and System admins (10.01 to 10.16).
- A refused delete (5.14, 5.15) opens at once with its reason, from `lastOwner` and `isSystemAdmin`, as a stopped action does (F56). Removing yourself stays disabled with its reason (10.09), as F51 does for a role the viewer cannot give.
- The root row has no action; the reason is in the Root label's tooltip (10.16; the maintainer's decision of 2026-11-04).
- It waits for 37b-backend and 37b-frontend-1.
- As built: Users and System admins grow with Load more over `ListUsers`' infinite options, with no page in the URL; a failed page says so under the rows (5.19). The Users tabs show `statusCounts`, All their sum; each count is the status's total, which no search changes (frames 8.06, 4.04; the maintainer's decision of 2026-11-10, which 37b-backend-counts brings to the API). On a phone the tabs are one choice, the Organisations' `ViewChoice`, which moves to `apps/platform/src/shared/`; the next page is #244's `useLoadMore`, which takes an in-place failure. The tables follow the frames' columns, with no avatar; a user's organisations are a card list with Make Owner, and the suspension's banner holds the reason and Reactivate. A deleted user is offered no Suspend; 409 `user.deleted` stays the race's text. A phone card of a system admin opens their page, `/settings/system-admins/[userId]` (10.15), whose two-step row says the rule without "On": no field says whether a new admin has set up their app (the maintainer's decision of 2026-11-10). The phone keeps the user's ⋯ and Make Owner in place of 5.17's one Suspend button, so no action is lost on a phone (the maintainer's decision of 2026-11-10). Done.

37b-frontend-4, the end of 37b:

- `Status`, `Tag`, `SubNav` and `Freshness` are deleted from `packages/ui`, with the web's last `Tag` ("You" in Members) and the samples of the dev components page. `Freshness` leaves the Figma Components page.
- The organisation's tile: the frames draw no tile on the platform, so the platform's `Initial` is deleted. The tile of `OrgSwitcher` becomes a component of `packages/ui`, `info-subtle` in every place; the brand gradient stays only on the product's mark (the maintainer's decision of 2026-11-08).
- `isNavigation` of `apps/web/src/proxy.ts` and `apps/platform/src/proxy.ts` (#238) moves to one shared place; the two proxies use it.
- It waits for 37b-frontend-1, -2 and -3.
- As built: no screen used `Status`, `Tag`, `SubNav` or `Freshness`, so they are deleted; Members' "(you)" is muted status text, as 8.01 draws it. The Figma Components page had no Freshness component any more. `OrgTile` of `packages/ui` draws the organisation's first letter on `info-subtle` in the switcher's face (24 px), its menu (24 px) and Choose an organisation (32 px, 1.18). The platform's switcher names the product (`product`), whose mark keeps the brand's gradient. `isNavigation` is in `packages/ui/src/lib/navigation.ts`, beside the other helpers both apps run on the server, with its tests; each proxy keeps the tests of its answer: 401 to a signed-out navigation, and `protect()` for a signed-out page load and a signed-in navigation. Done.

37b is done.

37c, audit retention (the maintainer's decisions of 2026-11-11):

- The organisation's Activity log keeps its entries for a plan limit, an entitlement like seats: `audit.retention.days`. The default is 1 year, 365 days.
- At the end of the period, a daily job deletes an entry entirely. Owners can export before, with 37a's CSV export.
- The platform's own log keeps its entries for a deployment setting, `Audit:Retention:Days`, default 365, not tied to a plan.
- When the period gets shorter, the entries outside the new period stay 30 days, then go. This holds for each of the four ways: a plan change, an edit of the plan's value, an override set lower or removed, and an override that expires. The Change plan dialog, the plan editor's preview and the override dialogs say so.
- It is done before production (step 47).

The rules, in `apps/api/docs/ARCHITECTURE.md`:

- The deletion goes through two narrow `SECURITY DEFINER` functions in the shape of R14 and U8, one for each log (R19). Partitions do not fit a period that differs for each organisation (the maintainer's decision of 2026-11-11).
- A floor of 30 days: each function refuses a moment later than 30 days ago, and the entitlement and the setting refuse a value under 30, so a fault in the application cannot delete a recent log. The plan editor and the override dialog say it (the maintainer's decisions of 2026-11-11, the value of 30 days included).
- The key is a limit in days, a whole number of at least 30, or unlimited. A missing value never deletes: a plan without a value keeps every entry, as unlimited does (E3). Retention is the one limit that deletes (E4). Every plan gets 365 in the migration (K14).
- A shortening starts a hold of 30 days in `audit.tenant_retentions`, the one state of each tenant. A plan change and an override start it at the command's moment, from the event; a plan edit and an expiry start it at the daily job's moment, because they have no event for the tenant (K14).
- The Activity log says its period, in a sentence under its title, in both apps. The organisation's log shows a Banner while a hold runs and an entry older than the new period exists, with Export for a viewer who may export. The Owners' `tenant.plan_changed` notification, in the bell and by email, says which entries go and on which date, in three languages. An override change goes to the bell only, so the Banner carries the date there (the maintainer's decisions of 2026-11-11).

The parts of 37c, in their order. 37c-design and 37c-backend-1 run at the same time. The pull request of this plan holds the table of every frame with its part, and the table of every value with its field.

37c-design, the frames of the retention:

- Platform app: the plan editor's Activity log card in 6.04, 6.05, 6.06, 6.10, 6.11 (a value under 30), 6.13 and the phone's summary 6.18; a new frame 6.19, the impact dialog of a save that shortens the period, with the count of the organisations it touches; the organisation's Plan tab row in 4.14 and 4.45; the Change plan dialog's sentence in 4.15; the override dialogs' sentences in 4.16 (the key, the floor, a lower value, an expiry) and 4.17 (a removal that shortens); the period's sentence of the platform's Activity log in 7.01 and 7.08.
- Web app: the period's sentence under the title of 12.01 to 12.09; two new frames, 12.10, the Banner of a hold with Export, and 12.11, the same in dark.
- No dark, Arabic or phone frame of these sections draws a plan, an override or the organisation's Activity log beyond those above, so none is added. The Billing frames 10.02, 10.03, 10.06 and 10.09 are step 40's.
- It waits for nothing.
- As built: the plan editor has an Activity log area with one row, "Keeps entries": days, Unlimited, and the floor and the hold in its help (6.04 to 6.06, 6.10, 6.13). 6.11 refuses 7 with "Enter 30 days or more." 6.18 lists "Activity log 365 days". 6.19 is the impact dialog of a save from 365 to 90 days, with the count of the organisations it touches. The override dialog 4.16 draws a lower value, with the floor under Value. A new frame, 4.49, draws a longer override with an end date: the expiry that shortens the period. 4.17 removes an override of the key, over a Plan tab that shows it. In a sentence the key is "Activity log" (4.14, 4.15, 4.17), as the frame table of the plan's pull request writes it. The period's sentence is "Entries are kept for 365 days." in 7.01, 7.08 and 12.01 to 12.09. 12.10 and 12.11 draw the Banner with Export CSV, and a note says that a viewer without the export's permission sees no Export. The frames are in English; a note in sections 6 and 12 of the Figma file holds the Turkish and Arabic texts. Done.

37c-backend-1, the deletion:

- The key `audit.retention.days` (E1), its floor of 30 days in `ControlPlane.Domain/Plans/`, generated with the grace of 30 days into the contracts and into the generated TypeScript of `packages/api-client`, so the platform app reads the floor too (D1). A CHECK constraint of the floor (R15), and a migration that gives every plan 365.
- E3's missing value that keeps every entry, and E4's sentence that retention deletes.
- `audit.tenant_retentions`, the hold of K14 and its lock (K6o). A tenant's first row starts with no limit, so the first run holds the older entries for 30 days. The tenant purge deletes the row. `TenantPlanChangedEvent`, `EntitlementOverrideSetEvent` and `EntitlementOverrideRemovedEvent` get an optional `retentionShortened`, `{ fromDays, toDays, goesAt }`, which ControlPlane computes in the tenant's transaction; Audit's handlers of those events start the hold in the same transaction as the entry.
- The two functions of R19, with their floor and their tests in the shape of `PurgeTenantDataTests`.
- The daily job of K14 and K15: at a worker's start and every 24 hours. Unlike the timers of I7 and U8, it sends at the start too. It reads each tenant's effective period through one ControlPlane contract, starts a hold for a plan edit and an expiry, and deletes in batches of 1000, one transaction each. The platform's log follows `Audit:Retention:Days` (K15).
- The words of the key in three languages, for the notifications of an override (`notificationWords`, U1) and their link to the Activity log (`NotificationLinks`).
- The documents: E1, E3, E4, E7, K3, K6o, K14, K15, R11, R15, R18, R19, the flows `plans-and-entitlements.md`, `notifications.md`, `reading-the-audit-logs.md` and `tenant-purge.md`, `configuration.md` and api-catalog. The first sentence of section 13's items for 37c goes.
- As built: the job's tenant step has two hops, because one DbContext owns a handler's transaction (W1): `ApplyTenantAuditRetentionHandler` reads the tenant's status and period from ControlPlane, and `DeleteExpiredEntriesHandler` writes Audit's row and deletes. The second hop keeps the row only while the tenant still has an entry, under the row's lock, and the purge takes the same lock, so a purge between the hops leaves no row (the maintainer's decision of 2026-11-12). The second hop uses the period only while the first hop's reading is at most a minute old; else it reads again, at most three times in one run, and then the tenant waits for the next day (the maintainer's decision of 2026-11-12). An event's handler only shortens; a longer period is the job's. The functions refuse a moment later than 30 days before the clock of the call. The floor and the hold reach the platform app as `AuditRetention.Floor` and `AuditRetention.Grace` of `@repo/api-client`, which the generator of the keys writes; an enum of the OpenAPI document is a string (N11f), so the document does not carry them. The ControlPlane migration is `AddRetentionLimit`, Audit's `AddAuditRetention`. An override of the key leads to the new link kind `tenant_activity_log`, which the web maps in 37c-frontend-web; until then it leads to the start page, or to the organisation's Home inside one, and is reported (F55). Done.

37c-backend-2, the fields the frames read:

- `GetPlanUpdatePreview` answers `retentionShortened`, `{ count, fromDays, toDays }`: the organisations on the plan whose period gets shorter, those without an override of the key that has not expired, or null (6.19).
- `GetTenantPlanChangePreview` answers `retentionShortened`, `{ fromDays, toDays }`, or null (4.15).
- `GetAuditRetention`, `GET /v1/tenants/{tenantId}/audit-log/retention`, `tenant.audit.read`, `NotPlanGated`: `{ days, shortening }`. `days` is the effective period, null for no limit. `shortening` is `{ days, goesAt }` while a hold runs and an entry older than the new period exists, else null (12.01 to 12.11).
- `GetPlatformAuditRetention`, `GET /v1/platform/audit-log/retention`, `platform.audit.read`: `{ days }` (7.01, 7.08).
- The Owners' `tenant.plan_changed` gets the optional values `retentionDays` and `retentionGoesAt`, with a text without them, in three languages (U1, U2).
- The override dialogs (4.16, 4.17) read the plan's value and the override from `GetTenantPlan`, which has them already; a shorter period is the comparison of those two fields.
- api-catalog and `what-the-screens-read.md`. Section 13's item of 37c-backend-2 goes.
- It waits for 37c-backend-1.
- As built: `GetAuditRetention` follows the pattern of an audit page and its names: the endpoint sends two queries, the period through `IAuditRetentionDirectory`, the job's contract, on ControlPlane's DbContext, then Audit's row and entries on Audit's (W1). The row follows the period and clears an ended hold in memory by the job's own methods and is never saved, so the read writes no row and takes no lock. A plan edit and an expiry, which the job holds at its next run, answer the hold the job would start at the moment of the read: up to a day earlier than the job's date, never later (`what-the-screens-read.md` says which date each screen shows). The plan edit's count leaves out a purging tenant too, whose log goes with its purge, and no search or page narrows it; `retentionShortened` is null only when the plan's period stays as long or gets longer, so a count of 0 can come. The preview's answer is `PlanUpdatePreviewPageResponse`, the list fields and `retentionShortened`; the platform app takes the new type name and the field in its test data, with no screen change. `GetPlatformAuditRetention`'s endpoint reads the setting, with no message and no transaction. The two values of `tenant.plan_changed` go together: a notification value may name the optional value it goes with (`With`, U1), and the two share one text without them. 4.49's sentence needs no other field. Done.

37c-fixes, two faults that 37c left:

- `TraceIdTests` runs its worker on a database of its own, so the worker kept the audit retention job, and the job's handlers joined the new tenant's spans. Its hosts remove the job (`ApiFactory.RemoveAuditRetention`, section 9 rule 6 of the API's architecture). A run with the job every 500 ms found no other test it disturbs.
- A number of days in a notification is a value of its own kind, written in the form of the number's CLDR plural category in each language (U1, U2). Arabic writes "3 أيام", "30 يومًا" and "100 يوم". `tenant.plan_changed`'s `retentionDays` is the one such value.
- Done.

37c-frontend-platform, the platform's screens:

- The plan editor's card, its floor and its preview (6.04 to 6.06, 6.10, 6.11, 6.13 without its prices, 6.18, 6.19); the Plan tab's row (4.14); the Change plan dialog's sentence (4.15); the override dialogs (4.16, 4.17, 4.49); the Activity log's sentence (7.01, 7.08); the Activity sentences of an override of the key.
- 4.45 and 6.13's prices and meters stay step 40's.
- It waits for 37c-design and 37c-backend-2.
- As built: the editor's Activity log area has one row, "Keeps entries", with the days, Unlimited, and the floor and the hold in its help; a value under the floor is refused before it is sent, and after a save a value is checked again as it is typed, as the name is (6.11). A new plan starts the key at `AuditRetention.Default`, 365, which the generator of the keys now writes too (the maintainer's decision of 2026-11-13). 6.19 opens only for a `retentionShortened` whose count is above 0; with an excess too, the dialog says both and lists the excess. 4.15's line, 4.16's lower value, 4.49's expiry and 4.17's removal compare the plan's value with the override or read `retentionShortened`; a plan with no limit has its own sentence. 7.01 and 7.08 say the period under the description, once `GetPlatformAuditRetention` answers. Every number of days on the platform's screens takes the ICU plural of its language, Arabic's few, many and other included. Done.

37c-frontend-web, the organisation's Activity log:

- The period's sentence (12.01 to 12.09) and the Banner of a hold (12.10, 12.11), with Export only for a viewer who holds `tenant.audit.read`, the export's permission. The Activity sentences of an override of the key.
- It waits for 37c-design and 37c-backend-2.
- As built: the page reads `GetAuditRetention` beside the log. The sentence stands under the description: "Entries are kept for 365 days.", or "with no time limit" for null. While `shortening` is not null, the warning Banner stands between the header and the log, with the date in the viewer's zone (F18) and Export CSV, which opens 12.08's dialog. The export's permission is the page's own, `tenant.audit.read`, so a viewer who sees the Banner may export, and a viewer without it sees "You can’t open this page" and reads no period. While the period loads or fails, the page shows neither the sentence nor the Banner, and the log works as before. In a sentence of an override the key is "Activity log" and its value counts days ("90 days"). Every count of days is an ICU plural in each language; Arabic has its six forms. The link kind `tenant_activity_log` opens the log. 12.09, the capped toast, gets its shot. Done.

37c is done.

37d, dependent limits (the maintainer's decisions of 2026-11-13):

- A limit may depend on a feature (E1). Today one pair exists: `roles.custom.max` depends on `roles.custom`. The rule is general, for every later pair.
- While the feature is off, the limit cannot be changed: on a plan (the plan editor), and for one organisation by its effective value of the feature (the override dialogs). The screen disables the limit's field and says "Turn on Custom roles first.", with the feature's own name.
- The API refuses a change of the limit while its feature is off, with 400. Removing an override of the limit stays allowed (E4); the plan's value applies when the feature turns on.
- The stored value stays. When the feature turns on again, the limit shows its stored value again. Turning the feature off deletes nothing.
- The Plan tab shows the limit while its effective feature is off as "Off", the word of a feature that is off, so one state has one word. The line under it keeps the value that comes back: the plan's and the override's.
- Rejected: one key with 0 as off (it loses "off also stops editing the existing custom roles", E5), and a greyed hint only (the API would keep a value with no effect).
- 37d takes over from 40a the limit of a feature that is off (37b's line, 6.05).

The parts of 37d, in their order. 37d-design and 37d-backend run at the same time; 37d-frontend-platform waits for both. The pull request of 37d-design holds the table of every frame with its part, and the table of every value with its field.

37d-design, the frames of a dependent limit:

- Platform app: the plan editor's sentence in 6.05 and 6.10; new frames 6.21 (Team, Custom roles turned off, the stored 5 disabled), 6.22 (the same in dark), 6.23 (the same in Arabic), 6.24 (the phone editor) and 6.25 (the phone's summary, "Off"); the Plan tab with the feature off by an override, 4.50; the Add override dialog of the limit with its field disabled, 4.51; the Remove override dialog of the limit, which stays allowed, 4.52. A note in section 6 of the Figma file holds the Turkish and Arabic texts.
- It waits for nothing.
- Done (#275).

37d-backend, the rule in the API:

- The dependency is declared once in code, beside the keys (E1), and reaches the platform app through the generator, as `AuditRetention` does (D1), so the screen reads the pair and does not hard-code it. The plan editor's New plan (6.10) has no plan to read yet, so a field of a response is not enough.
- `CreatePlan`, `UpdatePlan` and `SetEntitlementOverride` refuse a change of a dependent limit while its feature is off, with 400 and the key. A request that turns the feature on and changes the limit together passes. `RemoveEntitlementOverride` is not refused. `GetPlanUpdatePreview` never blocks an edit: the save refuses it (E4).
- It waits for nothing.
- As built: `[DependsOn(FeatureKeys.RolesCustom)]` on `roles.custom.max` declares the pair, and the generator writes it as `LimitFeatures` of `@repo/api-client`. The code is `plan.feature_required`, with the limit's key in `entitlement`. A value the limit already has is no change, and a limit left out of a plan's edit closes it. An override reads the tenant's effective feature, an override of the feature included. Done (#276).

37d-frontend-platform, the platform's screens:

- The plan editor (6.05, 6.10, 6.21 to 6.25), the Plan tab (4.50) and the override dialogs (4.51, 4.52), from the dependency of 37d-backend and the effective value of the feature in `GetTenantPlan`.
- It waits for 37d-design and 37d-backend.
- As built: the screens read the pair from `LimitFeatures`, so no component names a key of the pair. The plan editor disables the limit while the form's value of its feature is not on, keeps its value and says "Turn on Custom roles first." under it; turning the feature on in the same form enables it at once, and turning it off deletes nothing. A save's 400 `plan.feature_required` shows the sentence under the limit's field. The phone's summary says Off for the limit (6.25). The Plan tab reads the effective feature of `GetTenantPlan`: the limit says Off with the lines of the plan's value and the override and the sentence, Edit is disabled and Remove stays; such a limit is over nothing, so it has no "over by" and no banner. Add override disables the value and the action for the limit; Remove override says that the plan's value applies when Custom roles turns on. Done.

37d is done.

## 6. Step 38: Terms of use

Status: in progress.

- The platform manages the versions. A version has an effective date, a link, an optional summary, whether it requires acceptance, and whether it sends an email.
- An effective date is never in the past.
- One scheduled version at a time. A system admin can edit or cancel it before it takes effect.
- A version in force never changes.
- Once someone has accepted a scheduled version, its link and "requires acceptance" lock; its effective date and summary stay editable. To change the link, a system admin cancels the version and adds a new one; the cancelled version keeps its acceptances (the maintainer's decision of 2026-11-14). The edit endpoint refuses a locked field with 409, and 11.10 disables the two fields with their reason (F52). Why: an acceptance must prove which text the person agreed to (GDPR, Article 7(1)).
- A person accepts the version after sign-in.
- A public endpoint gives the version in force. The "Terms" link before sign-in opens its link.
- The template stores the versions and their links only. The text of the terms lives outside the repository, with the brand's site (step 44). Support access's shape (`docs/decisions.md`, section 2, 2026-11-14) is settled before a product writes that text, not before this step's code.

Decided by the frames (38-start, 2026-11-14):

- A version that requires acceptance asks for it from the moment it is scheduled, before its effective date: the gate after sign-in (1.09 to 1.12, 1.14) says the date it applies from, and has Accept and Sign out only. 11.01 and 11.05 count acceptances of a scheduled version.
- A version that requires no acceptance and sends an email gives a notice on Home (4.03) with Read the terms and a close button, and the email (13.37 to 13.39). A version that requires acceptance and sends an email has its own email text (13.40 to 13.42).
- "Send an email" is a second switch beside "Requires acceptance", on by default (11.02). The two switches give three kinds of change: a minor one (no acceptance, no email, no notice), a medium one (no acceptance; the email and the notice) and a major one (acceptance and the email). A version that requires acceptance and sends no email shows only the gate. The email goes when the version is added with the switch on, or when a system admin turns the switch on later. Once the email has gone, the switch locks, as an email cannot be taken back: 11.10 disables it with its reason (F52). Until then it stays editable, like the summary. The list says "No notice" for a minor version (11.01, 11.14), and a version's page has an Email row (11.05, 11.12, 11.15). Why: a material change of the terms needs a notice (Regulation (EU) 2019/1150, Article 3(2): 15 days for business users); a typo does not (the maintainer's decision of 2026-11-14).
- An invitee who has no account yet accepts the version in force on Accept invitation (1.15).
- Cancelling a scheduled version keeps its acceptances in its record (11.11, 11.13).
- Only one version may be scheduled: Add version is disabled with its reason while one is (11.01, 11.14; F52).
- Who accepts: the tenant users. A system admin never joins an organisation (1.48), so the platform has no gate. "18 of 67 accepted" counts the active users: not suspended, not deleted.

The parts of 38, in their order. 38-design and 38-backend run at the same time; the two frontend parts wait for both. The pull request of 38-start holds the table of every frame with its part, and the table of every value with its field.

38-design, the frames the step still lacks:

- The notice email of a version that requires no acceptance (web section 13), in light, dark and Arabic, with its footer.
- The Activity log sentences of the platform's log for a version added, edited and cancelled (platform section 7).
- A note with the Turkish and Arabic texts of the step's frames (web 1.09 to 1.15, 4.03; platform 11.01 to 11.15).
- The dark and Arabic forms of 1.09 and 11.01, as the earlier steps drew one of each.
- It waits for nothing.
- As built: the notice email 13.37 (light), 13.38 (dark) and 13.39 (Arabic), without the summary, which is one language only; 7.09 with the four sentences (added with acceptance, added with a notice, changed, cancelled); 1.55 (1.09 in dark), 1.56 (the phone's gate, 1.14, in Arabic), 11.16 (11.01 in dark) and 11.17 (11.01 in Arabic); and a note, "Note · 38 copy", in the platform's section 11. 11.10 now draws the locked state that this section decides: Link and Requires acceptance disabled, each with its reason. Done.

38-backend, the versions and the acceptances:

- A terms version and its acceptances in ControlPlane: the version's effective date, link, summary, `requiresAcceptance`, `sendsEmail`, the moment its email went out, and its state (scheduled, current, replaced, cancelled), which the date derives; an acceptance is the user, the version and the moment.
- Platform endpoints, `platform.terms.read`, `platform.terms.create`, `platform.terms.update` and `platform.terms.cancel`, in the pattern of the other platform permissions: list the versions with their acceptance count (11.01, 11.09); one version with its people, Accepted and Not yet, with a cursor (11.05, 11.06, 11.12, 11.15); add (11.02 to 11.04); edit and cancel the scheduled version (11.10, 11.11, 11.13). Every change writes the platform's Activity log.
- Tenant user endpoints: what the signed-in person must do (nothing, accept a version, or see a notice), accept a version (1.09 to 1.12), close a notice (4.03). Accept invitation records the acceptance of the version in force (1.15).
- A public endpoint, anonymous and cached: the version in force, its link and its date. No version in force answers 204, and the "Terms" link stays hidden.
- The notice email to the active users when a version with `sendsEmail` is added, or when the switch turns on later, through Notifications, in three languages. The edit endpoint refuses to turn the switch off after the email went out, with 409.
- It waits for nothing.
- Done (`apps/api/docs/flows/terms-of-use.md`). The points it settled, which the frontend parts read:
  - A version's day takes effect at 00:00 UTC; "today or later" is the UTC day. A version of today is in force at once.
  - `GetMyTerms` answers `action` `nothing`, `acceptance` or `notice`, in place of 38-start's `none`, `accept` and `notice`, and a version's `state` is `in_force` in place of `current`: the screens use "none", "accept" and "current" as other literals, which F3 forbids for a value of the contract.
  - The gate asks for the version in force first, then the scheduled one. A notice goes only to a user who existed before the version, and stops when the person closes it or accepted the version.
  - The acceptance takes the link the person read, so a link that changed answers 409 `terms_version.changed`. An acceptance and a dismissal write no audit entry: the acceptance is its own record (`ARCHITECTURE.md`, K3).
  - The versions' list is one read, `GetTermsOfUse`, without the list contract: the versions are few. A version's people page by a cursor, `status=accepted` or `status=not_yet`.
  - The public route `/v1/public` is new (A5, N5). `GetTermsInForce` answers 204 before the first version, and sends `Cache-Control: public, max-age=300`.
  - Each version has `sendsEmail`, true when left out (decided with the maintainer on 2026-11-14): with acceptance, the email says the person accepts at next sign-in (13.40 to 13.42); without, it is the notice email (13.37 to 13.39) and Home's notice (4.03); without email, nothing is sent or shown. `emailSentAt` keeps the moment it went; then off answers 409 `terms_version.email_sent`. A version saved without email may turn it on while scheduled, and the email goes then.
  - The notice email goes by email only, at `Notifications:Pace:DeliveriesPerSecond` (5) deliveries a second, under Resend's 10 requests a second (U3). The texts in Turkish and Arabic are the thread's; 38-design's copy note may change them.

38-frontend-platform, Settings › Terms of use:

- 11.01 to 11.15 and the command menu's "Terms of use" (2 · Shell). The Activity log sentences of 38-design.
- It waits for 38-design and 38-backend.

38-frontend-web, the gate, the notice and the link:

- The gate after sign-in (1.09 to 1.12, 1.14), the invitation's checkbox (1.15), the Home notice (4.03), and the "Terms" link in the footer before sign-in (`packages/ui` `auth-footer`, which waits for it today).
- It waits for 38-design and 38-backend.

Not this step: 1.27 (Create an account · accept the terms) is 40d's. It uses 38-backend's accept endpoint.

## 7. Step 39: Feature flags

Status: todo.

- We build them in-house.
- The code defines each flag with its type and a default. The types are release and kill switch.
- The rules apply in order: listed organisations, then a stable percentage of organisations, then everyone or nobody.
- Each environment has its own state.
- Every change is audited.
- Cleanup: a release flag that is on for everyone for 30 days is "Ready to remove". Any flag that is off everywhere for 30 days is "Unused".
- A weekly notification tells the system admins.
- A removed flag is archived. Its name is never used again.

## 8. Step 40: Billing

Status: todo.

The model:

- By default an organisation pays by card in the app. A system admin can mark an organisation Contract: it is invoiced outside the app.
- The provider is Stripe, behind one interface.
- Our app keeps the rules: the plans, the limits, the features, the quotas, stop or charge, the spending cap and its own usage count.
- Stripe keeps the money: the prices, the subscription, the card, the address, the VAT ID, the invoices, the tax and the metered overage.
- We do not use Stripe Entitlements.
- A5 gets an exception for signed Stripe webhooks.

40a, plan prices and the Billing manager:

- A plan has a base fee and a fee per person. Each can be 0, monthly and yearly.
- A plan has usage meters (a quota, stop or charge, and a unit price) and limits.
- The seeded plans: Free (3 seats), Team (15 seats, 5 custom roles) and Business (50 seats, 20 custom roles).
- Each seeded plan keeps the Activity log for 365 days (`audit.retention.days`, 37c), until the maintainer gives a plan another value.
- The custom-role limit follows the same F56 rule as Members: "Create role" stays enabled and opens a dialog that says the limit, the reason and what to do, its banner is as Members', and it shows no count beside "Create role" (the maintainer's decisions of 2026-11-07).
- A new permission, `tenant.billing.manage`. The Owner holds it by default.
- A fourth built-in role, Billing manager, with `tenant.profile.read`, `tenant.members.read` and `tenant.billing.manage`. It is free: it takes no seat and no fee per person. Product modules never add permissions to it.
- Members' "No free seat" offers "Request a plan change", to the support contact, since step 37a. This changes with the plan: an organisation that pays by card can change its plan (8.39, 8.40), a Contract organisation keeps the button (8.43, 8.45).
- When Billing comes, the maintainer decides whether the Plan row stays on General (F56, 7.01).

40b, Stripe:

- Checkout takes the first payment.
- The customer portal manages the card, the address, the VAT ID and the invoices. The email change, the plan switch and the cancellation are off in the portal.
- An upgrade applies at once, with proration.
- A downgrade or a cancellation applies at the end of the period. The person can undo it until then.
- The Change plan page and Cancel plan (10.02, 10.03, 10.06, 10.09) say when a move shortens the Activity log's period: the entries outside the new period stay 30 days, then go (37c; the maintainer's decision of 2026-11-11). Its frames get the sentence in this step's design.
- Monthly to yearly applies at once. Yearly to monthly applies at the end of the year.
- Billing per person is automatic. Billing starts when a person accepts an invitation. The next invoice prorates joins and removals. On a yearly plan, a join is charged at once.
- A failed payment: Stripe retries for about 2 weeks. Then 1 week of grace follows. Then the organisation moves to the default plan. Nothing is deleted.
- An optional billing email.
- A price change has three options: everyone after 30 days' notice, everyone after a set period, or new customers only with a later "move".
- Yearly customers keep their price until renewal.

40c, usage metering:

- The code defines the meters.
- The app keeps the totals per organisation per period and reports them to Stripe.
- Notifications at 80% and at 100%.
- An optional monthly spending cap. The people who manage billing set it.

40d, trial and sign-up:

- A trial is a state of a plan. It needs no card. It lasts 14 days by default. An organisation gets one trial.
- A system admin can extend a trial. After the trial, the organisation moves to the default plan.
- Self-service sign-up is built, and off by default. A product turns it on in its configuration, with the identity provider's sign-up mode.
- The sign-up flow: sign up, accept the terms, create the organisation, start a 14-day Business trial, invite the team.
- A person creates at most 3 organisations.
- Sign-up needs a captcha and a verified email.

## 9. Step 41: Extension points

Status: todo.

- How a product adds modules, permissions, entitlement keys, meters, notification types and menu sections without editing template files.
- An example business module, end to end.
- A guide to start a product from the template, with the template as its git upstream.

## 10. Step 42: Observability

Status: todo.

- Metrics, traces and logs go to a backend.
- Alerts.

## 11. Step 43: Accessibility and end-to-end tests

Status: todo.

- A WCAG 2.2 AA review.
- End-to-end coverage of the critical journeys.

## 12. Step 44: Marketing site

Status: todo.

- Its own design step comes first.
- It hosts the texts of the terms and of the privacy policy.

## 13. Step 45: Mobile app

Status: todo.

- Its own design step comes first.
- Push notifications come as a new channel.

## 14. Step 46: Environments and infrastructure

Status: todo.

- The environment model: preview, staging and production.
- Terraform on Azure.
- Redis for the cache.
- Secrets.

## 15. Step 47: Production

Status: todo.

- The production environment. Today there is none: only staging runs. `delivery.yml` has a production job behind the maintainer's approval, but `DEPLOY_PRODUCTION` is unset, so the job is skipped, and nothing has been deployed to production (`docs/deployment.md`, section 4).
- It waits for 37c: the audit retention is done before production (the maintainer's decision of 2026-11-11).
- The first release.
- Rollback and a restore drill.
- The Clerk email templates and the sign-in lockout, set on purpose.

## 16. Later: Template 1.1 and after

Status: todo.

- Support access. Its shape is decided (`docs/decisions.md`, section 2, 2026-11-14).
- The status page.
- Enterprise SSO.
- Public API keys.
- Outgoing webhooks.
- Notification settings for activity categories.
- Prepaid credits.
- Seats included in a base fee.
- A native review of the Turkish and Arabic texts.

## 17. Order of the work

The work comes in this order. A thread that finishes an item updates this section in its pull request.

1. Merged: #277 (the e2e routes removed before the context closes), #279 (three frontend fixes), #280 (the fixes of four staging findings; its maintainer's tasks are in its description), #281 (the coordinator's knowledge in the repository) and #282 (a members' limit of 0, and two checks of the frontend's rules).
2. Step 34's handoff package (`docs/design/`), before the Figma seat ends, about 29 Dec 2026: make it by about 24 Dec 2026. The maintainer kept the seat one more month on 2026-11-14 (section 2), while steps 38 to 40 still need frames. Without the seat, Figma's MCP server gives a few calls a month in place of 200 a day, so a thread can no longer read the design (Figma documentation, "MCP server rate limits and access").
3. Step 38, in the parts of section 6. 38-start, 38-design and 38-backend are done; the frames of the email switch are merged (#286), so the two frontend parts can start. Support access's shape is decided (`docs/decisions.md`, section 2, 2026-11-14); it is settled before a product writes the terms' text, not before step 38's code.
4. Small findings that no pull request fixes yet:
   - The Figma follow-ups of `docs/design/followups.md`.
5. Steps 39 to 47, in the order of section 1. Step 40a brings the Billing manager role and the custom roles limit dialog (9.14).
6. Wolverine 6.63 or later, when Dependabot brings it (`docs/decisions.md`, section 2).

Which threads may run at the same time:

- A design thread and a backend thread of the same part run together. The frontend thread of that part waits for both.
- Two design threads never edit the Figma file at the same time.
- Two threads that change `docs/roadmap.md` or the same `ARCHITECTURE.md` conflict at the second rebase. That is expected: the second thread rebases on `origin/main` and resolves it.
