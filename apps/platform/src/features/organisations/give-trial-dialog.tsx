"use client";

import {
  getGetSignUpQueryOptions,
  getGetTenantPlanQueryKey,
  getListPlansQueryOptions,
  giveTenantTrial,
  problemOf,
  type SubscriptionResponseInterval,
  savedUnderKey,
  type TenantDetailResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { daysAfter } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { isolated } from "@/shared/isolated";
import { plansQuery } from "@/shared/plans";
import { useDates } from "@/shared/use-dates";
import { afterRefusal, refreshOrganisation } from "./organisation";
import { trialDays, useTrialDates } from "./trial";

/**
 * Give trial (4.29, 4.47): a plan and a number of days, the trial of a new organisation first (GetSignUp's `trial`). It starts now and
 * needs no card; at its end the organisation moves back to the plan it is on, which the hint names with the period it pays for.
 */
export function GiveTrialDialog({
  organisation,
  plan,
  interval,
  onClose,
}: {
  organisation: TenantDetailResponse;
  plan: TenantPlanResponse;
  interval: SubscriptionResponseInterval | null;
  onClose: () => void;
}) {
  const t = useTranslations("billingCard");
  const all = useTranslations();
  const common = useTranslations("common");
  const { date } = useDates();
  const range = useTrialDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const reasonId = useId();
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const offer = useQuery(getGetSignUpQueryOptions());
  const { key, renew } = useIntentKey();
  const [planId, setPlanId] = useState<string | null>(null);
  const [days, setDays] = useState<string | null>(null);
  // The trial of a new organisation, once GetSignUp answers; a choice of the system admin stays.
  const chosenPlan = planId ?? offer.data?.trial?.plan.id ?? "";
  const typed = days ?? String(offer.data?.trial?.days ?? "");
  const count = trialDays(typed);
  const give = useMutation({
    mutationFn: () =>
      giveTenantTrial(
        organisation.id,
        { planId: chosenPlan, days: count ?? 0 },
        { "Idempotency-Key": key },
      ),
    onSuccess: (answer) => {
      queryClient.setQueryData(
        getGetTenantPlanQueryKey(organisation.id),
        answer,
      );
      void refreshOrganisation(queryClient, organisation.id);
      showToast({ title: t("given") });
      onClose();
    },
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
      // A refusal saved under the key would answer again: the next try is a new intent (F48).
      if (savedUnderKey(error)) renew();
    },
  });
  const last = plan.trial;
  const hint =
    count === null
      ? undefined
      : t(interval ? "giveEndsPaid" : "giveEnds", {
          date: date(daysAfter(new Date(), count)),
          plan: plan.plan.name,
          interval: interval ?? "",
        });

  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("giveTitle", {
        organisation: organisation.name,
        ...isolated,
      })}
      description={
        last
          ? t.rich("giveTextUsed", {
              organisation: organisation.name,
              range: range(last.startedAt, last.endsAt),
              ...isolated,
            })
          : t("giveText")
      }
      cancelLabel={common("cancel")}
      actionLabel={t("giveTrial")}
      actionDisabled={count === null || chosenPlan === ""}
      actionReasonId={reasonId}
      pending={give.isPending}
      onConfirm={() => give.isPending || give.mutate()}
    >
      <div className="flex flex-col gap-3.5">
        <Combobox
          label={t("trialPlan")}
          value={chosenPlan}
          options={(plans.data?.items ?? []).map((item) => ({
            id: item.id,
            label: item.name,
          }))}
          // Another plan or another number of days is another intent: it gets a new key (F48).
          onValueChange={(id) => {
            setPlanId(id);
            renew();
          }}
          searchPlaceholder={all("planSection.planSearch")}
          emptyLabel={all("planSection.planEmpty")}
        />
        <Input
          label={t("days")}
          inputMode="numeric"
          autoComplete="off"
          value={typed}
          onChange={(event) => {
            setDays(event.target.value);
            renew();
          }}
          hint={hint}
          error={
            count === null && typed !== "" ? (
              <span id={reasonId}>{all("errors.trial.days_invalid")}</span>
            ) : undefined
          }
        />
        {give.error && (
          <Banner tone="destructive" title={t("giveFailed")}>
            {failureText(problemOf(give.error))}
          </Banner>
        )}
      </div>
    </Dialog>
  );
}
