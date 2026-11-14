# Terms of use

A system admin manages the versions of the terms of use from the platform. A tenant user accepts a version, or reads its notice, after sign-in. Anyone reads the version in force before sign-in. The template keeps each version's link, never its text: the text lives with the brand's site. The step is 38 of `docs/roadmap.md`, section 6. The rules are in `ARCHITECTURE.md` (A5, K3, K6, R6, U3, U5, N5). This file says what happens, step by step.

## 1. A version and its state

- A version has a day, `effectiveDate`, the link to its text, `url`, an optional summary of what changed, and `requiresAcceptance`. It takes effect at 00:00 UTC of its day. The day is today or later in UTC, never in the past (400 `terms_version.effective_date_past`). The link is an absolute `https` URL (400 `terms_version.url_invalid`).
- The days decide each version's state, on the day of each read, and nothing stores it (`TermsTimeline`). A cancelled version is `cancelled`. Of the others, one whose day has not come is `scheduled`. The latest whose day has come is `current`, the version in force. An older one is `replaced`. Two versions of one day are ordered by their creation: the later one is in force.
- Example: versions of 1 January and 1 June, and one of 1 December, on 14 November. 1 June is `current`, 1 January `replaced`, 1 December `scheduled`. On 1 December, 1 December becomes `current` and 1 June `replaced`, with no write.
- One version may be scheduled at a time. A creation while another is scheduled answers 409 `terms_version.scheduled_exists`. Every creation takes one lock of its transaction first (`pg_advisory_xact_lock`), so two system admins never both create a scheduled version (K6).
- A version of today is in force at once. A version in force never changes.

## 2. The platform's commands

1. `CreateTermsVersion` adds a version and records `terms_version.created` in the platform's log, with the version as the target and its values as the details (K3). A version belongs to no tenant (R6), so nothing goes to a tenant's log.
2. `UpdateTermsVersion` gives the scheduled version new values. It locks the version row first (K6). A version that is not scheduled answers 409 `terms_version.not_scheduled`.
3. Once someone accepted the version, its link and `requiresAcceptance` lock: another value answers 409 `terms_version.locked`. Its day and its summary still change. To change the link, the system admin cancels the version and creates a new one (the maintainer's decision of 2026-11-14). Why: an acceptance must prove which text the person agreed to (GDPR, Article 7(1)). The edit records `terms_version.updated` with the old and the new values; the values the version has record nothing (I1).
4. `CancelTermsVersion` cancels the scheduled version, under the same lock. A version in force or replaced answers 409 `terms_version.not_scheduled`; a cancelled version answers as it is, and records nothing. The acceptances stay with the cancelled version. The entry `terms_version.cancelled` holds how many users had accepted it.

## 3. Who may accept

- A user who may accept is a tenant user who is active: neither suspended nor deleted, and a member of at least one tenant (R13). A system admin never joins a tenant for that role, so the platform itself has no gate.
- `activeUserCount` counts them. A version's `acceptedCount` counts those of them who accepted it, so "18 of 67 accepted" never counts a suspended user. `locked` is true when anyone accepted the version, a suspended user included.
- `ListTermsVersionPeople` lists them for one version by a cursor (K5's exception): `status=accepted`, the latest acceptance first, or `status=not_yet`, the newest user first.

## 4. The gate after sign-in

`GetMyTerms` answers what the signed-in person must do, in this order (`TermsGate`):

1. The version in force requires acceptance and the person did not accept it: `accept` that version.
2. The scheduled version requires acceptance and the person did not accept it: `accept` that version. A version that requires acceptance asks from the moment it is created, before its day; the gate says the day it applies from.
3. The scheduled version, then the version in force, requires no acceptance, the person neither accepted nor dismissed it, and the person became a user before the version was created: `notice` of that version. A person who became a user later knew no earlier terms, so the notice tells them nothing.
4. Else `none`. A signed-in person who is not a user yet must do nothing: they accept the version in force with their invitation (section 5).

`AcceptTermsVersion` takes the link the person read. It accepts the version in force or the scheduled one only, with that link; else 409 `terms_version.changed`, and the person reads the terms again. It locks the version row against an edit, but not against another acceptance (`FOR SHARE`, K6): an edit that changes the link waits, and then finds the acceptance and refuses the link. A second acceptance keeps the first moment (I1). It answers what the person must do next: after the version in force, the scheduled one may ask in turn.

`DismissTermsNotice` closes the notice of a version. It accepts nothing, and a version that requires acceptance still asks for it.

Neither writes an audit entry: the acceptance is its own record, the user, the version and the moment, and the dismissal is the person's own view, as a read state is (K3).

## 5. Accept invitation

An invitee who has no account yet ticks the terms on Accept invitation (1.15). `AcceptInvitation` then sends `termsVersionId`, the version in force. In the tenant's transaction, after the tenant's checks and before any change (W7), the version is locked against an edit; a version that is not in force answers 409 `terms_version.changed`, and nothing is added. The acceptance is added after the new user's row, in the same transaction. A known user who accepted it already, even by the gate at the same moment, keeps their first acceptance (I1). The checkbox covers the version in force only: a scheduled version that requires acceptance asks at the gate right after.

## 6. The notice email

1. `TermsVersionCreatedEvent` reaches Notifications. A version that requires acceptance tells nobody: the gate asks for it.
2. A version that requires no acceptance tells every user who may accept, but the system admin who created it (U3), with the type `terms_version.created`. It goes by email only: Home shows the notice from `GetMyTerms`, not from the bell (U5).
3. The fan-out reaches every user at once, so it gives a pace: `Notifications:Pace:DeliveriesPerSecond` (5) deliveries each second. Each later delivery is a message scheduled a second after the ones before it. Example: 1000 users take 200 seconds. Resend allows 10 requests a second to the whole team by default, so half of it stays for the invitations and the codes of the same seconds (Resend, "Rate limit"). Wolverine releases scheduled messages at each poll of its store, 5 s by default, so the deliveries of a few seconds may still go together. An email that Resend refuses for its rate limit (429) takes the growing retries of `Notifications:InvitationEmails:RetryDelays`, so it goes later and does not reach the dead letter queue at once (U3).
4. The email's texts are in `packages/emails/messages/*.json`: the day, written in the reader's language and in no time zone, the link, and the summary, or a text without it.

An edit or a cancellation sends no email.

## 7. Before sign-in

`GetTermsInForce`, on the public route (`/v1/public/terms`, A5), answers the version in force: its id, its day and its link. Before the first version takes effect it answers 204, and the entry pages hide the "Terms" link. The answer is the same for everyone, so it sends `Cache-Control: public, max-age=300`: a browser or a cache in between may keep it for five minutes. A version in force never changes, so a kept answer is wrong only in the first minutes of a new version's day.
