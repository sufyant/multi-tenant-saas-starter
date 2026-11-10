"use client";

import {
  ErrorCode,
  getGetUserQueryOptions,
  problemOf,
  reactivateUser,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { isolated } from "@/shared/isolated";
import { userNameOf } from "@/shared/name-of";
import { useOrganisationList } from "./organisation-list";
import { afterRefusal, refreshUser, type User } from "./user";

/**
 * Reactivate a suspended user (5.11): the sentence names the organisations where their roles stay, from GetUser. A refusal shows its
 * text in the dialog (F47).
 */
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
  const name = userNameOf(user, t("deletedUser"));
  const organisationList = useOrganisationList();
  // The list's row has no memberships. The user's page has read them already, so the dialog takes them from the cache there.
  const detail = useQuery(
    getGetUserQueryOptions(user.id, { query: { refetchOnMount: false } }),
  );
  const organisations = (detail.data?.memberships ?? []).map(
    (membership) => membership.tenantName,
  );

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
      title: t.rich("notDone", { name, ...isolated }),
      text: failureText(problemOf(reactivate.error)),
    };
  })();

  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("reactivateTitle", { name, ...isolated })}
      description={t.rich("reactivateText", {
        name,
        count: organisations.length,
        ...isolated,
        list: () => organisationList(organisations),
      })}
      actionLabel={t("reactivateAction")}
      cancelLabel={common("cancel")}
      pending={reactivate.isPending}
      onConfirm={() =>
        reactivate.isPending ||
        reactivate.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({ title: t.rich("reactivated", { name, ...isolated }) });
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
