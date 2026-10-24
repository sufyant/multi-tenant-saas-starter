"use client";

import {
  closeTenant,
  ErrorCode,
  getGetTenantQueryOptions,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { problemOf } from "@/shared/problem";
import { useDates } from "@/shared/use-dates";
import { useFailureText } from "@/shared/use-failure-text";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
} from "./organisation";

/**
 * Close an organisation (the CloseOrg and PhoneClose boards): red, and the slug typed to confirm. The text names no period, because
 * the reopen period is configuration (`ControlPlane:Tenants:ReopenPeriod`). After the closure the toast and the page show the API's
 * date: CloseTenant answers no `reopenableUntil`, so the dialog reads GetTenant again first.
 */
export function CloseDialog({
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
  const { name, slug, memberCount: count } = organisation;

  const close = useMutation({
    mutationFn: () => closeTenant(organisation.id),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => afterRefusal(queryClient, organisation.id, error),
  });
  // The toast names the last day to reopen, from a fresh GetTenant; without it, the toast says only that it closed.
  const confirmClosed = () =>
    void queryClient
      .fetchQuery(
        getGetTenantQueryOptions(organisation.id, { query: { staleTime: 0 } }),
      )
      .then(
        (closed) => closed.reopenableUntil,
        () => null,
      )
      .then((until) =>
        showToast({
          title: t("closedDone", { name }),
          description: until
            ? t("closedText", { date: date(until) })
            : undefined,
        }),
      );

  const failure = (() => {
    if (!close.error) return null;
    if (problemOf(close.error)?.code === ErrorCode.TenantNotActive)
      return {
        title: t("closeNotActive", { name }),
        text: t("closeNotActiveText"),
      };
    return { title: t("notDone", { name }), text: failureText(close.error) };
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("closeTitle", { name })}
      description={t("closeText", { count })}
      actionLabel={t("close")}
      cancelLabel={common("cancel")}
      confirmText={slug}
      confirmLabel={t("closeConfirm", { slug })}
      pending={close.isPending}
      onConfirm={() =>
        close.mutate(undefined, {
          onSuccess: () => {
            onClose();
            confirmClosed();
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
