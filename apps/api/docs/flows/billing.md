# Billing

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

A member who manages billing pays for a plan by card, through Stripe, and the tenant moves to the plan once Stripe says the subscription is active (B1 to B6; `docs/roadmap.md`, section 8, step 40b). Billing is off by default: while `Billing:Payments:Enabled` is false, every step below answers 404 `billing.off` (B1). Step 40b builds billing in parts; this file says what is built. The upgrade, the downgrade, the cancellation, the dunning, Contract and the price changes come in its later parts.

## 1. The first payment

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetCheckoutFactsQuery` | `POST /v1/tenants/{tenantId}/billing/checkout-session` | Billing, `GetCheckoutFactsHandler`, through ControlPlane's `IPlanDirectory`, `IMemberDirectory` and `IUserDirectory` | ControlPlane's DbContext, in the tenant of the route |
| `GetBillingQuery` | the same endpoint | Billing, `GetBillingHandler` | Billing's DbContext, in the tenant of the route |
| `GetCustomerQuery` | the same endpoint, after the rules | Billing, `GetCustomerHandler` | Billing's DbContext, in the tenant of the route |
| `CreateCustomerCommand` | the same endpoint, when the tenant has no customer yet | Billing, `CreateCustomerHandler`, which calls Stripe with the tenant's idempotency key | No |
| `KeepCustomerCommand` | the same endpoint | Billing, `KeepCustomerHandler` (the tenant's row of `billing.customers`, and the member who opens the checkout) | Billing's DbContext, in the tenant of the route |
| `ReserveCheckoutCommand` | the same endpoint | Billing, `ReserveCheckoutHandler`: the tenant's one open checkout of the order, locked by the tenant's customer (B7) | Billing's DbContext, in the tenant of the route |
| `ExpireCheckoutSessionCommand` | `ReserveCheckoutHandler`, for the page that a checkout of another order replaces | Billing, `ExpireCheckoutSessionHandler`, which calls Stripe | No |
| `OpenCheckoutSessionQuery` | the same endpoint, from `CheckoutOrders.Of`, with the tenant's customer and the checkout's key, when the checkout has no page yet | Billing, `OpenCheckoutSessionHandler`, which calls Stripe with the key | No |
| `RecordCheckoutSessionCommand` | the same endpoint | Billing, `RecordCheckoutSessionHandler`: the page of the key, unless another checkout replaced it since | Billing's DbContext, in the tenant of the route |

- The request names the plan, the interval (`monthly` or `yearly`) and the web app's page to come back to. The endpoint reads ControlPlane's facts (the member who pays and their address, the plan and its prices, the plan the tenant is on, the seated members), then Billing's row of the tenant, each in a message of its own module (W1).
- `CheckoutOrders.Of` checks, in this order: the page is under `Billing:Payments:WebUrl` with no query and no fragment (400 `billing.return_url_invalid`), the tenant has no subscription that has not ended (409 `billing.already_subscribed`), and the plan costs something for the seated members (422 `billing.plan_free`). An unknown plan is 404 `billing.plan_not_found`, from the facts. The order pays the base fee once and the fee per person for each member who takes a seat: a Billing manager takes none (A2), and a pending invitation is not charged until it is accepted.
- After the rules, the tenant's customer: the one in our map, or, at the first checkout, a new one at Stripe, with the tenant's id as Stripe's idempotency key, so two first checkouts get one customer. `billing.customers` keeps one row for each tenant, the first customer when two raced, and names the member who opens the checkout; a refused checkout creates none (B5).
- Stripe's page is made for the answer: the adapter finds or creates the plan's Product and the Prices of these amounts (B2), and opens Checkout in subscription mode for the tenant's customer, with the billing address and the VAT ID asked for. The tenant, the plan and the member go to Stripe as metadata, for support only: no read takes them from there (B5).
- Then the tenant's one open checkout (B7): the same order, while its page is being made or has more than 10 minutes left, answers the same page, so two members who ask at once, or one who asks twice, get one page; another order replaces the open checkout, and Stripe expires the replaced page. The page works for an hour. So the request takes no `Idempotency-Key` of its own (I1): its key at Stripe is the checkout's. The answer is `{ url }`; Stripe sends the person back with `checkout=success` or `checkout=cancelled` after the page's address.

## 2. Stripe's webhook

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetWebhookPayloadQuery` | `POST /v1/public/billing/webhook` | Billing, `GetWebhookPayloadHandler`, which checks Stripe's signature and reads no network | No |
| `ReceiveWebhookCommand` | the same endpoint | Billing, `ReceiveWebhookHandler` | Yes, without a tenant (R6) |
| `SyncSubscriptionCommand` | `ReceiveWebhookHandler`, for the first delivery of an event about a subscription | Billing, `SyncSubscriptionHandler`, which reads the subscription from Stripe; retried after `Billing:Provider:RetryDelays` | No |
| `RouteSubscriptionCommand` | `SyncSubscriptionHandler`, for a subscription of a plan of ours | Billing, `RouteSubscriptionHandler`, which finds the tenant by the customer in `billing.customers` | Yes, without a tenant (R6) |
| `ApplySubscriptionCommand` | `RouteSubscriptionHandler`, in the tenant of the customer | Billing, `ApplySubscriptionHandler` | Yes, the tenant's |
| `PlanPaidEvent` | `ApplySubscriptionHandler`, when an active subscription's row changed | ControlPlane, `ChangeBilledPlanHandler` | Yes, the tenant's |
| `TenantPlanChangedEvent` (`ByMember`) | `ChangeBilledPlanHandler`, when the plan changed | Audit, `RecordTenantPlanChangedHandler` (`tenant.plan_changed` in both logs, by the member who paid); Notifications' `NotifyTenantPlanChangedHandler` sends nothing for it, as its text says that the platform acted | Yes |

- The body is read as text, because Stripe signs its exact bytes. A body without Stripe's signature, with another one, or with one older than `Billing:Stripe:WebhookTolerance` answers 400 `billing.webhook_invalid` and keeps nothing (B4).
- The event's id goes into `billing.stripe_events` with `ON CONFLICT DO NOTHING`. Only the first delivery asks for the sync, through the outbox in the same transaction (O1). The endpoint answers 204, so Stripe stops sending the event. The events about a subscription are `checkout.session.completed` and `customer.subscription.created`, `updated`, `deleted`, `paused` and `resumed`; any other event is kept and asks for nothing.
- The sync reads the subscription from Stripe as it is now, and its plan from its Price's Product (B2, B5). One of no plan of ours is not ours, and nothing follows. The route finds the tenant by the subscription's customer in our own map, never in Stripe's metadata; a customer that is not in it is not ours. `ApplySubscriptionCommand` travels in that tenant (W3): the handler locks the tenant's row, writes the first one or takes the new state, and changes nothing for a state the row has (I1). A late read of an ended subscription never takes the row back from the subscription that replaced it.
- An active subscription whose row changed publishes `PlanPaidEvent`, with the member who opened the last checkout, removes the tenant's open checkout and expires its page (B7), so nobody pays a second time. ControlPlane locks the tenant (K6a) and puts it on the plan; the plan it is on announces nothing (B6). Any other status keeps the tenant's plan: an unpaid first invoice is `incomplete`.
- The tenant's purge deletes its rows (`flows/tenant-purge.md`), ends a subscription that has not ended at once, and expires an open checkout's page (B8); Stripe keeps its own records of the payments.

## 4. A closure and a reopen

- `TenantClosedEvent`: `StopBillingAtCloseHandler` asks Stripe to end the subscription at the end of the period it paid for (`SetCancelAtPeriodEndCommand`), and removes the open checkout and expires its page (`ExpireCheckoutSessionCommand`). Stripe's event of the change then brings `cancelAtPeriodEnd` to the tenant's row (B5).
- `TenantReopenedEvent`: `ResumeBillingAtReopenHandler` takes the end back while the subscription has not ended. A subscription that ended meanwhile stays ended; the tenant pays again by checkout.
- Each call to Stripe is a message of its own, with no transaction open, retried after `Billing:Provider:RetryDelays` (W11).

## 3. Reading and the portal

- `GET /v1/tenants/{tenantId}/billing` answers `{ subscription }`: the plan it pays for, the interval, the status, the end of the period it paid for and whether it ends then, or `null` before the first payment. Row level security keeps it to the tenant of the route.
- `POST /v1/tenants/{tenantId}/billing/portal-session` opens Stripe's customer portal for the tenant's customer in our map, to change the card, the address and the VAT ID and to read the invoices. A tenant that never opened a checkout has no customer (409 `billing.no_customer`). The page to come back to follows the rule of Checkout.
- Each of these needs `tenant.billing.manage`, which the Owner and the Billing manager hold (A2), and needs no feature of the plan (`NotPlanGated`, E5).

Tests: `OpenCheckoutSessionTests`, `ReceiveWebhookTests`, `TenantPurgeTests`, `CloseTenantTests`, `ReopenTenantTests`, `HostRoleTests`, `GetBillingTests`, `OpenPortalSessionTests`, `CheckoutOrdersTests`, `StripeBillingProviderTests`, `SubscriptionTests`, `RecordTenantPlanChangedTests`.

Source: Stripe, "Checkout", "Customer portal", "Webhooks", "Subscription statuses" and "Change prices". Richardson, *Microservices Patterns*, Chapter 3.
