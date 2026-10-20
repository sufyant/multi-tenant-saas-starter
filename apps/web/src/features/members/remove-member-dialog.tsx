"use client";

import { ErrorCode, type MemberResponse, removeMember } from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { problemOf } from "@/shared/problem";
import { useTenant } from "@/shared/tenant-context";
import { useFailureText } from "@/shared/use-failure-text";
import { nameOf } from "./person";
import { afterRefusal, refreshMembers } from "./refresh";

/**
 * Remove a member (the RemoveMember and PhoneRemove boards): it names the person and the organisation. The API decides; each
 * refusal shows its text in the dialog (F47).
 */
export function RemoveMemberDialog({
  member,
  onClose,
}: {
  member: MemberResponse;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const name = nameOf(member) ?? t("members.deletedUser");
  const organisation = tenant.name;

  const remove = useMutation({
    mutationFn: () => removeMember(tenant.id, member.userId),
    onSuccess: () => refreshMembers(queryClient, tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      if (problemOf(error)?.code === ErrorCode.MemberNotFound)
        void refreshMembers(queryClient, tenant.id);
    },
  });

  const failure = (() => {
    if (!remove.error) return null;
    switch (problemOf(remove.error)?.code) {
      case ErrorCode.MemberLastOwner:
        return {
          title: t("members.lastOwner", { organisation }),
          text: t("members.lastOwnerRemove"),
        };
      case ErrorCode.MemberOwnerRequiresOwner:
        return {
          title: t("members.ownerRequiresOwnerRemove"),
          text: t("members.ownerRequiresOwnerRemoveText", { organisation }),
        };
      case ErrorCode.MemberNotFound:
        return {
          title: t("members.memberGone", { name }),
          text: t("members.memberGoneRemove"),
        };
      default:
        return {
          title: t("members.removeFailed", { name }),
          text: failureText(remove.error),
        };
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("members.removeTitle", { name, organisation })}
      description={t("members.removeText")}
      actionLabel={t("members.removeAction")}
      cancelLabel={t("common.cancel")}
      pending={remove.isPending}
      onConfirm={() =>
        remove.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({ title: t("members.removed", { name, organisation }) });
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
