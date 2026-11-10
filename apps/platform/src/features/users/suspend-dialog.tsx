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
import { useOrganisationList } from "./organisation-list";
import { afterRefusal, refreshUser, type User } from "./user";

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
 * Suspend a user (5.10). It asks for a reason that only the platform sees, as an organisation's suspension does. Where the user is
 * the only Owner of an organisation (`memberships[].lastOwner`), it warns by the organisation's name; the warning never blocks. A
 * refusal shows its text in the dialog (F47); a deleted user is offered no Suspend, so `user.deleted` is the race of another admin's
 * delete.
 */
export function SuspendDialog({
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
  const organisationList = useOrganisationList();
  const lastOwnerOf = (detail.data?.memberships ?? [])
    .filter((membership) => membership.lastOwner)
    .map((membership) => membership.tenantName);

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
    if (code === ErrorCode.UserDeleted)
      return { title: t("wasDeleted"), text: null };
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
      description={t.rich("suspendText", { name, ...isolated })}
      actionLabel={t("suspendAction")}
      cancelLabel={common("cancel")}
      pending={suspend.isPending}
      onConfirm={() => void submit()}
    >
      {/* The warning never blocks (5.10): the frame draws it as the dialog's red text, above the reason. */}
      {lastOwnerOf.length > 0 && (
        <p className="text-small text-destructive-text">
          {t.rich("lastOwnerWarning", {
            name,
            count: lastOwnerOf.length,
            ...isolated,
            ...organisationList(lastOwnerOf),
          })}
        </p>
      )}
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
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
