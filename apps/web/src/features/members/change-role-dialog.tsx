"use client";

import {
  changeMemberRole,
  ErrorCode,
  type MemberResponse,
  MemberRole,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useTenant } from "@/shared/tenant-context";
import { nameOf } from "./person";
import { problemOf } from "./problem";
import {
  refreshMembers,
  refreshTenant,
  refreshWhenTenantGone,
} from "./refresh";
import { RoleChoice, rolesFor } from "./role-choice";
import { useFailureText } from "./use-failure-text";

/**
 * Change a member's role (the ChangeRole board): the three roles, Owner only for an Owner (K1). The API decides; each refusal shows
 * its text in the dialog (F47).
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
  const { tenant, detail } = useTenant();
  const [role, setRole] = useState<MemberRole>(
    member.role.builtIn ?? MemberRole.Member,
  );
  const name = nameOf(member) ?? t("members.deletedUser");
  const organisation = tenant.name;

  const change = useMutation({
    mutationFn: () => changeMemberRole(tenant.id, member.userId, { role }),
    onSuccess: () =>
      Promise.all([
        refreshMembers(queryClient, tenant.id),
        self && refreshTenant(queryClient, tenant.id),
      ]),
    onError: (error) => {
      refreshWhenTenantGone(queryClient, tenant.id, error);
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
        role: member.role.builtIn ?? "",
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
                role: changed.role.builtIn ?? "",
                roleName: changed.role.name,
              }),
            });
          },
        })
      }
    >
      <RoleChoice
        roles={rolesFor(detail.role.builtIn === MemberRole.Owner)}
        value={role}
        onChange={setRole}
      />
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
