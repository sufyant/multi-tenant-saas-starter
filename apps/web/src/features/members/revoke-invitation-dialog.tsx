"use client";

import {
  ErrorCode,
  type InvitationResponse,
  revokeInvitation,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useTenant } from "@/shared/tenant-context";
import { problemOf } from "./problem";
import { afterRefusal, refreshInvitations } from "./refresh";
import { useFailureText } from "./use-failure-text";

/**
 * Revoke a pending invitation, or cancel an expired one (the RevokeInvitation board, decision 3 of step 21c). Both call
 * RevokeInvitation. Revoking stops a link that works; cancelling removes a record whose link already stopped, so the texts differ.
 * An invitation accepted or gone meanwhile closes the dialog, refreshes the list and says so.
 */
export function RevokeInvitationDialog({
  invitation,
  expired,
  onClose,
}: {
  invitation: InvitationResponse;
  expired: boolean;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const { email } = invitation;

  const revoke = useMutation({
    mutationFn: () => revokeInvitation(tenant.id, invitation.id),
    onSuccess: () => refreshInvitations(queryClient, tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      const code = problemOf(error)?.code;
      if (
        code !== ErrorCode.InvitationNotPending &&
        code !== ErrorCode.InvitationNotFound
      )
        return;
      void refreshInvitations(queryClient, tenant.id);
      onClose();
      showToast(
        code === ErrorCode.InvitationNotPending
          ? {
              tone: "info",
              title: t("members.alreadyJoined", { email }),
              description: t("members.alreadyJoinedText"),
            }
          : {
              tone: "info",
              title: t("members.invitationGone"),
              description: t("members.invitationGoneText"),
            },
      );
    },
  });

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={
        expired
          ? t("members.cancelTitle", { email })
          : t("members.revokeTitle", { email })
      }
      description={expired ? t("members.cancelText") : t("members.revokeText")}
      actionLabel={
        expired ? t("members.cancelInvitation") : t("members.revoke")
      }
      cancelLabel={t("common.cancel")}
      pending={revoke.isPending}
      onConfirm={() =>
        revoke.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: expired ? t("members.cancelled") : t("members.revoked"),
            });
          },
        })
      }
    >
      {revoke.error && (
        <Banner tone="destructive" title={t("members.revokeFailed")}>
          {failureText(revoke.error)}
        </Banner>
      )}
    </Dialog>
  );
}
