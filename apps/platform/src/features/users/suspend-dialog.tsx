"use client";

import {
  ErrorCode,
  getGetUserQueryOptions,
  problemOf,
  suspendUser,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { isolated } from "@/shared/isolated";
import { afterRefusal, displayNameOf, refreshUser, type User } from "./user";

/** A list names at most three, then "and n more" (the Messages board, F15). */
const named = 3;

/**
 * Suspend a user (the SuspendUser board). Where the user is the last Owner of an organisation (`memberships[].lastOwner`), it
 * warns by the organisation's name; the warning never blocks. A refusal shows its text in the dialog (F47).
 */
export function SuspendDialog({
  user,
  onClose,
}: {
  user: User;
  onClose: () => void;
}) {
  const t = useTranslations("users");
  const locale = useTranslations("locale")("format");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const name = displayNameOf(user, t("deletedUser"));
  // The list's row has no memberships. The user's page has read them already, so the dialog takes them from the cache there.
  const detail = useQuery(
    getGetUserQueryOptions(user.id, { query: { refetchOnMount: false } }),
  );
  const lastOwnerOf = (detail.data?.memberships ?? [])
    .filter((membership) => membership.lastOwner)
    .map((membership) => membership.tenantName);
  const more = lastOwnerOf.length - named;
  // Each organisation's name is isolated in the list, as in the sentence around it; the list's words stay text.
  const organisations = new Intl.ListFormat(locale, {
    type: "conjunction",
  })
    .formatToParts(
      more > 0
        ? [
            ...lastOwnerOf.slice(0, named),
            t("moreOrganisations", { count: more }),
          ]
        : lastOwnerOf,
    )
    .map((part, index) =>
      part.type === "element" && lastOwnerOf.includes(part.value) ? (
        // Two organisations may share a name; a part's place in the one formatted list never changes.
        // biome-ignore lint/suspicious/noArrayIndexKey: the parts of a formatted list have no id of their own.
        <bdi key={index}>{part.value}</bdi>
      ) : (
        part.value
      ),
    );

  const suspend = useMutation({
    mutationFn: () => suspendUser(user.id),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => afterRefusal(queryClient, user.id, error),
  });

  const failure = (() => {
    if (!suspend.error) return null;
    const code = problemOf(suspend.error)?.code;
    if (code === ErrorCode.UserIsSystemAdmin)
      return {
        title: t.rich("isSystemAdmin", { name, ...isolated }),
        text: t("isSystemAdminText"),
      };
    if (code === ErrorCode.UserNotFound)
      return { title: t("notFound"), text: null };
    return {
      title: t.rich("notDone", { name, ...isolated }),
      text: failureText(problemOf(suspend.error)),
    };
  })();

  return (
    <Dialog
      open
      tone="destructive"
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("suspendTitle", { name, ...isolated })}
      description={t("suspendText")}
      actionLabel={t("suspendAction")}
      cancelLabel={common("cancel")}
      pending={suspend.isPending}
      onConfirm={() =>
        suspend.isPending ||
        suspend.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: t.rich("suspendedDone", { name, ...isolated }),
              description: t("suspendedToast"),
            });
          },
        })
      }
    >
      {lastOwnerOf.length > 0 && (
        <Banner
          tone="warning"
          title={t.rich("lastOwnerWarning", {
            name,
            ...isolated,
            list: () => organisations,
          })}
        >
          {t("lastOwnerWarningText")}
        </Banner>
      )}
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
