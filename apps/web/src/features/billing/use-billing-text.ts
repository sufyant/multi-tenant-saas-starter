"use client";

import {
  getGetMeQueryOptions,
  getListMembersQueryOptions,
  LimitKey,
  type PaymentCardResponse,
  type SubscriptionResponseInterval,
} from "@repo/api-client";
import { formatDate, formatMonth } from "@repo/ui/lib/format-date-time";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { usePlan } from "@/shared/plan";
import { takesNoSeat } from "@/shared/roles";
import { useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import {
  type Fees,
  priceShapeOf,
  type SeatFacts,
  seatsOf,
  totalOf,
} from "./billing-facts";

/** The card brands the messages name; another brand is "the card" (the API's U1 writes the same in its emails). */
const brands = new Set([
  "visa",
  "mastercard",
  "amex",
  "discover",
  "diners",
  "jcb",
  "unionpay",
  "cartes_bancaires",
]);

/** The Billing screens' values as text: a day in the viewer's zone, an amount in the deployment's currency, the card, the price. */
export function useBillingText(currency: string) {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  const locale = t("locale.format");
  const money = (minor: number) => formatMoney(minor, currency, locale);
  return {
    date: (at: string) =>
      formatDate(at, { locale, timeZone: me.data?.timeZone }),
    month: (at: string) =>
      formatMonth(at, { locale, timeZone: me.data?.timeZone }),
    money,
    card: (card: PaymentCardResponse | null) =>
      card
        ? t("billing.card", {
            brand: brands.has(card.brand)
              ? t(`billing.brands.${card.brand}` as "billing.brands.visa")
              : card.brand,
            last4: card.last4,
          })
        : t("billing.theCard"),
    /** "€10 per person × 10 people = €100 a month, excl. VAT", or its base fee's form. */
    price: (
      fees: Fees,
      people: number,
      interval: SubscriptionResponseInterval,
    ) =>
      totalOf(fees, people) === 0
        ? money(0)
        : t(`billing.price.${priceShapeOf(fees)}`, {
            base: money(fees.base),
            perPerson: money(fees.perPerson),
            people,
            total: money(totalOf(fees, people)),
            interval,
          }),
  };
}

/**
 * The members and the seats for the Billing page: ListMembers' total, and the total of its Billing managers, whose role id comes from
 * ListRoles, or from the viewer's own role when the viewer is one (a Billing manager cannot read the roles). Null while unknown.
 */
export function useSeatFacts():
  | (SeatFacts & {
      people: number;
      invitations: number;
      billingNames: string[];
    })
  | null {
  const { tenant, detail } = useTenant();
  const plan = usePlan();
  const roles = useTenantRoles();
  const own = takesNoSeat(detail.role) ? detail.role.id : undefined;
  const roleId = roles.data?.items.find(takesNoSeat)?.id ?? own;
  const members = useQuery(
    getListMembersQueryOptions(tenant.id, { pageSize: 1 }),
  );
  const managers = useQuery({
    ...getListMembersQueryOptions(tenant.id, { roleId, pageSize: 5 }),
    enabled: roleId !== undefined,
  });
  if (!members.data || !managers.data) return null;
  const facts = {
    members: members.data.total,
    billing: managers.data.total,
    used: plan.limit(LimitKey.MembersMax).count,
  };
  return {
    ...facts,
    ...seatsOf(facts),
    billingNames: managers.data.items.flatMap((item) =>
      item.name ? [item.name] : [],
    ),
  };
}
