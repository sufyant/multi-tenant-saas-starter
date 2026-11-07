"use client";

import {
  changeMemberRole,
  ErrorCode,
  type MemberResponse,
  Permission,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { afterRefusal, refreshMembers, refreshTenant } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import { usePlanReason } from "@/shared/use-plan-reason";
import { roleSelect } from "@/shared/use-role-name";
import { nameOf } from "./person";
import { RoleChoice } from "./role-choice";

/**
 * Change a member's role (8.19, 8.38): every role of the tenant (ListRoles); a role the viewer's role does not fully hold is
 * disabled, with its reason (F51). The viewer's own row is "Change your role". The last Owner (ListRoles' Owner `memberCount`)
 * hears so when the dialog opens: every other role is disabled and the dialog only informs (8.20). The API decides; each refusal
 * shows its text in the dialog (F47).
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
  const planReason = usePlanReason();
  const [roleId, setRoleId] = useState(member.role.id);
  const name = nameOf(member) ?? t("members.deletedUser");
  const organisation = tenant.name;
  // The Owner role is the one that holds tenant.owners.manage (K1, F51).
  const owners = roles.data?.items.find((role) =>
    role.permissions.includes(Permission.TenantOwnersManage),
  );
  const lastOwner = member.role.id === owners?.id && owners.memberCount === 1;

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
    if (lastOwner)
      return {
        title: t("members.lastOwner", { organisation }),
        text: t("members.lastOwnerChange"),
      };
    if (!change.error) return null;
    const plan = planReason.refusal(change.error);
    if (plan) return { title: plan.title, text: plan.text };
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
          text: failureText(problemOf(change.error)),
        };
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={
        self
          ? t("members.changeYourRole")
          : t("members.changeRoleTitle", { name })
      }
      description={
        lastOwner
          ? undefined
          : t("members.changeRoleText", {
              name,
              organisation,
              role: roleSelect(member.role),
              roleName: member.role.name,
            })
      }
      action={lastOwner ? "secondary" : "primary"}
      actionLabel={lastOwner ? t("common.close") : t("members.changeRole")}
      cancelLabel={lastOwner ? undefined : t("common.cancel")}
      pending={change.isPending}
      onConfirm={() =>
        lastOwner
          ? onClose()
          : change.mutate(undefined, {
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
      {lastOwner && failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
      <RoleChoice
        roles={roles.data?.items ?? []}
        value={roleId}
        onChange={setRoleId}
        ownerReason={t("members.giveOwner")}
        lockedReason={lastOwner ? t("members.lastOwnerChange") : undefined}
      />
      {!lastOwner && failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
