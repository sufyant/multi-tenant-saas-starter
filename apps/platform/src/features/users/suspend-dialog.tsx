"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  FieldErrorCode,
  getGetUserQueryOptions,
  ProblemDetailsError,
  problemOf,
  schemas,
  suspendUser,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { Textarea } from "@repo/ui/components/ui/textarea";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { cn } from "@repo/ui/lib/utils";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { isolated } from "@/shared/isolated";
import { userNameOf } from "@/shared/name-of";
import { afterRefusal, refreshUser, type User } from "./user";

/** A list names at most three, then "and n more" (the Messages board, F15). */
const named = 3;

// The generated schema of SuspendUser (F11). The API trims the reason and needs 1 to 500 characters, as for an organisation; the
// generated schema has the limit only, so the form adds the trim and the minimum.
const suspension = schemas.SuspendUserBody.extend({
  reason: z.string().trim().min(1).max(schemas.suspendUserBodyReasonMax),
});
type Suspension = z.infer<typeof suspension>;

/** The reason's error: a Zod message key of the form (F12) or a field code of the API, as a key under `users`. */
const reasonText = (error: string | undefined) => {
  if (!error) return undefined;
  return error === FieldErrorCode.TooLong || error === "validation.too_long"
    ? "reasonTooLong"
    : "reasonRequired";
};

/**
 * Suspend a user (the SuspendUser board). It asks for a reason that the user never sees, as an organisation's suspension does. Where the
 * user is the last Owner of an organisation (`memberships[].lastOwner`), it warns by the organisation's name; the warning never blocks.
 * A refusal shows its text in the dialog (F47).
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
  const fieldId = useId();
  const name = userNameOf(user, t("deletedUser"));
  const form = useForm<Suspension>({
    resolver: zodResolver(suspension),
    defaultValues: { reason: "" },
  });
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
    mutationFn: (body: Suspension) => suspendUser(user.id, body),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => {
      afterRefusal(queryClient, user.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      const action = actionFor(error.status, error.problem);
      const [code] =
        action.kind === "field-errors" ? (action.fields.reason ?? []) : [];
      if (code) form.setError("reason", { message: code });
    },
  });

  const failure = (() => {
    const code = problemOf(suspend.error)?.code;
    if (!suspend.error || code === ErrorCode.RequestValidationFailed)
      return null;
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

  const submit = form.handleSubmit(
    (body) =>
      suspend.isPending ||
      suspend.mutate(body, {
        onSuccess: () => {
          onClose();
          showToast({
            title: t.rich("suspendedDone", { name, ...isolated }),
            description: t("suspendedToast"),
          });
        },
      }),
  );
  const reasonError = reasonText(form.formState.errors.reason?.message);

  return (
    <Dialog
      size="wide"
      open
      tone="destructive"
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("suspendTitle", { name, ...isolated })}
      description={t("suspendText")}
      actionLabel={t("suspendAction")}
      cancelLabel={common("cancel")}
      pending={suspend.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-1.5"
      >
        <label htmlFor={fieldId} className="text-body font-medium">
          {t("reason")}
        </label>
        <Textarea
          id={fieldId}
          rows={3}
          aria-invalid={reasonError ? true : undefined}
          aria-describedby={`${fieldId}-note`}
          className="rounded-md bg-card px-3 text-body"
          {...form.register("reason")}
        />
        <p
          id={`${fieldId}-note`}
          className={cn(
            "text-small",
            reasonError ? "text-destructive-text" : "text-muted-foreground",
          )}
        >
          {t(reasonError ?? "reasonHint", {
            max: schemas.suspendUserBodyReasonMax,
          })}
        </p>
      </form>
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
