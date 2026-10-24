"use client";

import {
  changeMemberRole,
  ErrorCode,
  type MemberResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { problemOf } from "@/shared/problem";
import { useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import { useFailureText } from "@/shared/use-failure-text";
import { roleSelect } from "@/shared/use-role-name";
import { nameOf } from "./person";
import { afterRefusal, refreshMembers, refreshTenant } from "./refresh";
import { RoleChoice } from "./role-choice";

/**
 * Change a member's role (the ChangeRole board): every role of the tenant (ListRoles); a role the viewer's role does not fully
 * hold is disabled, with its reason (F51). The API decides; each refusal shows its text in the dialog (F47).
 */
export function ChangeRoleDialog({
  member,
  self,
  onClose,
}: {
  member: MemberResponse;
  /** The member is the viewer: their own permissions change too. */
  self: boolean;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const roles = useTenantRoles();
  const [roleId, setRoleId] = useState(member.role.id);
  const name = nameOf(member) ?? t("members.deletedUser");
  const organisation = tenant.name;

  const change = useMutation({
    mutationFn: () => changeMemberRole(tenant.id, member.userId, { roleId }),
    onSuccess: () =>
      Promise.all([
        refreshMembers(queryClient, tenant.id),
        self && refreshTenant(queryClient, tenant.id),
      ]),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      if (problemOf(error)?.code === ErrorCode.MemberNotFound)
        void refreshMembers(queryClient, tenant.id);
    },
  });

  const failure = (() => {
    if (!change.error) return null;
    switch (problemOf(change.error)?.code) {
      case ErrorCode.MemberLastOwner:
        return {
          title: t("members.lastOwner", { organisation }),
          text: t("members.lastOwnerChange"),
        };
      case ErrorCode.MemberOwnerRequiresOwner:
        return {
          title: t("members.ownerRequiresOwnerChange"),
          text: t("members.ownerRequiresOwnerChangeText", { organisation }),
        };
      case ErrorCode.MemberNotFound:
        return {
          title: t("members.memberGone", { name }),
          text: t("members.memberGoneChange"),
        };
      case ErrorCode.RoleEscalation:
        return {
          title: t("members.roleOutOfReach"),
          text: t("members.roleOutOfReachText"),
        };
      case ErrorCode.RoleNotFound:
        return {
          title: t("members.roleGone"),
          text: t("members.roleGoneText"),
        };
      default:
        return {
          title: t("members.changeRoleFailed"),
          text: failureText(change.error),
        };
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("members.changeRoleTitle", { name })}
      description={t("members.changeRoleText", {
        name,
        organisation,
        role: roleSelect(member.role),
        roleName: member.role.name,
      })}
      actionLabel={t("members.changeRole")}
      cancelLabel={t("common.cancel")}
      pending={change.isPending}
      onConfirm={() =>
        change.mutate(undefined, {
          onSuccess: (changed) => {
            onClose();
            showToast({
              title: t("members.roleChanged"),
              description: t("members.roleChangedText", {
                name,
                role: roleSelect(changed.role),
                roleName: changed.role.name,
              }),
            });
          },
        })
      }
    >
      <RoleChoice
        roles={roles.data?.items ?? []}
        value={roleId}
        onChange={setRoleId}
        ownerReason={t("members.giveOwner")}
      />
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
