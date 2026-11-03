# Roadmap

This document is the plan of the template's steps. Linear no longer tracks them. Each step has its scope, its decided points and its status: `done`, `in progress` or `todo`. A step's pull request marks its own row in section 1 and its own section. A decided point is closed: reopen it only when the maintainer asks.

## 1. Status

| Step | Name | Status |
| --- | --- | --- |
| 34 | Design | done, but the handoff package is todo |
| 35 | Cleanup, the shell and the menu | done |
| 36 | Personal pages | todo |
| 37 | Organisation pages | todo |
| 38 | Terms of use | todo |
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

- One Figma file holds the design of the web app and the platform app, on desktop and phone, in light and dark, in English and Arabic.
- The handoff package goes into `docs/design/` before the Figma seat ends, about 29 Nov 2026.
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

## 4. Step 36: Personal pages

Status: todo.

- Profile: the name, the language and the time zone.
- Account and security: the email change goes through our API. The API owns the email and updates the identity provider.
- Account and security also shows the sign-in methods, the sessions and the optional two-step verification. The frontend reads them from the identity provider through one folder.
- Self-service account deletion.
- Personal data export: it runs in the background. An email and a notification say when it is ready. The person can download it until a date. The file is in object storage.

## 5. Step 37: Organisation pages

Status: todo.

- General, Members, Roles and Activity log in the new design.
- A CSV export of Members and of the Activity log. There is no import.
- HTML emails in three languages, from "Starter <no-reply@starter.example>".
- The words: "Address" (never "slug"), "Seats", "Activity log" and "See the activity log".
- The organisation's Owners also receive tenant.suspended, tenant.reactivated, tenant.closed, tenant.reopened and tenant.purged, by bell and email, with their own texts in three languages.
- The pages move to Pill and Label, and Status and Tag are deleted.

## 6. Step 38: Terms of use

Status: todo.

- The platform manages the versions. A version has an effective date, a link, an optional summary, and whether it requires acceptance.
- An effective date is never in the past.
- One scheduled version at a time. A system admin can edit or cancel it before it takes effect.
- A version in force never changes.
- A person accepts the version after sign-in.
- A public endpoint gives the version in force. The "Terms" link before sign-in opens its link.

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
- A new permission, `tenant.billing.manage`. The Owner holds it by default.
- A fourth built-in role, Billing manager, with `tenant.profile.read`, `tenant.members.read` and `tenant.billing.manage`. It is free: it takes no seat and no fee per person. Product modules never add permissions to it.

40b, Stripe:

- Checkout takes the first payment.
- The customer portal manages the card, the address, the VAT ID and the invoices. The email change, the plan switch and the cancellation are off in the portal.
- An upgrade applies at once, with proration.
- A downgrade or a cancellation applies at the end of the period. The person can undo it until then.
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

- The production environment.
- The first release.
- Rollback and a restore drill.
- The Clerk email templates and the sign-in lockout, set on purpose.

## 16. Later: Template 1.1 and after

Status: todo.

- Support access.
- The status page.
- Enterprise SSO.
- Public API keys.
- Outgoing webhooks.
- Notification settings for activity categories.
- Prepaid credits.
- Seats included in a base fee.
- A native review of the Turkish and Arabic texts.
