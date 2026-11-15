# Billing

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

A member who manages billing pays for a plan by card, through Stripe, and the tenant moves to the plan once Stripe says the subscription is active (B1 to B6; `docs/roadmap.md`, section 8, step 40b). Billing is off by default: while `Billing:Payments:Enabled` is false, every step below answers 404 `billing.off` (B1). Step 40b builds billing in parts; this file says what is built. The upgrade, the downgrade, the cancellation, the dunning, Contract and the price changes come in its later parts.

## 1. The first payment

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetCheckoutFactsQuery` | `POST /v1/tenants/{tenantId}/billing/checkout-session` | Billing, `GetCheckoutFactsHandler`, through ControlPlane's `IPlanDirectory`, `IMemberDirectory` and `IUserDirectory` | ControlPlane's DbContext, in the tenant of the route |
| `GetBillingQuery` | the same endpoint | Billing, `GetBillingHandler` | Billing's DbContext, in the tenant of the route |
| `OpenCheckoutSessionQuery` | the same endpoint, from `CheckoutOrders.Of` | Billing, `OpenCheckoutSessionHandler`, which calls Stripe | No |

- The request names the plan, the interval (`monthly` or `yearly`) and the web app's page to come back to. The endpoint reads ControlPlane's facts (the member who pays and their address, the plan and its prices, the plan the tenant is on, the seated members), then Billing's row of the tenant, each in a message of its own module (W1).
- `CheckoutOrders.Of` checks, in this order: the page is under `Billing:Payments:WebUrl` with no query and no fragment (400 `billing.return_url_invalid`), the tenant has no subscription that has not ended (409 `billing.already_subscribed`), and the plan costs something for the seated members (422 `billing.plan_free`). An unknown plan is 404 `billing.plan_not_found`, from the facts. The order pays the base fee once and the fee per person for each member who takes a seat: a Billing manager takes none (A2), and a pending invitation is not charged until it is accepted.
- Stripe's page is made for the answer: the adapter finds or creates the plan's Product and the Prices of these amounts (B2), and opens Checkout in subscription mode, with the billing address and the VAT ID asked for. A tenant whose subscription ended pays again on its own customer. The tenant, the plan and the payer go to Stripe as metadata of the session and of the subscription.
- Nothing is stored, so the request takes no `Idempotency-Key` (I1): two requests open two pages, and only a payment makes a subscription. The answer is `{ url }`; Stripe sends the person back with `checkout=success` or `checkout=cancelled` after the page's address.

## 2. Stripe's webhook

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetWebhookPayloadQuery` | `POST /v1/public/billing/webhook` | Billing, `GetWebhookPayloadHandler`, which checks Stripe's signature and reads no network | No |
| `ReceiveWebhookCommand` | the same endpoint | Billing, `ReceiveWebhookHandler` | Yes, without a tenant (R6) |
| `SyncSubscriptionCommand` | `ReceiveWebhookHandler`, for the first delivery of an event about a subscription | Billing, `SyncSubscriptionHandler`, which reads the subscription from Stripe; retried after `Billing:Provider:RetryDelays` | No |
| `ApplySubscriptionCommand` | `SyncSubscriptionHandler`, in the tenant the subscription names | Billing, `ApplySubscriptionHandler` | Yes, the tenant's |
| `PlanPaidEvent` | `ApplySubscriptionHandler`, when an active subscription's row changed | ControlPlane, `ChangeBilledPlanHandler` | Yes, the tenant's |
| `TenantPlanChangedEvent` (`ByMember`) | `ChangeBilledPlanHandler`, when the plan changed | Audit, `RecordTenantPlanChangedHandler` (`tenant.plan_changed` in both logs, by the member who paid) | Yes |

- The body is read as text, because Stripe signs its exact bytes. A body without Stripe's signature, with another one, or with one older than `Billing:Stripe:WebhookTolerance` answers 400 `billing.webhook_invalid` and keeps nothing (B4).
- The event's id goes into `billing.stripe_events` with `ON CONFLICT DO NOTHING`. Only the first delivery asks for the sync, through the outbox in the same transaction (O1). The endpoint answers 204, so Stripe stops sending the event. The events about a subscription are `checkout.session.completed` and `customer.subscription.created`, `updated`, `deleted`, `paused` and `resumed`; any other event is kept and asks for nothing.
- The sync reads the subscription from Stripe as it is now (B5). One whose metadata names no tenant is not ours, and nothing follows. `ApplySubscriptionCommand` travels in the tenant it names (W3): the handler locks the tenant's row, writes the first one or takes the new state, and changes nothing for a state the row has (I1).
- An active subscription whose row changed publishes `PlanPaidEvent`. ControlPlane locks the tenant (K6a) and puts it on the plan; the plan it is on announces nothing (B6). Any other status keeps the tenant's plan: an unpaid first invoice is `incomplete`.
- The tenant's purge deletes its row (`flows/tenant-purge.md`); Stripe keeps its own records of the payments.

## 3. Reading and the portal

- `GET /v1/tenants/{tenantId}/billing` answers `{ subscription }`: the plan it pays for, the interval, the status, the end of the period it paid for and whether it ends then, or `null` before the first payment. Row level security keeps it to the tenant of the route.
- `POST /v1/tenants/{tenantId}/billing/portal-session` opens Stripe's customer portal for the tenant's customer, to change the card, the address and the VAT ID and to read the invoices. A tenant that never paid has no customer (409 `billing.no_customer`). The page to come back to follows the rule of Checkout.
- Each of these needs `tenant.billing.manage`, which the Owner and the Billing manager hold (A2), and needs no feature of the plan (`NotPlanGated`, E5).

Tests: `OpenCheckoutSessionTests`, `ReceiveWebhookTests`, `GetBillingTests`, `OpenPortalSessionTests`, `CheckoutOrdersTests`, `StripeBillingProviderTests`, `SubscriptionTests`, `RecordTenantPlanChangedTests`.

Source: Stripe, "Checkout", "Customer portal", "Webhooks", "Subscription statuses" and "Change prices". Richardson, *Microservices Patterns*, Chapter 3.
