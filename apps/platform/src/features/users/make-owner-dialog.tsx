"use client";

import {
  ErrorCode,
  makeOwner,
  problemOf,
  type UserMembershipResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { isolated } from "@/shared/isolated";
import { afterRefusal, displayNameOf, refreshUser, type User } from "./user";

/**
 * Make a member an Owner of an organisation (the MakeOwner board), the platform's Owner recovery (the API's K13). It works with or
 * without an Owner, and the old Owner stays. MakeOwner takes no Idempotency-Key: it changes a role and creates nothing (I1). A
 * refusal shows its text in the dialog, and the user's page reads again (F47).
 */
export function MakeOwnerDialog({
  user,
  membership,
  onClose,
}: {
  user: User;
  membership: UserMembershipResponse;
  onClose: () => void;
}) {
  const t = useTranslations("users");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const name = displayNameOf(user, t("deletedUser"));
  const organisation = membership.tenantName;

  const make = useMutation({
    mutationFn: () => makeOwner(membership.tenantId, { userId: user.id }),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => afterRefusal(queryClient, user.id, error),
  });

  const failure = (() => {
    if (!make.error) return null;
    switch (problemOf(make.error)?.code) {
      case ErrorCode.MemberAlreadyOwner:
        return t.rich("alreadyOwnerRaced", { name, organisation, ...isolated });
      case ErrorCode.TenantNotActive:
        return t.rich("organisationNotActive", { organisation, ...isolated });
      case ErrorCode.MemberNotFound:
        return t.rich("notAMember", { name, organisation, ...isolated });
      case ErrorCode.UserSuspended:
        return t.rich("reactivateFirst", { name, ...isolated });
      case ErrorCode.UserDeleted:
        return t("wasDeleted");
      default:
        return null;
    }
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("makeOwnerTitle", { name, organisation, ...isolated })}
      description={t.rich("makeOwnerText", { organisation, ...isolated })}
      actionLabel={t("makeOwner")}
      cancelLabel={common("cancel")}
      pending={make.isPending}
      onConfirm={() =>
        make.isPending ||
        make.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: t.rich("madeOwner", { name, organisation, ...isolated }),
            });
          },
        })
      }
    >
      {make.error && (
        <Banner
          tone="destructive"
          title={failure ?? t.rich("notDone", { name, ...isolated })}
        >
          {failure ? null : failureText(problemOf(make.error))}
        </Banner>
      )}
    </Dialog>
  );
}
