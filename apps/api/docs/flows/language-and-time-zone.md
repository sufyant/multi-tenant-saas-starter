# Language and time zone

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

The web speaks English, Turkish and Arabic. The API sends codes and data, and the screen writes every text in its own language (K5). The API writes text only in emails (api-catalog, K8).

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `ChangePreferencesCommand` | `PATCH /v1/me/preferences` | ControlPlane, `ChangePreferencesHandler` | Yes; it declares nothing (R6) |
| `UserPreferencesChangedEvent` | `ChangePreferencesHandler`, only when a value changed | Audit, `RecordUserPreferencesChangedHandler` (`user.preferences_changed`, platform log) | Yes |

- One list: `Language` in `SharedKernel` (`ARCHITECTURE.md`, section 2), `en`, `tr` and `ar`. JSON, the messages and the database keep a language as its tag. `ExactNameEnumConverter` (`ARCHITECTURE.md`, section 2) reads an exact tag only: never a number, a list of names with commas or a tag with spaces, which the default enum converter would take. So an unknown tag is a body the endpoint cannot read (400 `request.invalid`), as an unknown role is. The OpenAPI document has the one schema `Language` (N11).
- A user keeps `language`, required, and `timeZone`, an IANA name such as `Europe/Dublin`, or null for "not set": the web then uses the browser's time zone. `GetMe` answers both. `ChangePreferences` sets both: it locks the user row (K6), refuses a deleted user under the lock (403 `user.deleted`, A9), and the values the user has change nothing and publish nothing (I1).
- `ChangePreferencesValidator` checks the shape of a time zone only: at most 64 characters (`too_long`), parts of ASCII letters, digits, `_`, `-` and `+`, joined by single slashes (`time_zone_invalid`). The web offers the list from the browser's time zone data, which can be newer or older than the image's (`ARCHITECTURE.md`, section 7). The API converts a time to a zone in one place only: the formatter of the emails, `EmailDates`. There a zone the image does not know gives UTC.
- A new user starts with the language of the invitation they accept (`AcceptInvitation`). A known user keeps theirs. A root system admin who enters first through the system door starts with `en`.
- `InviteMember`, `CreateTenant` and `InviteOwner` take an optional `language` for the invitation's email. Without it, the invitation takes the inviter's own; for `CreateTenant` and `InviteOwner`, the system admin's. The invitation keeps it (`control_plane.invitations.language`).
- `RecordSignUpHandler` puts the invitation's language, its expiry and the inviter's time zone into `InvitationReadyEvent` (`invitations.md`). Notifications writes the email in that language, in HTML and plain text, from the generated templates of `packages/emails` (`ARCHITECTURE.md`, section 7, "Email"). Each language's texts are in `packages/emails/messages/`.
- Both emails write a moment with one formatter, `EmailDates`: the date with the language's month names and pattern ("1 Nov 2026", "1 Kas 2026", "1 نوفمبر 2026"), the time in 24 hours, and the zone's label, its IANA city and its offset at that moment, `Istanbul (UTC+3)`. Digits are Western in every language. The image has no ICU, so the month names and patterns come with the generated emails, and the city stays the IANA name in English: an email writes `Istanbul` where the boards write `İstanbul`. The invitation takes the inviter's zone, the purge warning the Owner's (`tenant-purge.md`). Without a zone, or with one the image's data does not know, the moment is in UTC, which each language writes in its own words.

Sources: W3C, "Language tags in HTML and XML" and "Working with time zones". IANA Time Zone Database, "Theory and pragmatics of the tz code and data". Microsoft Learn, "Globalization invariant mode". RFC 5646, section 4.4.1 (the column holds 35 characters).
