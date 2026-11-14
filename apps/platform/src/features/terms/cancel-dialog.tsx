"use client";

import {
  cancelTermsVersion,
  ErrorCode,
  problemOf,
  type TermsVersionResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { afterAccountRefusal } from "@/shared/refusal";
import { refreshTerms, useDay } from "@/shared/terms";

/**
 * Cancel scheduled version (11.11): red; the version will not take effect, the version in force stays, and the acceptances stay in
 * the cancelled version's record. A cancelled version answers as it is (the API records nothing), so a second click is harmless. A
 * refusal shows its text in the dialog, and the screens read again (F47). After a cancel the list opens with the toast (11.13).
 */
export function CancelDialog({
  version,
  inForce,
  onClose,
  onCancelled,
}: {
  version: TermsVersionResponse;
  /** The day of the version in force, or null before the first version takes effect. */
  inForce: string | null;
  onClose: () => void;
  onCancelled: () => void;
}) {
  const t = useTranslations();
  const day = useDay();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const date = day(version.effectiveDate);
  const current = inForce ? day(inForce) : null;

  const cancel = useMutation({
    mutationFn: () => cancelTermsVersion(version.id),
    onSuccess: () => {
      void refreshTerms(queryClient, version.id);
      onClose();
      onCancelled();
      showToast({
        title: t("terms.cancelled", { date }),
        description: current
          ? t("terms.cancelledText", { current })
          : undefined,
      });
    },
    onError: (error) => {
      afterAccountRefusal(queryClient, error);
      void refreshTerms(queryClient, version.id);
    },
  });

  const failure = (() => {
    const problem = problemOf(cancel.error);
    if (!cancel.error) return null;
    const code = problem?.code;
    // A code that says the version changed meanwhile has its own text; the screen has read again.
    return code === ErrorCode.TermsVersionNotScheduled ||
      code === ErrorCode.TermsVersionNotFound
      ? t(`errors.${code}`)
      : failureText(problem);
  })();

  return (
    <Dialog
      open
      tone="destructive"
      onOpenChange={(open) => !open && onClose()}
      title={t("terms.cancelTitle", { date })}
      description={t("terms.cancelText", {
        current: current ?? "none",
        count: version.acceptedCount,
      })}
      actionLabel={t("terms.cancel")}
      cancelLabel={t("terms.keep")}
      pending={cancel.isPending}
      onConfirm={() => cancel.isPending || cancel.mutate()}
    >
      {failure && (
        <Banner tone="destructive" title={t("terms.notCancelled")}>
          {failure}
        </Banner>
      )}
    </Dialog>
  );
}
