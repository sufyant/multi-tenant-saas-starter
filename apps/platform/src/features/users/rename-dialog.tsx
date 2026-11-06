"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  FieldErrorCode,
  ProblemDetailsError,
  problemOf,
  renameUser,
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
import { isolated } from "@/shared/isolated";
import { nameOf, userNameOf } from "@/shared/name-of";
import { afterRefusal, refreshUser, type User } from "./user";

// The generated schema of RenameUser (F11). The API trims the name and needs 1 to 100 characters (A7); the generated schema has the
// limit only, so the form adds the trim and the minimum.
const renaming = schemas.RenameUserBody.extend({
  name: z.string().trim().min(1).max(schemas.renameUserBodyNameMax),
});
type Renaming = z.infer<typeof renaming>;

const tooLong = (error: string) =>
  error === FieldErrorCode.TooLong || error === "validation.too_long";

/** The name's error, a Zod key or an API field code, as the board's text for that field (the Messages board). */
const fieldText = (error: string | undefined) => {
  if (!error) return undefined;
  return tooLong(error) ? "nameTooLong" : "nameRequired";
};

/** Rename a user (the RenameUser board): the one name, whole. A refusal shows its text in the dialog (F47). */
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
  const name = userNameOf(user, t("deletedUser"));
  const form = useForm<Renaming>({
    resolver: zodResolver(renaming),
    defaultValues: { name: user.name ?? "" },
  });

  const rename = useMutation({
    mutationFn: (body: Renaming) => renameUser(user.id, body),
    onSuccess: () => refreshUser(queryClient, user.id),
    onError: (error) => {
      afterRefusal(queryClient, user.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      const action = actionFor(error.status, error.problem);
      if (action.kind !== "field-errors") return;
      const [code] = action.fields.name ?? [];
      if (code) form.setError("name", { message: code });
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
    return {
      title: t.rich("notDone", { name, ...isolated }),
      text: failureText(problemOf(rename.error)),
    };
  })();

  const submit = form.handleSubmit(
    (body) =>
      rename.isPending ||
      rename.mutate(body, {
        onSuccess: (renamed) => {
          onClose();
          showToast({
            title: t.rich("renamed", {
              name: nameOf(renamed) ?? name,
              ...isolated,
            }),
          });
        },
      }),
  );
  const max = schemas.renameUserBodyNameMax;
  const error = fieldText(form.formState.errors.name?.message);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("renameTitle", { name, ...isolated })}
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
        <Input
          label={t("name")}
          autoComplete="off"
          error={error && t(error, { max })}
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
