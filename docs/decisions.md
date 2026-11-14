# Decisions

This document holds the maintainer's decisions that no other document holds, and the options the maintainer rejected. Read it before you ask the maintainer a decision, and before you propose to change one. A decision here is closed: reopen it only when the maintainer asks, or when you have new information. Then say what the new information is.

## 1. Where a decision lives

| Kind of decision | Place |
| --- | --- |
| A rule of the API | `apps/api/docs/ARCHITECTURE.md`, with its id (`K3`, `W1`) |
| A rule of the frontend | `docs/frontend/ARCHITECTURE.md`, with its id (`F52`) |
| A decided point of one roadmap step | `docs/roadmap.md`, in the section of the step |
| A GitHub setting | `docs/github-settings.md` |
| Any other decision, and every rejected option | This document |

- A thread writes each decision it takes into its place, in the same pull request. A decision that lives only in a chat or a pull request description is lost when the chat ends.
- A row holds the date of the decision, the decision, why, and its source. The date is the day the maintainer took it, not the day of the pull request.
- When a rule in an architecture document takes over a row, the row becomes a one-line pointer to the rule.

## 2. Decisions

| Date | Decision | Why | Source |
| --- | --- | --- | --- |
| 2026-11-11 | Wolverine fixed the race of #227 upstream in 6.63.0. Do not report it upstream. When a Dependabot pull request raises WolverineFx to 6.63.0 or later, that pull request also removes the `SettledMessages` workaround and keeps the race's test, after `pnpm generate`. | The workaround exists only for that race (`apps/api/docs/ARCHITECTURE.md`, section 9, rule 9). A test of our own still catches the race if it comes back. | #227; Wolverine 6.63.0 release |
| 2026-11-11 | The last Owner rule stays. An organisation may start with no Owner while its Owner invitation is pending. An organisation that has Owners never drops to none by Leave, Remove or account deletion. The platform's Make Owner is the recovery. | An organisation with no Owner has nobody who can manage its members, roles and billing, and its Owners' notifications reach nobody. | `LeaveTenant` and `user.last_owner` in `apps/api/docs/ARCHITECTURE.md`; `apps/api/docs/flows/owner-recovery.md`; GitHub and GitLab keep the same rule |
| 2026-11-11 | The screens keep the word "Organisation"; the code keeps "Tenant". A rename to "Workspace" does not make the template ready for consumers (B2C). That readiness is a later "personal workspace" step: each new user gets a tenant of their own at sign-up, and a tenant of one person hides the team features. | A rename touches three languages, the emails and hundreds of frames, and gives no B2C behaviour. | The maintainer's decision |
| 2026-11-11 | Support access (impersonation) stays in Later (`docs/roadmap.md`, section 16). Its shape is chosen before step 38 starts, because the terms of use must name it. The parts to choose: Clerk's actor tokens (a small free quota; more needs a paid Clerk add-on, so check Clerk's pricing then), a required reason, an entry in the organisation's Activity log, a visible banner, a short time limit, blocked sensitive actions (account deletion, email change, two-step verification, billing), and maybe the Owner's consent to a time-limited access. The maintainer chose the shape on 2026-11-14 and relaxed the "before step 38" part (the rows of 2026-11-14 below). | The terms of use (step 38) must say who may see an organisation's data. A10 refuses an impersonation session with 401 today; that rule changes when Support access is designed. | A10 in `apps/api/docs/ARCHITECTURE.md`; Clerk documentation, "User impersonation" |
| 2026-11-11 | "Require branches to be up to date" stays on (`docs/github-settings.md`, section 3.4). | E2E does not run on a push to `main` (`docs/github-settings.md`, section 3.7), so without the rule nothing tests E2E on the merged result. GitHub's merge queue is fast and safe, but it needs a repository that an organisation account owns. It stays a later option. | GitHub Docs: "Managing a merge queue" |
| 2026-11-14 | Support access's shape. A system admin opens a support session in an organisation without the Owner's consent. The session needs a reason, writes an entry in the organisation's Activity log, and shows a banner for its whole length. Sensitive actions stay blocked during it: account deletion, email change, two-step verification and billing. | Support must be able to act when the Owner is unreachable; the reason, the entry and the banner keep every session visible to the organisation. The blocked actions are the ones an attacker with a session would take over an account with. | The maintainer's decision; Clerk documentation, "User impersonation"; OWASP ASVS, V3 (session management) |
| 2026-11-14 | A support session lasts at most 30 minutes. | A short session limits what a stolen or forgotten session can do. Clerk's actor token caps a session at 30 minutes by default (`session_max_duration_in_seconds`), with a 10-minute idle timeout. | The maintainer's decision; Clerk documentation, "User impersonation" |
| 2026-11-14 | Support access is settled before the terms of use's text is written, not before step 38's code. Step 38 stores the versions and their links only; the text lives outside the repository. Support access itself stays in Later (`docs/roadmap.md`, section 16). | Step 38 names no part of the text, so its code does not wait for Support access. The text must say who may see an organisation's data. | The maintainer's decision; relaxes the row of 2026-11-11 above |
| 2026-11-14 | Support access starts with Clerk's free quota of 5 impersonations a month. The template documents the limit. A product buys Clerk's Enhanced Administration add-on (unlimited impersonations, 100 $ a month, or 85 $ billed yearly, on 2026-11-14) when its support needs more. | No cost until a product needs more. Our own impersonation in place of Clerk's would put a session flow of our own into the identity layer. | The maintainer's decision; Clerk documentation, "User impersonation"; Clerk pricing, "Administration" |
| 2026-11-14 | A dead letter's alert uses no new channel and no new secret. Each dead letter stays an Error in Better Stack, with a fingerprint of the message type and its cause, so a new cause alerts once and a repeat joins its error. After a fix, the maintainer marks the error resolved, so the same cause alerts again. | One alert per message is a storm for one cause; a threshold hides a new cause. | #280 writes the rule (`apps/api/docs/ARCHITECTURE.md`, section 7, "Error tracking", rule 9); Google, *Site Reliability Engineering*, "Monitoring Distributed Systems" |
| 2026-11-14 | An invitee's address is personal data behind `tenant.invitations.read`. In the organisation's Activity log and its CSV export, the address shows only to a viewer who holds `tenant.invitations.read`; any other viewer reads "someone". | A custom role can hold `tenant.audit.read` without `tenant.invitations.read`, and every check is by permission. | #280 writes the rule (K3, and `apps/api/docs/flows/reading-the-audit-logs.md`); GDPR, Article 25 |
| 2026-11-14 | The Figma file always matches what is built. A pull request that changes what a screen shows, or a value it reads, merges only after the frames show the change. | A frontend thread builds from the frames; a frame behind the code leads it to build the old behaviour. | The maintainer's decision |

## 3. Standing rules of the screens

The maintainer answered these in #218 (37a-frontend-2), and every screen thread applies them. Their ids are #218's own; they are not rule ids of an architecture document. Where a rule took one over, the row names the rule.

| #218 | Decision | Now in |
| --- | --- | --- |
| D4 | An action that a plan limit stops stays enabled and opens a dialog. | F56 |
| D5 | "No free seat" has one way to support: its "Request a plan change" button. | `docs/roadmap.md`, 37a-frontend-2 |
| D6 | A list grows with Load more, with no page in the URL. | F9, F10 |
| D7 | GetMyTenant's error shows in the shell. Loading stays generic. | #218 (frame 7.02) |
| D8 | Dialog and RowMenu keep the sizes of `packages/ui`. | `docs/roadmap.md`, 37a-shared-components |
| D9 | The locked role card of F51. | F51 |
| D10 | No Freshness line under a list's title. | F59 |
| D11, D12 | Files download with `apiDownload`. A file's name is made once, on the server, in the person's time zone. | F1 (`apiDownload`) |
| D13 | A Table column shows from a container width (`show: "wide"`), with a container token. | F20 (`cq-table-wide`) |
| D14 | Pending invitations = max(0, seats − members). | `usePlan` of the tenant web |
| D15 | The Members banner shows at and over the limit, with "View invitations" only while an invitation is pending. | F56 |
| D16 | No count beside Invite. The invite dialog says "{count} seats left.". | F56 |

The standing rules of #218:

- Where a frame contradicts only a rule, and the principle of D4 to D16 fits, the frame wins and the rule itself changes. No value is invented, and there is one way to support.
- Arabic: the organisation's Admin role is مشرف (مسؤول only in مسؤول النظام). Email is البريد الإلكتروني. Singular or plural follows how many people a dialog invites.

## 4. Rejected options

Do not offer these again. Where an architecture rule or a roadmap step already holds the reason, the row points to it.

| Date | Rejected option | Why |
| --- | --- | --- |
| 2026-11-08 | The platform's `user.suspended` entry holds the reason | `docs/roadmap.md`, 37b-backend (A8) |
| 2026-11-10 | The status tabs' counts follow the search | `docs/roadmap.md`, 37b-backend |
| 2026-11-11 | Turn off "Require branches to be up to date" | Nothing would test E2E on the merged result (section 2). |
| 2026-11-11 | An organisation with no Owner after its last Owner leaves | Nobody could manage it (section 2). |
| 2026-11-11 | Rename "Organisation" to "Workspace" | A high cost and no B2C behaviour (section 2). |
| 2026-11-11 | Report the Wolverine race of #227 upstream | Wolverine fixed it in 6.63.0 (section 2). |
| 2026-11-11 | Partitions, or a DELETE grant, for the audit retention | R18, R19 |
| 2026-11-12 | The retention job's list message carries the period | K14 |
| 2026-11-13 | One key with 0 as off, in place of a dependent limit; a greyed hint only | E1; `docs/roadmap.md`, 37d |
| 2026-11-13 | ICU plural syntax in the notification texts, or a reworded Arabic sentence | A `Days` value kind is smaller, and correct in every language (U1, U2; #273). |
| 2026-11-14 | The Owner's consent before a support session | Support could not act when the Owner is unreachable (section 2, Support access's shape). |
| 2026-11-14 | Clerk's Enhanced Administration add-on from the start, or impersonation of our own | Section 2, the Clerk quota. |
| 2026-11-14 | A `SECURITY DEFINER` function that reads invitation addresses for the platform's log | A read path that bypasses row level security is a hole in tenant isolation. The platform sends one query for each tenant on the page instead (W3; #280). |
| 2026-11-14 | Two messages that check that an invitation is still pending before its email | It needs a hand-written retry count. A contract with its own short read-only transaction does it instead (W1; #280). |
| 2026-11-14 | One alert for each dead letter, or a Better Stack threshold | Section 2, the dead letter's alert. |
