"use client";

import { ErrorCode, reactivateTenant } from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
} from "./organisation";

/** Reactivate a suspended organisation (the ReactivateOrg board). A closed one is reopened instead (409 `tenant.not_active`). */
export function ReactivateDialog({
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
  const { name, memberCount: count } = organisation;

  const reactivate = useMutation({
    mutationFn: () => reactivateTenant(organisation.id),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => afterRefusal(queryClient, organisation.id, error),
  });

  const failure = (() => {
    if (!reactivate.error) return null;
    if (problemOf(reactivate.error)?.code === ErrorCode.TenantNotActive)
      return {
        title: t("reactivateClosed", { name }),
        text: t("reactivateClosedText"),
      };
    return {
      title: t("notDone", { name }),
      text: failureText(reactivate.error),
    };
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("reactivateTitle", { name })}
      description={t("reactivateText", { count })}
      actionLabel={t("reactivateAction")}
      cancelLabel={common("cancel")}
      pending={reactivate.isPending}
      onConfirm={() =>
        reactivate.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: t("reactivated", { name }),
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
