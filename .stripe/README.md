# Stripe settings

Stripe takes the payments of billing, while billing is on (`apps/api/docs/ARCHITECTURE.md`, section 6, "Billing"). `config.json` holds the settings that Stripe's API can manage and that the template decides: the customer portal's features and the API's webhook endpoint. `scripts/stripe/config.sh` plans and applies them (`pnpm stripe:plan`, `pnpm stripe:apply`), like Clerk's and Better Stack's settings. Agents may run `pnpm stripe:plan`; only the maintainer runs `pnpm stripe:apply` (`AGENTS.md`, "Repository settings").

The file holds no value: no key, no URL and no secret. The script reads the account's secret key from `STRIPE_SECRET_KEY` and the API's root from `API_URL`. A test key (`sk_test_...`) changes the account's test mode, for staging; a live key (`sk_live_...`) its live mode, for production.

## What the file holds

- `portal.features`: the features of the account's default customer portal configuration. A person changes the card, the address, the name, the phone and the VAT ID, and reads the invoices. The email change, the plan switch and the cancellation are off in the portal (`docs/roadmap.md`, section 8, 40b): the email is ControlPlane's (A7), and a plan change or a cancellation goes through our app, which keeps the rules.
- `webhook`: the path of the API's webhook endpoint, after `API_URL`, and the events it listens to. The API keeps any other event and does nothing with it (B4).

## What the API does not manage

Checked on 2026-11-15 against Stripe's API reference.

| Setting | Where it lives | Source |
| --- | --- | --- |
| The products and prices of the plans | Our app writes them at the first checkout of each amount (B2), so nobody creates them | `apps/api/docs/ARCHITECTURE.md`, B2 |
| Stripe's own emails of a failed payment | Off, by hand: Dashboard, Settings, Billing, Subscriptions and emails, "Customer emails". Our app sends those emails, so a person gets one email, not two (`docs/roadmap.md`, section 8) | Stripe docs, "Customer emails"; no API |
| Smart Retries, the dunning schedule | By hand: Dashboard, Settings, Billing, Subscriptions and emails, "Manage failed payments": retry for about 2 weeks, then leave the subscription past due. Our app's grace follows (a later part of 40b) | Stripe docs, "Automate payment retries"; no API |
| The webhook's signing secret | Printed once by `pnpm stripe:apply` when it creates the endpoint; it goes into `Billing:Stripe:WebhookSecret` | Stripe API, "Create a webhook endpoint" (`secret` is returned on create only) |

## The first setup

1. In Stripe's Dashboard, take the secret key of the mode: test for staging, live for production. Keep it in the password manager.
2. Run `STRIPE_SECRET_KEY=... API_URL=... pnpm stripe:plan`, then `pnpm stripe:apply` with the same variables. Copy the signing secret it prints.
3. In Railway, set `Billing:Stripe:SecretKey`, `Billing:Stripe:WebhookSecret`, `Billing:Payments:WebUrl` and then `Billing:Payments:Enabled` on the API service (`apps/api/docs/configuration.md`).
4. Set the two settings that the API does not manage, above.
