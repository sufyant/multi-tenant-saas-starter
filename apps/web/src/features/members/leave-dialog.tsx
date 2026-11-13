"use client";

import {
  ErrorCode,
  getGetMyTenantQueryKey,
  getListMyTenantsQueryKey,
  leaveTenant,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { afterRefusal, refreshMembers } from "@/shared/refresh";
import { SupportLink } from "@/shared/support-link";
import { useTenant } from "@/shared/tenant-context";

/**
 * Leave the organisation (8.23, `LeaveTenant`): the dialog names the organisation. After the answer the toast says what stays and
 * the start page opens, which shows "No access yet" to a person with no organisation left (8.24). The last Owner sees the menu
 * item disabled first (8.46); 409 `member.last_owner` stays the fallback, with the frame's text and "contact support" (8.25).
 */
export function LeaveDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useFreshRouter(useRouter());
  const { tenant } = useTenant();
  const organisation = tenant.name;

  const leave = useMutation({
    mutationFn: () => leaveTenant(tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      // Another Owner left after the list was read: the list and the Owners' count are read again, and Leave follows (F47, 8.46).
      if (problemOf(error)?.code === ErrorCode.MemberLastOwner)
        void refreshMembers(queryClient, tenant.id);
    },
  });

  const left = async () => {
    onClose();
    showToast({
      title: t("members.left", { organisation }),
      description: t("members.leftText"),
    });
    await router.replace("/");
    // The start page reads the person's organisations afresh: the cached list still holds this one, which would open it again.
    queryClient.removeQueries({ queryKey: getListMyTenantsQueryKey() });
    queryClient.removeQueries({
      queryKey: getGetMyTenantQueryKey(tenant.id),
    });
  };

  if (problemOf(leave.error)?.code === ErrorCode.MemberLastOwner)
    return (
      <Dialog
        open
        onOpenChange={(open) => !open && onClose()}
        title={t("members.leaveTitle", { organisation })}
        actionLabel={t("common.close")}
        onConfirm={onClose}
      >
        <Banner
          tone="destructive"
          title={t("members.lastOwner", { organisation })}
        >
          {t.rich("members.lastOwnerLeave", {
            support: (chunks) => <SupportLink>{chunks}</SupportLink>,
          })}
        </Banner>
      </Dialog>
    );

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("members.leaveTitle", { organisation })}
      description={t("members.leaveText")}
      actionLabel={t("members.leave")}
      cancelLabel={t("common.cancel")}
      pending={leave.isPending}
      onConfirm={() => leave.mutate(undefined, { onSuccess: left })}
    >
      {leave.error && (
        <Banner
          tone="destructive"
          title={t("members.leaveFailed", { organisation })}
        >
          {failureText(problemOf(leave.error))}
        </Banner>
      )}
    </Dialog>
  );
}
