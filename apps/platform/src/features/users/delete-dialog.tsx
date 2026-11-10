"use client";

import {
  deleteUser,
  ErrorCode,
  getGetUserQueryOptions,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { isolated } from "@/shared/isolated";
import { userNameOf } from "@/shared/name-of";
import { useOrganisationList } from "./organisation-list";
import { afterRefusal, refreshUser, type User } from "./user";

/**
 * Delete a user (5.13): red, and the user's email typed to confirm. The user leaves every organisation and their email and names
 * are erased (A8). A delete the API would refuse opens at once with its reason and only Close, as a stopped action does (F56): a
 * system admin (5.15, `isSystemAdmin`), or the only Owner of an organisation (5.14, `memberships[].lastOwner` of GetUser). The
 * API stays the guard: its 409 `user.is_system_admin` or `user.last_owner` shows the same refusal, and the page reads again (F47).
 */
export function DeleteDialog({
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
  const organisationList = useOrganisationList();
  const name = userNameOf(user, t("deletedUser"));
  // The list's row has no memberships. The user's page has read them already, so the dialog takes them from the cache there.
  const detail = useQuery(
    getGetUserQueryOptions(user.id, { query: { refetchOnMount: false } }),
  );
  const memberships = detail.data?.memberships ?? [];

  const remove = useMutation({
    mutationFn: () => deleteUser(user.id),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => afterRefusal(queryClient, user.id, error),
  });

  const problem = problemOf(remove.error);
  const close = (open: boolean) => !open && onClose();
  const refused = (title: ReactNode, text: ReactNode) => (
    <Dialog
      size="wide"
      open
      onOpenChange={close}
      title={title}
      description={text}
      actionLabel={common("close")}
      onConfirm={onClose}
    />
  );

  if (user.isSystemAdmin || problem?.code === ErrorCode.UserIsSystemAdmin)
    return refused(
      t.rich("cannotDeleteAdmin", { name, ...isolated }),
      t.rich("cannotDeleteAdminText", { name, ...isolated }),
    );
  const tenantIds = new Set(
    problem?.code === ErrorCode.UserLastOwner ? (problem.tenantIds ?? []) : [],
  );
  const lastOwnerOf = memberships
    .filter(
      (membership) =>
        membership.lastOwner || tenantIds.has(membership.tenantId),
    )
    .map((membership) => membership.tenantName);
  if (lastOwnerOf.length > 0)
    return refused(
      t.rich("cannotDeleteYet", { name, ...isolated }),
      t.rich("cannotDeleteLastOwner", {
        name,
        ...isolated,
        ...organisationList(lastOwnerOf),
      }),
    );

  const failure = (() => {
    if (!remove.error) return null;
    if (problem?.code === ErrorCode.UserHasMemberships)
      return {
        title: t.rich("joinedMeanwhile", { name, ...isolated }),
        text: t("joinedMeanwhileText"),
      };
    if (problem?.code === ErrorCode.UserNotFound)
      return { title: t("notFound"), text: null };
    return {
      title: t.rich("notDone", { name, ...isolated }),
      text: failureText(problem),
    };
  })();
  const email = user.email ?? "";
  const organisations = memberships.map((membership) => membership.tenantName);

  return (
    <Dialog
      size="wide"
      open
      tone="destructive"
      onOpenChange={close}
      title={t.rich("deleteTitle", { name, ...isolated })}
      description={t.rich("deleteText", {
        name,
        count: organisations.length,
        ...isolated,
        ...organisationList(organisations),
      })}
      actionLabel={t("deleteAction")}
      cancelLabel={common("cancel")}
      confirmText={email}
      confirmLabel={t("deleteConfirm", { email })}
      pending={remove.isPending}
      onConfirm={() =>
        remove.isPending ||
        remove.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({ title: t.rich("deletedDone", { name, ...isolated }) });
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
