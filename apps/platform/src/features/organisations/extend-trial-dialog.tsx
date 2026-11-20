"use client";

import {
  extendTenantTrial,
  getGetTenantPlanQueryKey,
  problemOf,
  type TenantDetailResponse,
  type TrialResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { daysAfter } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { isolated } from "@/shared/isolated";
import { useDates } from "@/shared/use-dates";
import { afterRefusal, refreshOrganisation } from "./organisation";
import { trialDays } from "./trial";

/**
 * Extend trial (4.30, 4.48): the extra days move a running trial's end, which the API takes as the new end; the dialog says the new end.
 * The extension sets a state, so it takes no idempotency key (I1).
 */
export function ExtendTrialDialog({
  organisation,
  trial,
  onClose,
}: {
  organisation: TenantDetailResponse;
  trial: TrialResponse;
  onClose: () => void;
}) {
  const t = useTranslations("billingCard");
  const all = useTranslations();
  const common = useTranslations("common");
  const { date } = useDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const reasonId = useId();
  const [typed, setTyped] = useState("");
  const count = trialDays(typed);
  const endsAt = count === null ? null : daysAfter(trial.endsAt, count);
  const extend = useMutation({
    mutationFn: (newEnd: string) =>
      extendTenantTrial(organisation.id, { endsAt: newEnd }),
    onSuccess: (answer) => {
      queryClient.setQueryData(
        getGetTenantPlanQueryKey(organisation.id),
        answer,
      );
      void refreshOrganisation(queryClient, organisation.id);
      showToast({ title: t("extended") });
      onClose();
    },
    onError: (error) => afterRefusal(queryClient, organisation.id, error),
  });

  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("extendTitle")}
      description={t.rich("extendText", {
        organisation: organisation.name,
        plan: trial.plan.name,
        date: date(trial.endsAt),
        ...isolated,
      })}
      cancelLabel={common("cancel")}
      actionLabel={t("extendTrial")}
      actionDisabled={endsAt === null}
      actionReasonId={reasonId}
      pending={extend.isPending}
      onConfirm={() =>
        endsAt === null || extend.isPending || extend.mutate(endsAt)
      }
    >
      <div className="flex flex-col gap-3.5">
        <Input
          label={t("extraDays")}
          inputMode="numeric"
          autoComplete="off"
          value={typed}
          onChange={(event) => setTyped(event.target.value)}
          hint={endsAt && t("newEnd", { date: date(endsAt) })}
          error={
            count === null && typed !== "" ? (
              <span id={reasonId}>{all("errors.trial.days_invalid")}</span>
            ) : undefined
          }
        />
        {extend.error && (
          <Banner tone="destructive" title={t("extendFailed")}>
            {failureText(problemOf(extend.error))}
          </Banner>
        )}
      </div>
    </Dialog>
  );
}
