"use client";

import {
  deleteUser,
  ErrorCode,
  getGetUserQueryOptions,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Initial } from "@/shared/initial";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import { afterRefusal, displayNameOf, refreshUser, type User } from "./user";

const organisationPage = (tenantId: string) => `/organisations/${tenantId}`;

/**
 * Delete a user (the DeleteUser board): red, and the user's email typed to confirm. The user leaves every organisation and their
 * email and names are erased (A8). When they are the last Owner somewhere (409 `user.last_owner`), the dialog names those
 * organisations from the user's memberships by the answer's `tenantIds`. A refusal shows its text in the dialog (F47).
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
  const router = useRouter();
  const queryClient = useQueryClient();
  const name = displayNameOf(user, t("deletedUser"));
  // The list's row has no memberships. The user's page has read them already, so the dialog takes them from the cache there.
  const detail = useQuery(
    getGetUserQueryOptions(user.id, { query: { refetchOnMount: false } }),
  );

  const remove = useMutation({
    mutationFn: () => deleteUser(user.id),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => afterRefusal(queryClient, user.id, error),
  });

  const problem = problemOf(remove.error);
  const close = (open: boolean) => !open && onClose();

  if (problem?.code === ErrorCode.UserLastOwner) {
    const tenantIds = problem.tenantIds ?? [];
    const organisations = (detail.data?.memberships ?? []).filter(
      (membership) => tenantIds.includes(membership.tenantId),
    );
    const [first] = organisations;
    return (
      <Dialog
        open
        onOpenChange={close}
        title={t("cannotDelete", { name })}
        description={t("cannotDeleteText", { count: tenantIds.length })}
        actionLabel={t("openOrganisation")}
        cancelLabel={common("close")}
        onConfirm={() => first && router.push(organisationPage(first.tenantId))}
      >
        <ul className="flex flex-col gap-1.5">
          {organisations.map((organisation) => (
            <li key={organisation.tenantId}>
              <Link
                href={organisationPage(organisation.tenantId)}
                className="flex items-center gap-2.5 rounded-md border border-border px-3 py-2 hover:bg-muted"
              >
                <Initial name={organisation.tenantName} />
                <bdi className="flex-1 font-medium">
                  {organisation.tenantName}
                </bdi>
                <bdi
                  dir="ltr"
                  className="font-mono text-small text-muted-foreground"
                >
                  {organisation.tenantSlug}
                </bdi>
              </Link>
            </li>
          ))}
        </ul>
      </Dialog>
    );
  }

  const failure = (() => {
    if (!remove.error) return null;
    if (problem?.code === ErrorCode.UserIsSystemAdmin)
      return {
        title: t("isSystemAdmin", { name }),
        text: t("isSystemAdminText"),
      };
    if (problem?.code === ErrorCode.UserHasMemberships)
      return {
        title: t("joinedMeanwhile", { name }),
        text: t("joinedMeanwhileText"),
      };
    if (problem?.code === ErrorCode.UserNotFound)
      return { title: t("notFound"), text: null };
    return { title: t("notDone", { name }), text: failureText(remove.error) };
  })();
  const email = user.email ?? "";

  return (
    <Dialog
      open
      tone="destructive"
      onOpenChange={close}
      title={t("deleteTitle", { name })}
      description={t("deleteText")}
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
            showToast({ title: t("deletedDone", { name }) });
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
