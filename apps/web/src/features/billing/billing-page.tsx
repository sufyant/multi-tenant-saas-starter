"use client";

import {
  FeatureKey,
  getGetPlanChoicesQueryOptions,
  LimitKey,
  Permission,
  type PlanChoiceResponse,
  problemOf,
  type SubscriptionResponse,
  SubscriptionResponseInterval,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button } from "@repo/ui/components/button";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { notFound } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { isBillingOff, useBilling } from "@/shared/billing";
import { PermissionDenied } from "@/shared/permission-denied";
import { usePlan } from "@/shared/plan";
import { leaveOf } from "@/shared/problem-screen";
import { useCan, useTenant } from "@/shared/tenant-context";
import { BillingBanners } from "./billing-banners";
import { BillingEmailCard } from "./billing-email-card";
import {
  defaultPlanOf,
  feesOf,
  planNamed,
  planOf,
  stateOf,
  totalOf,
} from "./billing-facts";
import { CancelPlanDialog } from "./cancel-plan-dialog";
import { CheckoutReturn } from "./checkout-return";
import { useBillingText, useSeatFacts } from "./use-billing-text";
import { usePortal } from "./use-portal";

/**
 * Settings > Billing (10.01, 10.07, 10.15 to 10.24, 14.11, 14.14): the plan the organisation pays for and what it costs, its seats and
 * limits, the payment details in Stripe's portal and the billing email, for a viewer who manages billing. A Contract organisation
 * sees its plan and limits only (10.19). While the deployment's billing is off the page does not exist (B1).
 */
export function BillingPage() {
  const t = useTranslations();
  const can = useCan();
  const { tenant, detail } = useTenant();
  const manages = can(Permission.TenantBillingManage);
  const billing = useBilling();
  const choices = useQuery({
    ...getGetPlanChoicesQueryOptions(tenant.id),
    enabled: manages,
  });
  const contract = billing.data && stateOf(billing.data).kind === "byContract";

  const header = (
    <div className="flex flex-col gap-0.5">
      <h1 className="text-page-title">{t("billing.title")}</h1>
      <p className="text-body text-muted-foreground">
        {t(contract ? "billing.contractDescription" : "billing.description", {
          organisation: detail.name,
        })}
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
  if (!billing.data || !choices.data)
    return (
      <Frame>
        {header}
        <Skeleton rows={5} label={t("billing.loading")} />
      </Frame>
    );

  const plans = choices.data.plans;
  const state = stateOf(billing.data);
  const current = planNamed(plans, detail.planName);
  const fallback = defaultPlanOf(plans);
  const paidName =
    state.kind === "paying" || state.kind === "moved"
      ? (planOf(plans, state.subscription.planId)?.name ?? null)
      : null;
  return (
    <Frame>
      {header}
      <CheckoutReturn plan={paidName} />
      {state.kind !== "byContract" && state.kind !== "notPaying" && (
        <BillingBanners
          subscription={state.subscription}
          plans={plans}
          paid={planOf(plans, state.subscription.planId)}
        />
      )}
      <PlanCard
        state={state}
        plans={plans}
        current={current}
        fallback={fallback}
      />
      {state.kind !== "byContract" && (
        <>
          <PaymentDetailsCard paid={state.kind !== "notPaying"} />
          <BillingEmailCard address={billing.data.billingEmail} />
        </>
      )}
    </Frame>
  );
}

/** The plan's card: its name and price with the plan's actions, then a row for each limit (10.01, 10.07, 10.17, 10.19). */
function PlanCard({
  state,
  plans,
  current,
  fallback,
}: {
  state: ReturnType<typeof stateOf>;
  plans: PlanChoiceResponse[];
  current: PlanChoiceResponse | undefined;
  fallback: PlanChoiceResponse | undefined;
}) {
  const t = useTranslations();
  const { tenant, detail } = useTenant();
  const seats = useSeatFacts();
  const portal = usePortal();
  const [cancelling, setCancelling] = useState(false);
  const currency = (current ?? fallback ?? plans[0])?.currency ?? "EUR";
  const text = useBillingText(currency);
  const changePlan = (variant: "primary" | "secondary") => (
    <Button
      variant={variant}
      render={<Link href={`/${tenant.slug}/settings/billing/plans`} />}
      nativeButton={false}
    >
      {t("billing.changePlan")}
    </Button>
  );

  const head = (() => {
    if (state.kind === "byContract")
      return {
        name: detail.planName,
        label: t("billing.contractLabel"),
        lines: [t("billing.contractTitle")],
        hints: [t("billing.contractText")],
        actions: (
          <Button render={<a href={brand.supportUrl} />} nativeButton={false}>
            {t("plans.requestChange")}
          </Button>
        ),
      };
    if (state.kind === "moved") {
      const paid = planOf(plans, state.subscription.planId);
      const payment = state.subscription.payment;
      const price = current
        ? text.price(
            feesOf(current.prices, state.subscription.interval),
            seats?.people ?? 0,
            state.subscription.interval,
          )
        : "";
      return {
        name: detail.planName,
        lines: [
          t("billing.movedPlan", {
            price,
            plan: paid?.name ?? "",
            date: text.date(payment?.movedAt ?? payment?.failedAt ?? ""),
          }),
        ],
        hints: [t("billing.movedPay", { plan: paid?.name ?? "" })],
        actions: (
          <Button
            variant="primary"
            disabled={portal.pending}
            onClick={portal.open}
          >
            {t("billing.payAndGetBack", {
              amount: text.money(payment?.amountDue ?? 0),
              plan: paid?.name ?? "",
            })}
          </Button>
        ),
      };
    }
    if (state.kind === "notPaying") {
      const plan = current ?? fallback;
      return {
        name: detail.planName,
        lines:
          plan && seats
            ? [
                text.price(
                  feesOf(plan.prices, SubscriptionResponseInterval.Monthly),
                  seats.people,
                  SubscriptionResponseInterval.Monthly,
                ),
              ]
            : [],
        hints: [],
        actions: changePlan("primary"),
      };
    }
    return paying(state.subscription);
  })();

  function paying(subscription: SubscriptionResponse) {
    const paid = planOf(plans, subscription.planId);
    const people = seats?.people ?? 0;
    const fees = paid ? feesOf(paid.prices, subscription.interval) : undefined;
    const next = (() => {
      if (subscription.cancelAtPeriodEnd)
        return t("billing.ends", {
          plan: paid?.name ?? "",
          date: text.date(subscription.currentPeriodEnd),
        });
      if (subscription.payment)
        return t("billing.due", {
          date: text.date(subscription.payment.failedAt),
          amount: text.money(subscription.payment.amountDue),
        });
      const scheduled = subscription.scheduledChange;
      const nextPlan = scheduled ? planOf(plans, scheduled.planId) : paid;
      const nextFees = nextPlan
        ? feesOf(nextPlan.prices, scheduled?.interval ?? subscription.interval)
        : undefined;
      return nextFees && seats
        ? t("billing.next", {
            date: text.date(subscription.currentPeriodEnd),
            amount: text.money(totalOf(nextFees, people)),
          })
        : null;
    })();
    const priceChange = subscription.priceChange;
    const newFees = priceChange
      ? feesOf(priceChange.newPrices, subscription.interval)
      : undefined;
    const free =
      seats && seats.billing > 0
        ? t("billing.free", {
            count: seats.billing,
            names: new Intl.ListFormat(t("locale.format"), {
              type: "conjunction",
            }).format(seats.billingNames),
          })
        : null;
    const ends = subscription.cancelAtPeriodEnd || subscription.scheduledChange;
    return {
      name: paid?.name ?? detail.planName,
      label: t(`billing.interval.${subscription.interval}`),
      lines:
        fees && seats ? [text.price(fees, people, subscription.interval)] : [],
      hints: [
        free,
        next,
        newFees && priceChange && seats
          ? t("billing.fromDate", {
              date: text.date(priceChange.movesAt),
              line: text.price(newFees, people, subscription.interval),
            })
          : null,
      ].filter((line): line is string => line !== null),
      actions: (
        <>
          {!ends && !subscription.payment && (
            <Button variant="ghost" onClick={() => setCancelling(true)}>
              {t("billing.cancelPlan")}
            </Button>
          )}
          {!subscription.cancelAtPeriodEnd && changePlan("primary")}
        </>
      ),
    };
  }

  return (
    <section className="overflow-clip rounded-lg border border-border bg-card">
      <div className="flex flex-col gap-3 border-b border-border p-5 tablet:flex-row tablet:items-center">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h2 className="flex flex-wrap items-center gap-2 text-section-title">
            <bdi>{head.name}</bdi>
            {"label" in head && head.label && <Label>{head.label}</Label>}
          </h2>
          {head.lines.map((line) => (
            <p key={line} className="text-body">
              {line}
            </p>
          ))}
          {head.hints.map((line) => (
            <p key={line} className="text-small text-muted-foreground">
              {line}
            </p>
          ))}
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          {head.actions}
        </div>
      </div>
      <LimitRows planName={detail.planName} />
      {cancelling && state.kind === "paying" && (
        <CancelPlanDialog
          subscription={state.subscription}
          plans={plans}
          onClose={() => setCancelling(false)}
        />
      )}
    </section>
  );
}

/** The plan's limits: the seats and the custom roles, each with its count (10.01, 10.17). The meters come with step 40c. */
function LimitRows({ planName }: { planName: string }) {
  const t = useTranslations();
  const plan = usePlan();
  const seats = useSeatFacts();
  const members = plan.limit(LimitKey.MembersMax);
  const roles = plan.limit(LimitKey.RolesCustomMax);
  const ofLimit = (count: number, limit: number | null) =>
    limit === null ? String(count) : t("billing.ofLimit", { count, limit });
  const over = (state: { overLimit: boolean }) =>
    state.overLimit ? "text-destructive-text" : "text-muted-foreground";
  return (
    <dl className="text-body">
      <Row
        term={t("billing.seats")}
        value={ofLimit(members.count, members.limit)}
        hint={
          seats
            ? t("billing.seatsText", {
                members: seats.members,
                billing: seats.billing,
                invitations: seats.invitations,
                used: members.count,
                limit: members.limit === null ? "none" : members.limit,
              })
            : undefined
        }
        hintClass={over(members)}
      />
      <Row
        term={t("billing.customRoles")}
        value={
          plan.on(FeatureKey.RolesCustom)
            ? ofLimit(roles.count, roles.limit)
            : String(roles.count)
        }
        hint={
          !plan.on(FeatureKey.RolesCustom)
            ? t("billing.rolesNone", { plan: planName, count: roles.count })
            : roles.limit === null
              ? t("billing.rolesUnlimited")
              : t("billing.rolesGives", { limit: roles.limit })
        }
        hintClass="text-muted-foreground"
      />
    </dl>
  );
}

/** Payment details (10.01): Stripe keeps the card, the address, the VAT ID and the invoices; its portal opens in a new tab. */
function PaymentDetailsCard({ paid }: { paid: boolean }) {
  const t = useTranslations();
  const portal = usePortal();
  return (
    <section className="flex flex-col gap-3 rounded-lg border border-border bg-card p-5 tablet:flex-row tablet:items-center">
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <h2 className="text-section-title">{t("billing.paymentDetails")}</h2>
        <p className="text-small text-muted-foreground">
          {paid ? t("billing.paymentText") : t("billing.noCard")}
        </p>
      </div>
      {paid && (
        <Button disabled={portal.pending} onClick={portal.open}>
          {t("billing.managePayment")}
        </Button>
      )}
    </section>
  );
}

/** One row of a card: the term in its 200 px column, the value, and a hint under it. On a phone the value goes under the term. */
function Row({
  term,
  value,
  hint,
  hintClass,
}: {
  term: string;
  value: ReactNode;
  hint?: string;
  hintClass: string;
}) {
  return (
    <div className="flex flex-col gap-1 border-b border-border px-5 py-3.5 last:border-b-0 tablet:flex-row tablet:items-start tablet:gap-4">
      <dt className="shrink-0 text-small text-muted-foreground tablet:w-50">
        {term}
      </dt>
      <dd className="flex min-w-0 flex-1 flex-col items-start gap-0.5">
        {value}
        {hint && <span className={`text-small ${hintClass}`}>{hint}</span>}
      </dd>
    </div>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
