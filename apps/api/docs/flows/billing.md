# Billing

This file is a flow of `ARCHITECTURE.md`, section 6. It says what happens, step by step. The rules it cites by id are in `ARCHITECTURE.md` and `api-catalog.md`.

A member who manages billing pays for a plan by card, through Stripe, and the tenant moves to the plan once Stripe says the subscription is active (B1 to B6; `docs/roadmap.md`, section 8, step 40b). Billing is off by default: while `Billing:Payments:Enabled` is false, every step below answers 404 `billing.off` (B1). Step 40b builds billing in parts; this file says what is built. Its later parts are the billing emails.

## 1. The first payment

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetCheckoutFactsQuery` | `POST /v1/tenants/{tenantId}/billing/checkout-session` | Billing, `GetCheckoutFactsHandler`, through ControlPlane's `IPlanDirectory`, `IMemberDirectory` and `IUserDirectory` | ControlPlane's DbContext, in the tenant of the route |
| `GetBillingQuery` | the same endpoint | Billing, `GetBillingHandler` | Billing's DbContext, in the tenant of the route |
| `GetCustomerQuery` | the same endpoint, after the rules | Billing, `GetCustomerHandler` | Billing's DbContext, in the tenant of the route |
| `CreateCustomerCommand` | the same endpoint, when the tenant has no customer yet | Billing, `CreateCustomerHandler`, which calls Stripe with the tenant's idempotency key | No |
| `KeepCustomerCommand` | the same endpoint | Billing, `KeepCustomerHandler` (the tenant's row of `billing.customers`, and the member who opens the checkout) | Billing's DbContext, in the tenant of the route |
| `ReserveCheckoutCommand` | the same endpoint | Billing, `ReserveCheckoutHandler`: the tenant's one open checkout of the order, after a lock of the tenant's customer; 409 `billing.tenant_closed` for a closed tenant (B7, B8) | Billing's DbContext, in the tenant of the route |
| `ExpireCheckoutPagesCommand` | `ReserveCheckoutHandler`, for the key it replaces; `RecordCheckoutSessionHandler`, for a page of a replaced key; `AbandonCheckoutHandler`; a payment, a closure and the purge, for the open checkout's key | Billing, `ExpireCheckoutPagesHandler`, which expires the open pages of the customer that the key made, by the key in their metadata | No |
| `AbandonCheckoutCommand` | the same endpoint, when Stripe's call fails | Billing, `AbandonCheckoutHandler`: gives up the key, as Stripe keeps a failed result under it | Billing's DbContext, in the tenant of the route |
| `OpenCheckoutSessionQuery` | the same endpoint, from `CheckoutOrders.Of`, with the tenant's customer and the checkout's key, when the checkout has no page yet | Billing, `OpenCheckoutSessionHandler`, which calls Stripe with the key | No |
| `RecordCheckoutSessionCommand` | the same endpoint | Billing, `RecordCheckoutSessionHandler`: the page of the key; a page of a key that another checkout replaced since is expired, and the request answers 409 `billing.checkout_replaced` | Billing's DbContext, in the tenant of the route |

- The request names the plan, the interval (`monthly` or `yearly`) and the web app's page to come back to. The endpoint reads ControlPlane's facts (the member who pays and their address, the plan and its prices, the plan the tenant is on, the seated members), then Billing's row of the tenant, each in a message of its own module (W1).
- `CheckoutOrders.Of` checks, in this order: the page is under `Billing:Payments:WebUrl` with no query and no fragment (400 `billing.return_url_invalid`), the tenant has no subscription that has not ended (409 `billing.already_subscribed`), and the plan costs something for the seated members (422 `billing.plan_free`). An unknown plan is 404 `billing.plan_not_found`, from the facts. The order pays the base fee once and the fee per person for each member who takes a seat: a Billing manager takes none (A2), and a pending invitation is not charged until it is accepted.
- After the rules, the tenant's customer: the one in our map, or, at the first checkout, a new one at Stripe, with the tenant's id as Stripe's idempotency key, so two first checkouts get one customer. `billing.customers` keeps one row for each tenant, the first customer when two raced, and names the member who opens the checkout; its insert gives way on either key, so two first checkouts at once never fail on it; a refused checkout creates none (B5).
- Stripe's page is made for the answer: the adapter finds or creates the plan's Product and the Prices of these amounts (B2), and opens Checkout in subscription mode for the tenant's customer, with the billing address and the VAT ID asked for. The tenant, the plan and the member go to Stripe as metadata, for support only: no read takes them from there (B5).
- Then the tenant's one open checkout (B7): the same order (plan, interval, seats, currency, amounts and the page to come back to) keeps its key, its expiry and so the very same call to Stripe while its page has more than 10 minutes left, or, without a page yet, for the first 20 minutes; Stripe answers the same page, so two members who ask at once, or one who asks twice, get one page. Another order or a new price takes a new key, and Stripe expires the pages of the replaced key. The page works for an hour from the reservation.

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
- The event's id goes into `billing.stripe_events` with `ON CONFLICT DO NOTHING`. Only the first delivery asks for the sync, through the outbox in the same transaction (O1). The endpoint answers 204, so Stripe stops sending the event. The events about a subscription are `checkout.session.completed`, `customer.subscription.created`, `updated`, `deleted`, `paused` and `resumed`, and an invoice's `invoice.payment_failed` and `invoice.paid` (section 7); any other event is kept and asks for nothing.
- The sync reads the subscription from Stripe as it is now, and its plan from its Price's Product (B2, B5). One of no plan of ours is not ours, and nothing follows. Every change locks the tenant's customer first, then its subscription, then its checkout (B7, K6c). The route finds the tenant by the subscription's customer in our own map, never in Stripe's metadata; a customer that is not in it is not ours. `ApplySubscriptionCommand` travels in that tenant (W3): the handler locks the tenant's row, writes the first one or takes the new state, and changes nothing for a state the row has (I1). A late read of an ended subscription never takes the row back from the subscription that replaced it.
- An active subscription whose row changed publishes `PlanPaidEvent`, with the member who opened the last checkout, removes the tenant's open checkout and expires its key's pages (B7), so nobody pays a second time. A second subscription that arrives while one has not ended ends at once, its payment is refunded in full (`RefundSubscriptionCommand`, retried), and the tenant keeps the first. ControlPlane locks the tenant (K6a) and puts it on the plan; the plan it is on announces nothing (B6). Any other status keeps the tenant's plan: an unpaid first invoice is `incomplete`.
- The tenant's purge deletes its rows (`flows/tenant-purge.md`), ends a subscription that has not ended at once, and expires an open checkout's page (B8); Stripe keeps its own records of the payments.

## 4. A closure and a reopen

- `TenantClosedEvent`: `StopBillingAtCloseHandler` locks the tenant's customer and takes the closure when it is newer than the last closure or reopen billing took; it marks the customer closed, asks Stripe to end the subscription at the end of the period it paid for (`SetCancelAtPeriodEndCommand`) and records that the closure asked for it, and removes the open checkout and expires its key's pages (`ExpireCheckoutPagesCommand`). A closed tenant opens no checkout, and a subscription that arrives later ends at its period's end too. Stripe's event of the change then brings `cancelAtPeriodEnd` to the tenant's row (B5).
- `TenantReopenedEvent`: `ResumeBillingAtReopenHandler` takes the reopen when it is newer, clears the customer's closure and takes back only the end that the closure asked for, while the subscription has not ended. An end that a member asked for stays. Our calls can reach Stripe in another order than we sent them, so each change of the subscription at Stripe asks again for what we asked until its state shows it (B8). A subscription that ended meanwhile stays ended; the tenant pays again by checkout.
- Each call to Stripe is a message of its own, with no transaction open, retried after `Billing:Provider:RetryDelays` (W11).

## 3. Reading and the portal

- `GET /v1/tenants/{tenantId}/billing` answers `{ subscription }`: the plan it pays for, the interval, the status, the end of the period it paid for and whether it ends then, and the move scheduled then (section 5), or `null` before the first payment. Row level security keeps it to the tenant of the route.
- `POST /v1/tenants/{tenantId}/billing/portal-session` opens Stripe's customer portal for the tenant's customer in our map, to change the card, the address and the VAT ID and to read the invoices. A tenant that never opened a checkout has no customer (409 `billing.no_customer`). The page to come back to follows the rule of Checkout.
- Each of these needs `tenant.billing.manage`, which the Owner and the Billing manager hold (A2), and needs no feature of the plan (`NotPlanGated`, E5).

## 5. A plan change

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetPlanChoicesQuery` | `GET /v1/tenants/{tenantId}/plans` | ControlPlane, `GetPlanChoicesHandler` | ControlPlane's DbContext |
| `GetCheckoutFactsQuery` | `POST .../billing/plan-change`, its preview and `DELETE .../billing/scheduled-change`: once for the target plan, once for the plan the subscription pays for | Billing, `GetCheckoutFactsHandler` | ControlPlane's DbContext, in the tenant of the route |
| `GetBillingQuery` | the same endpoints | Billing, `GetBillingHandler` | Billing's DbContext, in the tenant of the route |
| `GetPlanChangePreviewQuery` | the preview, for a move at once | Billing, `GetPlanChangePreviewHandler`, which asks Stripe for the prorated invoice | No |
| `BeginPlanChangeCommand` | the change and the undo | Billing, `BeginPlanChangeHandler`: locks the tenant's customer, then its subscription (B7); a closed tenant (409 `billing.tenant_closed`), no subscription that has not ended (409 `billing.no_subscription`), one that is not active (409 `billing.subscription_not_active`), an undo with nothing scheduled (409 `billing.nothing_scheduled`); names the member, and for a cancellation or an undo sets `closure_end` to `None` (B8, B9) | Billing's DbContext, in the tenant of the route |
| `ChangePlanNowCommand` | the change, for a move at once | Billing, `ChangePlanNowHandler`, which moves the subscription with proration, charged at once, then releases a schedule and clears a member's end; false when the card was declined, with nothing changed | No |
| `SchedulePlanChangeCommand` | the change, for a move at the period's end | Billing, `SchedulePlanChangeHandler`, which writes the subscription's schedule: today's phase until the period's end, then one period of the new plan, then release, which replaces a member's end in the same call | No |
| `CancelAtPeriodEndCommand` | the change, for the default plan | Billing, `CancelAtPeriodEndHandler`, which releases a schedule and sets `cancel_at_period_end`; not retried in the request | No |
| `UndoScheduledChangeCommand` | the undo | Billing, `UndoScheduledChangeHandler`, which releases the schedule and clears `cancel_at_period_end` | No |
| `SyncSubscriptionCommand` | the change and the undo, after Stripe's call | Billing, as for a webhook (section 2) | No |
| `NameScheduleChangeCommand` | `ApplySubscriptionHandler`, when the state that the row takes shows a member's cancellation, its undo, a scheduled move or its undo | Billing, `NameScheduleChangeHandler`, which names the tenant (`ITenantNames`) and the plans (`IPlanDirectory`; the default plan for a cancellation) | ControlPlane's DbContext, in the tenant |
| `PlanCancelledEvent`, `PlanCancellationUndoneEvent`, `PlanChangeScheduledEvent`, `PlanChangeUndoneEvent` | `NameScheduleChangeHandler` | Audit, `RecordPlanCancelledHandler` and the others (`tenant.plan_cancelled`, `tenant.plan_cancellation_undone`, `tenant.plan_change_scheduled`, `tenant.plan_change_undone`, in both logs, by the member) | Yes, the tenant's |

- The Change plan page reads every plan with its values, its prices and its meters, the cheapest monthly price first, and the default plan marked (10.02). It needs `tenant.billing.manage`.
- The request names the plan and the interval. The default plan is the cancellation. `PlanMoves` decides the move from both plans' prices for the seated members (B9): a plan that costs more in the same interval, or monthly to yearly, applies at once (`at_once`); one that costs the same or less, or yearly to monthly, at the end of the paid period (`at_period_end`); the default plan at the end of the paid period too (`cancellation`). The plan and interval the subscription pays for, or a cancellation that is asked already, answer 409 `billing.plan_unchanged`. A plan that costs nothing and is not the default answers 422 `billing.plan_free`.
- The preview answers the move, when it applies, the currency and the new plan's amount for each interval, without tax. For a move at once it also answers what the rest of the period costs today, without tax, and the moment it was prorated at (10.04). The change names that moment, so Stripe charges the amount that the preview showed; a moment older than 30 minutes answers 400 `billing.preview_expired`.
- A move at once moves the subscription's items to the new plan's Prices (B2) and charges the prorated amount at once. A declined card leaves the subscription as it was and answers 422 `billing.payment_declined`. A scheduled move or a member's cancellation is dropped by a move at once.
- After Stripe's call, the endpoint asks for the sync of the subscription, so the page need not wait for the webhook; the webhook's sync then finds the same state (I1). The tenant's plan changes as after a checkout (B6), by the member who changed it, in both audit logs. A scheduled move shows in `GET .../billing` as `scheduledChange` `{ planId, interval }`, and a cancellation as `cancelAtPeriodEnd` (10.07). At the period's end, Stripe moves the subscription, and its event brings the new plan. The end of a cancelled subscription moves the tenant to the default plan (section 7).
- A closure's end replaces a scheduled move, and a reopen brings the move not back (B8, B9).
- `DELETE .../billing/scheduled-change` undoes a scheduled move or a member's cancellation; with nothing scheduled it answers 409 `billing.nothing_scheduled`.
- The Activity log (12.12): the sync compares the state that the row takes with the row (`Subscription.ScheduleChangeOf`). The same subscription, still active on the same plan and interval, that now ends at the period's end is a cancellation; one that ends no more, with nothing scheduled, its undo; a new scheduled plan or interval a scheduled move; a schedule gone, with no end, its undo. A move scheduled after a cancellation is a move, and a cancellation after a move is a cancellation. A move at once, the scheduled move when it applies, and a subscription that ends are no such change: the plan change records `tenant.plan_changed`. Nothing is recorded while the tenant is closed or `closure_end` is not `None`, as that end is the closure's or the reopen's, which `tenant.closed` and `tenant.reopened` record. The entry names the member in `checkout_by`, who changed the plan last; a change made in Stripe's dashboard names them too. A tenant that a purge deleted, or a plan that is gone, before the names are read records nothing. A known gap: a cancellation after a scheduled move, and a move after a cancellation, are two or three calls to Stripe in a row; a webhook's sync that reads Stripe between them takes the state in between and records an undo before the real entry.

## 6. The seats

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `MemberJoinedEvent`, `MemberRemovedEvent`, `MemberLeftEvent`, `MemberRoleChangedEvent`, `OwnerMadeEvent`, `UserMembershipRemovedEvent` | ControlPlane | Billing, `CountSeatsAtMemberChangeHandler`, which reads the tenant's subscription | Billing's DbContext, in the tenant of the event |
| `CountSeatsCommand` | `CountSeatsAtMemberChangeHandler`, for a subscription that has not ended; `ApplySubscriptionHandler`, for each state of a subscription that has not ended and has a fee per person, with Stripe's count | Billing, `CountSeatsHandler`, through ControlPlane's `IMemberDirectory` | ControlPlane's DbContext, in the tenant |
| `SetSeatsCommand` | `CountSeatsHandler`, when the count differs from the one it was given | Billing, `SetSeatsHandler`, which calls Stripe; retried after `Billing:Provider:RetryDelays` | No |

- The fee per person counts the members who take a seat: every member whose role is not the Billing manager (A2). A pending invitation is not charged; the join of its person is (B10).
- Each change of who takes a seat counts the seats again: a person accepts an invitation, an Owner or an Admin removes a member, a member leaves, a member's role changes, the platform makes a member an Owner, and a system admin deletes a user. A tenant that never paid by card has no subscription, and nothing follows.
- Stripe changes only the quantity of the fee per person, at its own Price (B2). A join on a yearly subscription is invoiced at once. A removal, and any change of a monthly subscription, is prorated on the next invoice. A subscription without a fee per person, one that has ended, or one with this count already changes nothing.
- A subscription that a schedule manages (section 5) takes the count in each phase from the current one on, so the move at the period's end keeps the count.
- Two changes at once can reach Stripe in another order. Each state of the subscription that the sync reads brings Stripe's count with it, and the sync counts the seats again and sets a count that differs, so the last count wins (I1).

## 7. A failed payment and the end of a subscription

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `NamePaymentFailureCommand` | `ApplySubscriptionHandler`, for a payment that failed for the first time on an invoice | Billing, `NamePaymentFailureHandler`, which names the tenant (`ITenantNames`) | ControlPlane's DbContext, in the tenant |
| `PaymentFailedEvent` (the card, the first failure and the next attempt) | `NamePaymentFailureHandler` | Audit, `RecordPaymentFailedHandler` (`tenant.payment_failed` in both logs, by the platform); Notifications, `NotifyTenantPaymentFailedHandler` (13.18, 3.17), when Stripe tries again | Yes, the tenant's |
| `PaymentGraceStartedEvent` | `ApplySubscriptionHandler`, when the grace starts | Notifications, `NotifyTenantPaymentGraceStartedHandler` (the last warning, 13.19, 3.18), which names the tenant and the default plan (`ITenantNames`, `IPlanDirectory`) | Yes, the tenant's |
| `EndGraceCommand` | `ApplySubscriptionHandler`, scheduled at the grace's end, once Stripe's retries are over | Billing, `EndGraceHandler`: locks the customer, then the subscription (B7) | Billing's DbContext, in the tenant |
| `PlanEndedEvent` (an unpaid one with its open invoice) | `EndGraceHandler` (`Unpaid`); `ApplySubscriptionHandler`, for a subscription that ended | ControlPlane, `MoveToDefaultPlanHandler` | Yes, the tenant's |
| `TenantPlanChangedEvent` (`ByMember` or `Unpaid`, with the open invoice) | `MoveToDefaultPlanHandler`, when the plan changed | Audit, `RecordTenantPlanChangedHandler` (`tenant.plan_changed` by the member, or `tenant.moved_to_default_plan` by the platform, in both logs); Notifications, `NotifyTenantMovedToDefaultPlanHandler` for `Unpaid` (13.20, 3.18), and nothing for `ByMember` | Yes |

- Stripe tries the invoice of a new period. A failed payment leaves the subscription `past_due`, and Stripe retries it for about two weeks (its Smart Retries, `.stripe/README.md`). Its events, `invoice.payment_failed` and `invoice.paid`, ask for the sync of the invoice's subscription (section 2).
- The sync reads the latest invoice with the subscription. An open invoice that Stripe tried, and whose payment failed, is the row's failed payment: the amount due without tax, the currency, when it failed, the attempts, the next attempt, and the card that Stripe tried (B11).
- The first failure of an invoice records `tenant.payment_failed` in both logs, with the amount and the end of Stripe's retries, `Billing:Payments:RetryWindow` after the failure (12.12, 7.11). The next attempts of the same invoice record nothing.
- While Stripe retries, the tenant keeps its plan. `GET .../billing` answers `payment.state` `retrying`, with `nextAttemptAt` (10.15).
- Once the invoice has no next attempt, billing's grace starts once and runs for `Billing:Payments:Grace` (7 days). `EndGraceCommand` is scheduled at its end. The tenant keeps its plan, and `payment.state` is `grace`, with `graceEndsAt` (10.16).
- At the grace's end, an unpaid subscription moves the tenant to the default plan, once. The module's clock decides: a message that arrives early, a grace that a payment ended, or one that another grace replaced moves nothing. ControlPlane locks the tenant, puts it on the default plan, and deletes nothing (E4). Both logs record `tenant.moved_to_default_plan`, by the platform. `payment.state` is `moved_to_default`, with `movedAt` (10.17).
- Each step tells the members whose role holds `tenant.billing.manage`, at that moment, in the tenant's bell and by email, and leads to Billing: a failure that Stripe retries, with the card, the amount and the next attempt (`tenant.payment_failed`, 13.18); the grace's start, the last warning, with the attempts and the grace's end (`tenant.payment_grace_started`, 13.19, named apart, U9: the Activity log has no entry of it); the move, with the open invoice that pays the plan back (`tenant.moved_to_default_plan`, 13.20). A failure with no next attempt starts the grace at once, so only the last warning tells of it. A Contract tenant hears none of them (B12).
- The subscription stays past due at Stripe. A payment of its open invoice makes it active, the sync clears the failed payment, the grace and the move, and the tenant is back on its plan at once, as after a checkout (B6).
- A subscription that ends moves the tenant to the default plan: one that a member cancelled, at the end of its period, by that member (`tenant.plan_changed`); one that ended unpaid by the platform (`tenant.moved_to_default_plan`). A tenant on the default plan already, after the grace, changes nothing. A closed tenant's plan is read-only, so its end changes nothing (E2).

## 8. Contract

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetTenantBillingQuery` | `PUT /v1/platform/tenants/{tenantId}/billing/billed-by` and `GET /v1/platform/tenants/{tenantId}/billing` | Billing, `GetTenantBillingHandler`, through ControlPlane's `ITenantNames` and `IUserDirectory`: the tenant's name, none for a tenant that does not exist (404 `billing.tenant_not_found`), and the system admin's user id | ControlPlane's DbContext, in the tenant of the route |
| `SetBilledByCommand` | the switch | Billing, `SetBilledByHandler`: locks the tenant's customer, when it has one, then the mark (B12), the subscription and the checkout (B7) | Billing's DbContext, in the tenant of the route |
| `SetCancelAtPeriodEndCommand`, `ExpireCheckoutPagesCommand` | `SetBilledByHandler` | Billing, as for a closure (section 4) | No |
| `ContractStartedEvent`, `ContractEndedEvent` | `SetBilledByHandler` | Audit, `RecordContractStartedHandler` and `RecordContractEndedHandler` (`tenant.contract_started` and `tenant.contract_ended`, by the system admin in the platform's log and by the platform in the tenant's) | Yes, the tenant's |
| `GetContractQuery` | the tenant's and the platform's reads, Checkout, the portal, the plan change, its preview and its undo | Billing, `GetContractHandler` | Billing's DbContext, in the tenant |

- A tenant pays by card by default. A system admin marks it Contract, and it is billed outside the app by invoice (B12, 4.34, 4.35). The mark is a row of `billing.contracts`, so a tenant that never paid by card can be Contract too. `billedBy` in both reads says `card` or `contract` (10.19).
- To Contract: Stripe ends the subscription at the end of the period it paid for, with no refund. The subscription takes the closure's mark (`closure_end` `Asked`), so each later state of Stripe asks for the end again (section 4), and the Activity log records no member's cancellation. The open checkout goes, and Stripe expires its pages.
- Back to card: the end that Contract asked for is taken back while the tenant is not closed, as a reopen does. A reopen takes back no end while the tenant is Contract. The members pay again by checkout once the subscription has ended.
- The same state again answers 200 and records nothing (I1).
- A Contract tenant's members open no checkout and no portal, and change, preview or undo no plan: 409 `billing.contract`. Checkout checks it again under the mark's lock, and the plan change under the customer's lock. The mark's lock is the tenant's advisory lock, as a tenant may have no customer to lock: a switch that comes after a first checkout finds its customer again and expires its page.
- A system admin sets a Contract tenant's plan (`ChangeTenantPlan`). Its last subscription, while it runs out, puts the tenant on no plan and moves it nowhere when it ends: no `PlanPaidEvent`, no `PlanEndedEvent`. It sets no seats at Stripe, and a failed payment records nothing and starts no grace.
- The purge deletes the mark.

## 9. A price change

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `PlanUpdatedEvent` (`PriceChange`, `Currency`) | ControlPlane's `UpdatePlanHandler`, when the edit changed the prices | Billing, `RecordPriceChangeHandler`, which keeps the change once (`billing.price_changes`) and schedules its notice and its move | Billing's DbContext, without a tenant (R6) |
| `PriceChangeScheduledEvent` | `RecordPriceChangeHandler` and `MoveToNewPriceHandler`, for a move with a day | Audit, `RecordPriceChangeScheduledHandler` (`plan.price_change_scheduled`, in the platform's log, by the system admin) | Yes |
| `AnnouncePriceChangeCommand` | the change, at once for a move 30 days away, else scheduled 30 days before the move | Billing, `AnnouncePriceChangeHandler`: only the plan's latest change; one message for each payer on another price | Billing's DbContext, without a tenant |
| `AnnounceTenantPriceChangeCommand` | `AnnouncePriceChangeHandler`, in the payer's tenant (W3) | Billing, `AnnounceTenantPriceChangeHandler`: no Contract tenant, no payer that ends, left the plan or pays the new price | Billing's DbContext, in the tenant |
| `NamePriceChangeCommand` | `AnnounceTenantPriceChangeHandler` | Billing, `NamePriceChangeHandler`, which names the tenant (`ITenantNames`) | ControlPlane's DbContext, in the tenant |
| `PriceChangeAnnouncedEvent` | `NamePriceChangeHandler` | Audit, `RecordPriceChangeAnnouncedHandler` (`tenant.price_change_announced`, in the tenant's log, by the platform); Notifications, `NotifyTenantPriceChangeAnnouncedHandler` (13.21, 3.17) | Yes, the tenant's |
| `MovePlanPricesCommand` | the change, scheduled at its day | Billing, `MovePlanPricesHandler`: only the plan's latest change, on its day; one message for each payer on another price | Billing's DbContext, without a tenant |
| `MoveSubscriptionPriceCommand` | `MovePlanPricesHandler`, in the payer's tenant | Billing, `MoveSubscriptionPriceHandler`: locks the customer, then the subscription (B7) | Billing's DbContext, in the tenant |
| `MovePricesCommand` | `MoveSubscriptionPriceHandler` | Billing, `MovePricesHandler`, which moves the subscription's items to the new Prices with no proration, then asks for the sync; retried | No |
| `GetPriceChangeFactsQuery`, `GetPriceChangeQuery` | `GET` and `POST /v1/platform/plans/{planId}/price-change...` | Billing, through ControlPlane's `IPlanDirectory` and `IUserDirectory`, then billing's payers and changes | ControlPlane's DbContext, then Billing's, without a tenant |
| `MoveToNewPriceCommand` | `POST .../price-change/move` | Billing, `MoveToNewPriceHandler`: locks the plan's latest change; nobody on an old price 409 `billing.nothing_to_move` | Billing's DbContext, without a tenant |
| `GetTenantPriceChangeQuery` | `GET .../billing`, the tenant's and the platform's | Billing, `GetTenantPriceChangeHandler` | Billing's DbContext, in the tenant |

- A Price never changes, so a plan's new amounts are new Prices (B2). Checkout and a plan change pay them at once; a payer keeps the Price it pays until its subscription moves.
- The plan's edit names who pays the new price (6.14): `everyone_after_notice` moves each payer 30 days after the edit, and tells it at once; `everyone_from` moves each payer on the set day, at least 30 days away, and tells it 30 days before; `new_customers_only`, the default, moves nobody. "Move to the new price" (6.16) moves the payers on an old price after 30 days' notice; a move that is scheduled already keeps its day.
- The move changes the subscription's Prices with the same quantities and no proration, so each payer pays the new price from its first period after the day; a yearly payer keeps its price until it renews. A Contract tenant, a closed one, a subscription that is not active, ends at its period's end or has a scheduled move, and one that pays the new price already, move nowhere.
- Only the plan's latest change tells or moves anybody, and a move that arrives before its day moves nobody.
- `billing.payers` keeps what each tenant pays: the plan, the interval and the amounts of Stripe's Prices, from each state of its subscription, in the tenant's transaction. An ended subscription deletes the row; one that ends at its period's end, or a Contract tenant, is marked as ending. The platform counts a plan's payers and the ones on an old price from it (6.14, 6.15).
- The notice tells the members whose role holds `tenant.billing.manage`, in the tenant's bell and by email (13.21, 3.17): the fee per person, the base fee or both, in the interval the tenant pays, each fee that changes with its old amount and one that stays with "stays", and the totals for the people who take a seat at the notice.
- A member who manages billing reads the change from its notice to its day, as `subscription.priceChange` of `GET .../billing` (10.18).
- While billing is off, nothing follows a price change.
- The notice, the tenant's read and the move ask the same of the subscription (`PriceMoves`), so nobody is told of a move that skips it while its state stays the same. Each notice has one event id for the change and the tenant, so a copy of a message records no second entry.
- Known gaps: a move of another plan or interval that a member scheduled at the period's end (B9) takes the Prices of its own moment; a subscription that is past due on the day was told and is not moved, until a later change or "Move to the new price"; a subscription of before this part has no payer row until Stripe sends its next state.

## 10. The billing email

| Message | Sent by | Handled by | Handler has a transaction |
| --- | --- | --- | --- |
| `GetMemberQuery` | `PUT /v1/tenants/{tenantId}/billing/email` | Billing, `GetMemberHandler`, the member's user id (`IUserDirectory`) | ControlPlane's DbContext, in the tenant |
| `SetBillingEmailCommand` | the same endpoint | Billing, `SetBillingEmailHandler`: locks the customer first, then the row (B7) | Billing's DbContext, in the tenant |
| `NameBillingEmailChangeCommand` | `SetBillingEmailHandler`, for a change | Billing, `NameBillingEmailChangeHandler`, which names the tenant (`ITenantNames`) | ControlPlane's DbContext, in the tenant |
| `BillingEmailChangedEvent` | `NameBillingEmailChangeHandler` | Audit, `RecordBillingEmailChangedHandler` (`tenant.billing_email_changed`, in the tenant's log, by the member) | Yes, the tenant's |
| `NameCustomerEmailCommand` | `SetBillingEmailHandler`, for a tenant with a customer | Billing, `NameCustomerEmailHandler`: the address, or the address of the member who paid first (`IUserDirectory`) | ControlPlane's DbContext, in the tenant |
| `KeepCustomerEmailCommand` | `NameCustomerEmailHandler` | Billing, `KeepCustomerEmailHandler`, which gives Stripe the customer's email; retried | No |

- A member who manages billing sets the billing email, changes it, or removes it with an empty address (10.01, B14). The address needs no account. The same address again changes nothing.
- Stripe sends its invoices and receipts to the customer's one address, so the billing email is the customer's email. Without one, it is the address of the member who paid first. A tenant that sets it before its first payment gets a customer with it.
- Billing's events carry the billing email and the member who set it: the failed payment, the grace's start, the unpaid move and the price change's notice (sections 7 and 9). Each email of these goes to the address too, by email only, in that member's language and time zone, or in English and UTC when that member is no user any more. An address that is also a told member's email gets one email.
- `GET .../billing` answers `billingEmail`. The tenant's log records the change without the address. A purge deletes the row.
- Known gaps: two changes at once reach Stripe in the order of their messages, which may not be the order of the changes; an event that arrives twice can send its email to the address twice after the email service's key of a day expired.

Tests: `OpenCheckoutSessionTests`, `SetBillingEmailTests`, `ReceiveWebhookTests`, `TenantPurgeTests`, `CloseTenantTests`, `ReopenTenantTests`, `HostRoleTests`, `GetBillingTests`, `OpenPortalSessionTests`, `CheckoutOrdersTests`, `StripeBillingProviderTests`, `SubscriptionTests`, `RecordTenantPlanChangedTests`, `PlanMovesTests`, `SubscriptionEndByMemberTests`, `ChangePlanTests`, `PreviewPlanChangeTests`, `UndoScheduledChangeTests`, `GetPlanChoicesTests`, `ScheduleChangeTests`, `AcceptInvitationTests`, `RemoveMemberTests`, `LeaveTenantTests`, `ChangeMemberRoleTests`, `MakeOwnerTests`, `DeleteUserTests`, `DunningTests`, `EndGraceTests`, `SetBilledByTests`, `GetTenantBillingTests`, `UpdatePlanTests`, `GetPriceChangeTests`, `MoveToNewPriceTests`.

Source: Stripe, "Checkout", "Customer portal", "Webhooks", "Subscription statuses", "Change prices", "Prorations", "Pending updates", "Subscription schedules", "Per-seat pricing", "Automate payment retries" and "Invoice lifecycle". Richardson, *Microservices Patterns*, Chapter 3.
