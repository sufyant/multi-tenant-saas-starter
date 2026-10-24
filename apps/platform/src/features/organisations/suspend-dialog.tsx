"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  ProblemDetailsError,
  schemas,
  suspendTenant,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { Textarea } from "@repo/ui/components/ui/textarea";
import { cn } from "@repo/ui/lib/utils";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import { reasonText } from "./field-text";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
} from "./organisation";

// The generated schema of SuspendTenant (F11). The API trims the reason and needs 1 to 500 characters; the generated schema has
// the limit only, so the form adds the trim and the minimum.
const suspension = schemas.SuspendTenantBody.extend({
  reason: z.string().trim().min(1).max(schemas.suspendTenantBodyReasonMax),
});
type Suspension = z.infer<typeof suspension>;

/**
 * Suspend an organisation (the SuspendOrg board): it names the organisation and its members, and asks for a reason that members
 * never see. A refusal shows its text in the dialog (F47).
 */
export function SuspendDialog({
  organisation,
  onClose,
}: {
  organisation: Organisation;
  onClose: () => void;
}) {
  const t = useTranslations("organisations");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const fieldId = useId();
  const { name, memberCount: count } = organisation;
  const form = useForm<Suspension>({
    resolver: zodResolver(suspension),
    defaultValues: { reason: "" },
  });

  const suspend = useMutation({
    mutationFn: (body: Suspension) => suspendTenant(organisation.id, body),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
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
    if (code === ErrorCode.TenantNotActive)
      return {
        title: t("suspendNotActive", { name }),
        text: t("notActiveText"),
      };
    return { title: t("notDone", { name }), text: failureText(suspend.error) };
  })();

  const submit = form.handleSubmit(
    (body) =>
      suspend.isPending ||
      suspend.mutate(body, {
        onSuccess: () => {
          onClose();
          showToast({
            title: t("suspendedDone", { name }),
            description: t("suspendedToast", { count }),
          });
        },
      }),
  );
  const reasonError = reasonText(form.formState.errors.reason?.message);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("suspendTitle", { name })}
      description={t("suspendText", { count })}
      actionLabel={t("suspendAction")}
      cancelLabel={common("cancel")}
      pending={suspend.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        <div className="flex flex-col gap-1.5">
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
              max: schemas.suspendTenantBodyReasonMax,
            })}
          </p>
        </div>
        {failure && (
          <Banner tone="destructive" title={failure.title}>
            {failure.text}
          </Banner>
        )}
      </form>
    </Dialog>
  );
}
