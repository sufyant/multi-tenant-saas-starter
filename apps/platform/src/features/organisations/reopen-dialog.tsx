"use client";

import {
  ErrorCode,
  getGetTenantQueryOptions,
  problemOf,
  reopenTenant,
  TenantResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useDates } from "@/shared/use-dates";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
} from "./organisation";

/**
 * Reopen a closed organisation (the ReopenOrg board). `tenant.not_closed` covers an organisation someone reopened already and one
 * whose purge has begun, so the dialog reads GetTenant again and names the status it finds.
 */
export function ReopenDialog({
  organisation,
  onClose,
}: {
  organisation: Organisation;
  onClose: () => void;
}) {
  const t = useTranslations("organisations");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { date } = useDates();
  const { name, memberCount: count, reopenableUntil } = organisation;
  // The status GetTenant answers after `tenant.not_closed`: null while it loads, `unread` when the read failed too.
  const [found, setFound] = useState<TenantResponseStatus | "unread" | null>(
    null,
  );

  const reopen = useMutation({
    mutationFn: () => reopenTenant(organisation.id),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => {
      setFound(null);
      afterRefusal(queryClient, organisation.id, error);
      if (problemOf(error)?.code !== ErrorCode.TenantNotClosed) return;
      void queryClient
        .fetchQuery(
          getGetTenantQueryOptions(organisation.id, {
            query: { staleTime: 0 },
          }),
        )
        .then(
          (tenant) => setFound(tenant.status),
          () => setFound("unread"),
        );
    },
  });

  const failure = (() => {
    if (!reopen.error) return null;
    switch (problemOf(reopen.error)?.code) {
      case ErrorCode.TenantReopenPeriodEnded:
        return {
          title: reopenableUntil
            ? t("periodEnded", { date: date(reopenableUntil) })
            : t("beingDeleted", { name }),
          text: t("periodEndedText", { name }),
        };
      case ErrorCode.TenantNotClosed:
        if (found === null) return null;
        if (found === TenantResponseStatus.Active)
          return { title: t("notClosed", { name }), text: t("notClosedText") };
        if (found === "unread")
          return {
            title: t("notDone", { name }),
            text: failureText(problemOf(reopen.error)),
          };
        return { title: t("beingDeleted", { name }), text: undefined };
      default:
        return {
          title: t("notDone", { name }),
          text: failureText(problemOf(reopen.error)),
        };
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("reopenTitle", { name })}
      description={t("reopenText", { count })}
      actionLabel={t("reopenAction")}
      cancelLabel={common("cancel")}
      pending={reopen.isPending}
      onConfirm={() =>
        reopen.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: t("reopened", { name }),
              description: t("signInAgain", { count }),
            });
          },
        })
      }
    >
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
