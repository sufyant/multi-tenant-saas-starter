import {
  type BillingResponse,
  BillingResponseBilledBy,
  PaymentResponseState,
  type PlanChoiceResponse,
  type PlanPricesResponse,
  type PricesResponse,
  type SubscriptionResponse,
  SubscriptionResponseInterval,
  SubscriptionResponseStatus,
} from "@repo/api-client";

/** A plan's two fees for one interval, in the minor unit of its currency (the API's PlanPricesResponse). */
export type Fees = { base: number; perPerson: number };

/** The fees of an interval: the monthly or the yearly pair. */
export function feesOf(
  prices: PlanPricesResponse | PricesResponse,
  interval: SubscriptionResponseInterval,
): Fees {
  return interval === SubscriptionResponseInterval.Yearly
    ? { base: prices.baseYearly, perPerson: prices.perPersonYearly }
    : { base: prices.baseMonthly, perPerson: prices.perPersonMonthly };
}

/** What the fees cost for the people who take a seat: the base fee once and the fee per person for each (B2, A2). */
export const totalOf = (fees: Fees, people: number): number =>
  fees.base + fees.perPerson * people;

/** Which sentence writes the price: the fee per person alone, the base fee and the fee per person, or the base fee alone. */
export type PriceShape = "perPerson" | "baseAndPerPerson" | "base";

export function priceShapeOf(fees: Fees): PriceShape {
  if (fees.perPerson === 0) return "base";
  return fees.base === 0 ? "perPerson" : "baseAndPerPerson";
}

/**
 * The members of the organisation and the seats: ListMembers' total, the Billing managers among them, who take no seat (A2), and the
 * count of `members.max`, which holds the members and the pending invitations that take a seat.
 */
export type SeatFacts = { members: number; billing: number; used: number };

/** The members who take a seat, whom the fee per person counts, and the pending invitations that take a seat. */
export function seatsOf(facts: SeatFacts): {
  people: number;
  invitations: number;
} {
  const people = Math.max(0, facts.members - facts.billing);
  return { people, invitations: Math.max(0, facts.used - people) };
}

/** A subscription that still pays: not ended at the provider (Stripe, "Subscription statuses"). */
export function isLive(subscription: SubscriptionResponse | null): boolean {
  if (!subscription) return false;
  return (
    subscription.status !== SubscriptionResponseStatus.Canceled &&
    subscription.status !== SubscriptionResponseStatus.IncompleteExpired
  );
}

/** The state of the Billing page, in the order the frames draw them (10.01, 10.07, 10.15 to 10.17, 10.19). */
export type BillingState =
  | { kind: "byContract" }
  | { kind: "notPaying" }
  | { kind: "moved"; subscription: SubscriptionResponse }
  | { kind: "paying"; subscription: SubscriptionResponse };

export function stateOf(billing: BillingResponse): BillingState {
  if (billing.billedBy === BillingResponseBilledBy.Contract)
    return { kind: "byContract" };
  const subscription = billing.subscription;
  if (!subscription || !isLive(subscription)) return { kind: "notPaying" };
  if (subscription.payment?.state === PaymentResponseState.MovedToDefault)
    return { kind: "moved", subscription };
  return { kind: "paying", subscription };
}

/** The plan of an id among the choices (GetPlanChoices), or the default plan. */
export const planOf = (
  plans: PlanChoiceResponse[],
  id: string | undefined,
): PlanChoiceResponse | undefined => plans.find((plan) => plan.id === id);

/** The plan the organisation is on, by the name GetMyTenant answers; the choices answer no id of it. */
export const planNamed = (
  plans: PlanChoiceResponse[],
  name: string,
): PlanChoiceResponse | undefined => plans.find((plan) => plan.name === name);

export const defaultPlanOf = (
  plans: PlanChoiceResponse[],
): PlanChoiceResponse | undefined => plans.find((plan) => plan.isDefault);
