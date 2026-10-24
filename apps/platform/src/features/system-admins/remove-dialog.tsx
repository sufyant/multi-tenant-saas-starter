"use client";

import { useSession } from "@clerk/nextjs";
import {
  ErrorCode,
  revokeSystemAdmin,
  type UserResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import { ReverifyStep } from "./reverify-step";
import {
  adminNameOf,
  afterRefusal,
  needsReverification,
  refreshSystemAdmin,
} from "./system-admin";

/**
 * Remove a system admin (the RemoveSystemAdmin board), from this page's row menu only: red, and the memberships stay. RevokeSystemAdmin
 * takes no Idempotency-Key (I1, a DELETE), so after a stale second factor (403, A6a) the code step checks it again and the
 * dialog sends the same request as it is. A refusal shows its text in the dialog (F47).
 */
export function RemoveDialog({
  user,
  onClose,
}: {
  user: UserResponse;
  onClose: () => void;
}) {
  const t = useTranslations("systemAdmins");
  const users = useTranslations("users");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { session } = useSession();
  const [reverifying, setReverifying] = useState(false);
  const name = adminNameOf(user);

  const revoke = useMutation({
    mutationFn: () => revokeSystemAdmin(user.id),
    onSuccess: () => refreshSystemAdmin(queryClient, user.id),
    onError: (error) => {
      if (needsReverification(error)) return setReverifying(true);
      afterRefusal(queryClient, user.id, error);
    },
  });

  const send = () =>
    revoke.isPending ||
    revoke.mutate(undefined, {
      onSuccess: () => {
        onClose();
        showToast({ title: t("noLongerAdmin", { name }) });
      },
    });

  if (reverifying && session)
    return (
      <ReverifyStep
        session={session}
        onCancel={onClose}
        onVerified={() => {
          setReverifying(false);
          send();
        }}
      />
    );

  const failure = (() => {
    if (!revoke.error || needsReverification(revoke.error)) return null;
    switch (problemOf(revoke.error)?.code) {
      case ErrorCode.SystemAdminRoot:
        return { title: t("rootCannotRemove"), text: null };
      case ErrorCode.SystemAdminCannotRevokeSelf:
        return { title: t("cannotRemoveSelf"), text: null };
      case ErrorCode.SystemAdminNotFound:
        return { title: t("noLongerAdmin", { name }), text: null };
      case ErrorCode.UserNotFound:
        return { title: users("notFound"), text: null };
      default:
        return {
          title: users("notDone", { name }),
          text: failureText(revoke.error),
        };
    }
  })();

  return (
    <Dialog
      open
      tone="destructive"
      onOpenChange={(open) => !open && onClose()}
      title={t("removeTitle", { name })}
      description={t("removeText")}
      actionLabel={t("removeAction")}
      cancelLabel={common("cancel")}
      pending={revoke.isPending}
      onConfirm={send}
    >
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
