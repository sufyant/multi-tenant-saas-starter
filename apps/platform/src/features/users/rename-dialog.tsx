"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  FieldErrorCode,
  ProblemDetailsError,
  renameUser,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import {
  afterRefusal,
  displayNameOf,
  nameOf,
  refreshUser,
  type User,
} from "./user";

// The generated schema of RenameUser (F11). The API trims both names and needs 1 to 100 characters each (A7); the generated schema
// has the limits only, so the form adds the trim and the minimum.
const renaming = schemas.RenameUserBody.extend({
  firstName: z.string().trim().min(1).max(schemas.renameUserBodyFirstNameMax),
  lastName: z.string().trim().min(1).max(schemas.renameUserBodyLastNameMax),
});
type Renaming = z.infer<typeof renaming>;
type Field = keyof Renaming;

const fields: Field[] = ["firstName", "lastName"];

const tooLong = (error: string) =>
  error === FieldErrorCode.TooLong || error === "validation.too_long";

/** A name's error, a Zod key or an API field code, as the board's text for that field (the Messages board). */
const fieldText = (field: Field, error: string | undefined) => {
  if (!error) return undefined;
  if (tooLong(error)) return "nameTooLong";
  return field === "firstName" ? "firstNameRequired" : "lastNameRequired";
};

/** Rename a user (the RenameUser board): the first and the last name. A refusal shows its text in the dialog (F47). */
export function RenameDialog({
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
  const name = displayNameOf(user, t("deletedUser"));
  const form = useForm<Renaming>({
    resolver: zodResolver(renaming),
    defaultValues: {
      firstName: user.firstName ?? "",
      lastName: user.lastName ?? "",
    },
  });

  const rename = useMutation({
    mutationFn: (body: Renaming) => renameUser(user.id, body),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => {
      afterRefusal(queryClient, user.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      const action = actionFor(error.status, error.problem);
      if (action.kind !== "field-errors") return;
      for (const field of fields) {
        const [code] = action.fields[field] ?? [];
        if (code) form.setError(field, { message: code });
      }
    },
  });

  const failure = (() => {
    const code = problemOf(rename.error)?.code;
    if (!rename.error || code === ErrorCode.RequestValidationFailed)
      return null;
    if (code === ErrorCode.UserDeleted)
      return { title: t("wasDeleted"), text: t("wasDeletedText") };
    if (code === ErrorCode.UserNotFound)
      return { title: t("notFound"), text: null };
    return { title: t("notDone", { name }), text: failureText(rename.error) };
  })();

  const submit = form.handleSubmit(
    (body) =>
      rename.isPending ||
      rename.mutate(body, {
        onSuccess: (renamed) => {
          onClose();
          showToast({
            title: t("renamed", { name: nameOf(renamed) ?? name }),
          });
        },
      }),
  );
  const max = schemas.renameUserBodyFirstNameMax;
  const errorOf = (field: Field) => {
    const key = fieldText(field, form.formState.errors[field]?.message);
    return key && t(key, { max });
  };

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("renameTitle", { name })}
      description={t("renameText")}
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
        <div className="grid grid-cols-1 gap-3 tablet:grid-cols-2">
          <Input
            label={t("firstName")}
            autoComplete="off"
            error={errorOf("firstName")}
            {...form.register("firstName")}
          />
          <Input
            label={t("lastName")}
            autoComplete="off"
            error={errorOf("lastName")}
            {...form.register("lastName")}
          />
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
