"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  FieldErrorCode,
  ProblemDetailsError,
  problemOf,
  renameTenant,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { nameText } from "./field-text";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
} from "./organisation";

// The generated schema of RenameTenant (F11). The API trims the name and needs 1 to 100 characters; the generated schema has the
// limit only, so the form adds the trim and the minimum.
const renaming = schemas.RenameTenantBody.extend({
  name: z.string().trim().min(1).max(schemas.renameTenantBodyNameMax),
});
type Renaming = z.infer<typeof renaming>;

/** Rename an organisation (the RenameOrg board). The slug never changes. A refusal shows its text in the dialog (F47). */
export function RenameDialog({
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
  const { name, slug } = organisation;
  const form = useForm<Renaming>({
    resolver: zodResolver(renaming),
    defaultValues: { name },
  });

  const rename = useMutation({
    mutationFn: (body: Renaming) => renameTenant(organisation.id, body),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      if (error.problem?.code === ErrorCode.TenantNameInvalid)
        form.setError("name", { message: FieldErrorCode.Required });
      const action = actionFor(error.status, error.problem);
      const [code] =
        action.kind === "field-errors" ? (action.fields.name ?? []) : [];
      if (code) form.setError("name", { message: code });
    },
  });

  const failure = (() => {
    const code = problemOf(rename.error)?.code;
    if (!rename.error) return null;
    if (
      code === ErrorCode.TenantNameInvalid ||
      code === ErrorCode.RequestValidationFailed
    )
      return null;
    if (code === ErrorCode.TenantNotActive)
      return {
        title: t("renameNotActive", { name }),
        text: t("notActiveText"),
      };
    return {
      title: t("notDone", { name }),
      text: failureText(problemOf(rename.error)),
    };
  })();

  const submit = form.handleSubmit(
    (body) =>
      rename.isPending ||
      rename.mutate(body, {
        onSuccess: (renamed) => {
          onClose();
          showToast({ title: t("renamed", { name: renamed.name }) });
        },
      }),
  );
  const nameError = nameText(form.formState.errors.name?.message);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("renameTitle", { name })}
      description={t.rich("renameText", {
        slug: () => (
          <bdi dir="ltr" className="font-mono">
            {slug}
          </bdi>
        ),
      })}
      actionLabel={t("renameAction")}
      cancelLabel={common("cancel")}
      pending={rename.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        <Input
          label={t("name")}
          hint={t("nameHint", { max: schemas.renameTenantBodyNameMax })}
          autoComplete="off"
          error={
            nameError && t(nameError, { max: schemas.renameTenantBodyNameMax })
          }
          {...form.register("name")}
        />
        {failure && (
          <Banner tone="destructive" title={failure.title}>
            {failure.text}
          </Banner>
        )}
      </form>
    </Dialog>
  );
}
