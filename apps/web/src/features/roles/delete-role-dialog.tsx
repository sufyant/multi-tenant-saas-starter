"use client";

import {
  deleteRole,
  ErrorCode,
  getListRolesQueryKey,
  problemOf,
  type RoleDetailResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { afterRefusal, refreshRoles, refreshTenant } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";

/**
 * Delete a custom role (the RoleDelete board). The API decides: a refusal shows its text in the dialog, and a role that changed
 * meanwhile reads the roles again (F47). `role.not_permitted` closes the dialog with a toast, as the Messages board says.
 */
export function DeleteRoleDialog({
  role,
  onClose,
  onDeleted,
}: {
  role: RoleDetailResponse;
  onClose: () => void;
  onDeleted?: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const name = role.name;

  const remove = useMutation({
    mutationFn: () => deleteRole(tenant.id, role.id),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: getListRolesQueryKey(tenant.id),
      }),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      const code = problemOf(error)?.code;
      if (code === ErrorCode.RoleInUse || code === ErrorCode.RoleBuiltIn)
        void refreshRoles(queryClient, tenant.id);
      if (code === ErrorCode.RoleNotPermitted) {
        void refreshTenant(queryClient, tenant.id);
        onClose();
        showToast({ title: t("roles.notPermitted"), tone: "destructive" });
      }
    },
  });

  const failure = (() => {
    if (!remove.error) return null;
    switch (problemOf(remove.error)?.code) {
      case ErrorCode.RoleInUse:
        return {
          title: t("roles.inUse", { name }),
          text: t("roles.inUseText"),
        };
      case ErrorCode.RoleNotFound:
        return { title: t("roles.gone"), text: t("roles.goneText") };
      case ErrorCode.RoleEscalation:
        return {
          title: t("roles.outOfReach"),
          text: t("roles.escalationAsk"),
        };
      case ErrorCode.RoleBuiltIn:
        return { title: t("roles.builtInTitle"), text: t("roles.builtInText") };
      case ErrorCode.RoleNotPermitted:
        return null;
      default:
        return {
          title: t("roles.deleteFailed", { name }),
          text: failureText(problemOf(remove.error)),
        };
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("roles.deleteTitle", { name })}
      description={t("roles.deleteText")}
      actionLabel={t("roles.delete")}
      cancelLabel={t("common.cancel")}
      pending={remove.isPending}
      onConfirm={() =>
        remove.mutate(undefined, {
          onSuccess: () => {
            onClose();
            onDeleted?.();
            showToast({
              title: t("roles.deleted"),
              description: t("roles.deletedText"),
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
