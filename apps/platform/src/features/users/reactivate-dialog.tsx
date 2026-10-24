"use client";

import { ErrorCode, problemOf, reactivateUser } from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { afterRefusal, displayNameOf, refreshUser, type User } from "./user";

/** Reactivate a suspended user (the ReactivateUser board). A refusal shows its text in the dialog (F47). */
export function ReactivateDialog({
  user,
  onClose,
}: {
  user: User;
  onClose: () => void;
}) {
  const t = useTranslations("users");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const name = displayNameOf(user, t("deletedUser"));

  const reactivate = useMutation({
    mutationFn: () => reactivateUser(user.id),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => afterRefusal(queryClient, user.id, error),
  });

  const failure = (() => {
    if (!reactivate.error) return null;
    if (problemOf(reactivate.error)?.code === ErrorCode.UserNotFound)
      return { title: t("notFound"), text: null };
    return {
      title: t("notDone", { name }),
      text: failureText(problemOf(reactivate.error)),
    };
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("reactivateTitle", { name })}
      description={t("reactivateText")}
      actionLabel={t("reactivateAction")}
      cancelLabel={common("cancel")}
      pending={reactivate.isPending}
      onConfirm={() =>
        reactivate.isPending ||
        reactivate.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({ title: t("reactivated", { name }) });
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
