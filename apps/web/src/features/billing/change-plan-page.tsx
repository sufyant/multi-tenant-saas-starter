"use client";

import {
  FeatureKey,
  getGetPlanChoicesQueryOptions,
  LimitKey,
  openCheckoutSession,
  Permission,
  type PlanChoiceResponse,
  problemOf,
  SubscriptionResponseInterval,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button } from "@repo/ui/components/button";
import { ArrowLeftIcon, CheckIcon, Icon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { Pill } from "@repo/ui/components/pill";
import { SegmentedControl } from "@repo/ui/components/segmented-control";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { notFound } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { isBillingOff, useBilling } from "@/shared/billing";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { useCan, useTenant } from "@/shared/tenant-context";
import {
  feesOf,
  planNamed,
  planOf,
  priceShapeOf,
  stateOf,
  totalOf,
} from "./billing-facts";
import { CancelPlanDialog } from "./cancel-plan-dialog";
import { UpgradeDialog } from "./upgrade-dialog";
import { useBillingText, useSeatFacts } from "./use-billing-text";
import { billingAddress } from "./use-portal";

const { Monthly, Yearly } = SubscriptionResponseInterval;

/** What a plan's button does: nothing for the plan it pays, the checkout, or a move at once or at the period's end (B9). */
type Move = "current" | "checkout" | "upgrade" | "lower";

/**
 * Change plan (10.02, 10.03, 10.10): every plan, the cheapest first, with its price for the interval and for the organisation's
 * people, its limits, and the move it takes. A plan that costs more for these people is an upgrade, at once with the rest of the
 * period charged today (10.04); a cheaper one moves at the end of the paid period (10.06), the default plan being the cancellation.
 * An organisation that does not pay by card yet pays on Stripe's page (10.10). The thread's choice: "lower" is decided by price.
 */
export function ChangePlanPage() {
  const t = useTranslations();
  const failureText = useFailureText();
  const can = useCan();
  const { tenant, detail } = useTenant();
  const manages = can(Permission.TenantBillingManage);
  const billing = useBilling();
  const choices = useQuery({
    ...getGetPlanChoicesQueryOptions(tenant.id),
    enabled: manages,
  });
  const seats = useSeatFacts();
  const state = billing.data ? stateOf(billing.data) : undefined;
  const subscription =
    state?.kind === "paying" ? state.subscription : undefined;
  const [interval, setInterval] = useState<SubscriptionResponseInterval>(
    subscription?.interval ?? Monthly,
  );
  const [chosen, setChosen] = useState<{
    plan: PlanChoiceResponse;
    move: "upgrade" | "lower";
  }>();
  const checkout = useMutation({
    mutationFn: (plan: PlanChoiceResponse) =>
      openCheckoutSession(tenant.id, {
        planId: plan.id,
        interval,
        returnUrl: billingAddress(tenant.slug),
      }),
    // Stripe's page opens in this tab, and comes back to Billing with the checkout's result (10.11, 10.12).
    onSuccess: ({ url }) => window.location.assign(url),
    onError: (error) =>
      showToast({
        tone: "destructive",
        title: t("billing.plans.checkoutFailed"),
        description: failureText(problemOf(error)),
      }),
  });
  const plans = choices.data?.plans ?? [];
  const text = useBillingText(plans[0]?.currency ?? "EUR");

  const back = (
    <Link
      href={`/${tenant.slug}/settings/billing`}
      className="inline-flex items-center gap-2 self-start text-body outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring"
    >
      <Icon icon={ArrowLeftIcon} size={16} directional />
      {t("billing.plans.back")}
    </Link>
  );
  const header = (
    <div className="flex flex-col gap-0.5">
      <h1 className="text-page-title">{t("billing.plans.title")}</h1>
      <p className="text-body text-muted-foreground">
        {t("billing.plans.text")}
      </p>
    </div>
  );

  if (!manages)
    return (
      <Frame>
        <PermissionDenied page={t("billing.title")} />
      </Frame>
    );
  if (billing.isError && isBillingOff(billing.error)) notFound();
  const failed = billing.error ?? choices.error;
  if (failed)
    return (
      <Frame>
        {back}
        {header}
        <ListError
          title={t("billing.loadError")}
          problem={problemOf(failed)}
          leave={leaveOf(failed)}
          at={Math.max(billing.errorUpdatedAt, choices.errorUpdatedAt)}
          onRetry={() => {
            void billing.refetch();
            void choices.refetch();
          }}
        />
      </Frame>
    );
  if (!state || !choices.data || !seats)
    return (
      <Frame>
        {back}
        {header}
        <Skeleton rows={3} label={t("billing.loading")} />
      </Frame>
    );
  if (state.kind === "byContract")
    return (
      <Frame>
        {back}
        {header}
        <p className="text-body">{t("billing.plans.contract")}</p>
        <Button
          className="self-start"
          render={<a href={brand.supportUrl} />}
          nativeButton={false}
        >
          {t("plans.requestChange")}
        </Button>
      </Frame>
    );

  const people = seats.people;
  const paid = subscription ? planOf(plans, subscription.planId) : undefined;
  const onNow = paid ?? planNamed(plans, detail.planName);
  const costOf = (plan: PlanChoiceResponse, at: SubscriptionResponseInterval) =>
    totalOf(feesOf(plan.prices, at), people);
  // A year's price against twelve months': the months a yearly payer saves, when every paid plan saves the same whole number.
  const savedMonths = (() => {
    const months = plans
      .filter((plan) => costOf(plan, Monthly) > 0)
      .map((plan) => 12 - costOf(plan, Yearly) / costOf(plan, Monthly));
    const first = months[0];
    return first !== undefined &&
      Number.isInteger(first) &&
      first > 0 &&
      months.every((value) => value === first)
      ? first
      : 0;
  })();
  const moveOf = (plan: PlanChoiceResponse): Move => {
    if (!subscription) return plan.id === onNow?.id ? "current" : "checkout";
    if (plan.id === subscription.planId && interval === subscription.interval)
      return "current";
    if (plan.isDefault) return "lower";
    // The same plan: monthly to yearly applies at once, yearly to monthly at the end of the year (B9).
    if (plan.id === subscription.planId)
      return interval === Yearly ? "upgrade" : "lower";
    return paid && costOf(plan, interval) > costOf(paid, interval)
      ? "upgrade"
      : "lower";
  };
  const labelOf = (plan: PlanChoiceResponse, move: Move) => {
    if (move === "current") return t("billing.plans.current");
    if (subscription && plan.id === subscription.planId)
      return interval === Yearly
        ? t("billing.plans.switchToYearly")
        : t("billing.plans.switchToMonthly");
    if (move === "lower") return t("billing.plans.moveTo", { plan: plan.name });
    if (move === "checkout" && costOf(plan, interval) === 0)
      return t("billing.plans.moveTo", { plan: plan.name });
    return t("billing.plans.upgradeTo", { plan: plan.name });
  };
  const shorter = plans.find((plan) => {
    const days = plan.entitlements[LimitKey.AuditRetentionDays];
    const today = detail.entitlements[LimitKey.AuditRetentionDays];
    return (
      typeof days === "number" &&
      (today === null || (typeof today === "number" && days < today))
    );
  });
  const yearlyFooter =
    subscription && subscription.interval === Monthly && interval === Yearly;

  return (
    <Frame>
      {back}
      {header}
      <div className="self-start">
        <SegmentedControl
          label={t("billing.plans.interval")}
          value={interval}
          onValueChange={setInterval}
          options={[
            { value: Monthly, label: t("billing.plans.monthly") },
            {
              value: Yearly,
              label:
                savedMonths > 0
                  ? t("billing.plans.yearlyFree", { months: savedMonths })
                  : t("billing.plans.yearly"),
            },
          ]}
        />
      </div>
      <ul className="grid gap-4 desktop:grid-cols-3">
        {plans.map((plan) => {
          const move = moveOf(plan);
          const current = move === "current";
          const fees = feesOf(plan.prices, interval);
          const total = totalOf(fees, people);
          const opening =
            checkout.isPending && checkout.variables?.id === plan.id;
          return (
            <li
              key={plan.id}
              className={`flex flex-col gap-4 rounded-lg border bg-card p-5 ${
                current ? "border-2 border-primary" : "border-border"
              }`}
            >
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-section-title">
                  <bdi>{plan.name}</bdi>
                </h2>
                {subscription && plan.id === subscription.planId && (
                  <Pill tone="info">
                    {subscription.interval === interval
                      ? t("billing.plans.current")
                      : t("billing.plans.currentInterval", {
                          interval: subscription.interval,
                        })}
                  </Pill>
                )}
                {!subscription && current && (
                  <Pill tone="info">{t("billing.plans.current")}</Pill>
                )}
              </div>
              <Price fees={fees} interval={interval} money={text.money} />
              <p
                className={`text-small ${
                  interval === Yearly && savedMonths > 0 && total > 0
                    ? "text-success-text"
                    : "text-muted-foreground"
                }`}
              >
                {total === 0
                  ? t("billing.plans.forPeopleFree", {
                      people,
                      amount: text.money(0),
                    })
                  : t("billing.plans.forPeople", {
                      people,
                      amount: text.money(total),
                      interval,
                      months: interval === Yearly ? savedMonths : 0,
                    })}
              </p>
              <ul className="flex flex-1 flex-col gap-2 text-small">
                {limitsOf(plan).map((line) => (
                  <li key={line.key} className="flex items-start gap-2">
                    <Icon
                      icon={CheckIcon}
                      size={16}
                      className="mt-0.5 shrink-0"
                    />
                    {t(line.key, line.values)}
                  </li>
                ))}
              </ul>
              <Button
                variant={
                  move === "upgrade" || (move === "checkout" && total > 0)
                    ? "primary"
                    : "secondary"
                }
                disabled={current || checkout.isPending}
                onClick={() => {
                  if (move === "checkout") checkout.mutate(plan);
                  else if (move === "upgrade" || move === "lower")
                    setChosen({ plan, move });
                }}
              >
                {opening ? t("billing.plans.opening") : labelOf(plan, move)}
              </Button>
            </li>
          );
        })}
      </ul>
      <p className="text-small text-muted-foreground">
        {checkout.isPending
          ? t("billing.plans.openingText")
          : [
              yearlyFooter && paid
                ? t("billing.plans.footerYearly", {
                    amount: text.money(costOf(paid, Yearly)),
                    people,
                  })
                : t("billing.plans.footer"),
              shorter
                ? t("billing.plans.shorterLog", {
                    plan: shorter.name,
                    days: shorter.entitlements[
                      LimitKey.AuditRetentionDays
                    ] as number,
                  })
                : null,
            ]
              .filter(Boolean)
              .join(" ")}
      </p>
      {chosen?.move === "upgrade" && subscription && (
        <UpgradeDialog
          subscription={subscription}
          plans={plans}
          target={chosen.plan}
          interval={interval}
          onClose={() => setChosen(undefined)}
        />
      )}
      {chosen?.move === "lower" && subscription && (
        <CancelPlanDialog
          subscription={subscription}
          plans={plans}
          target={chosen.plan}
          interval={interval}
          onClose={() => setChosen(undefined)}
        />
      )}
    </Frame>
  );
}

/** The limits a plan card lists: its seats and its custom roles (10.02). The meters come with step 40c. */
function limitsOf(plan: PlanChoiceResponse): {
  key:
    | "billing.plans.seats"
    | "billing.plans.seatsUnlimited"
    | "billing.plans.noCustomRoles"
    | "billing.plans.customRoles"
    | "billing.plans.customRolesUnlimited";
  values?: { count: number };
}[] {
  const seats = plan.entitlements[LimitKey.MembersMax];
  const rolesOn = plan.entitlements[FeatureKey.RolesCustom] === true;
  const roles = plan.entitlements[LimitKey.RolesCustomMax];
  return [
    typeof seats === "number"
      ? { key: "billing.plans.seats", values: { count: seats } }
      : { key: "billing.plans.seatsUnlimited" },
    !rolesOn || roles === 0
      ? { key: "billing.plans.noCustomRoles" }
      : typeof roles === "number"
        ? { key: "billing.plans.customRoles", values: { count: roles } }
        : { key: "billing.plans.customRolesUnlimited" },
  ];
}

/** The plan's price in large type, with its unit: "€10 per person a month", "€99 + €8 base + per person a month" (10.02). */
function Price({
  fees,
  interval,
  money,
}: {
  fees: ReturnType<typeof feesOf>;
  interval: SubscriptionResponseInterval;
  money: (minor: number) => string;
}) {
  const t = useTranslations();
  const shape = priceShapeOf(fees);
  const amount =
    shape === "baseAndPerPerson"
      ? `${money(fees.base)} + ${money(fees.perPerson)}`
      : money(shape === "base" ? fees.base : fees.perPerson);
  return (
    <p className="flex items-baseline gap-2">
      <span className="shrink-0 whitespace-nowrap text-page-title">
        {amount}
      </span>
      <span className="min-w-0 text-small text-muted-foreground">
        {t(`billing.plans.unit.${shape}`, { interval })}
      </span>
    </p>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
