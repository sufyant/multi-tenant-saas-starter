"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  getGetMeQueryOptions,
  inviteOwner,
  Language,
  ProblemDetailsError,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { problemOf } from "@/shared/problem";
import { useFailureText } from "@/shared/use-failure-text";
import { emailText } from "./field-text";
import { LanguageChoice } from "./language-choice";
import {
  afterRefusal,
  type Organisation,
  refreshOrganisation,
  savedUnderKey,
} from "./organisation";
import { useDates } from "./use-dates";

type Invitation = z.infer<typeof schemas.InviteOwnerBody>;

/**
 * Invite the Owner of an organisation that has none (the InviteOwner board), with the generated schema of InviteOwner (F11). The
 * email starts as the last invited address and the language as that invitation's, else the system admin's own. The new invitation
 * replaces the pending one. One Idempotency-Key per intent (F48): a new key when the dialog opens and when a field changes.
 */
export function InviteOwnerDialog({
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
  const { date } = useDates();
  const me = useQuery(getGetMeQueryOptions());
  const { key, renew } = useIntentKey();
  const { name } = organisation;
  const last = organisation.ownerInvitation;
  const form = useForm<Invitation>({
    resolver: zodResolver(schemas.InviteOwnerBody),
    defaultValues: {
      email: last?.email ?? "",
      language: last?.language ?? me.data?.language ?? Language.En,
    },
  });
  // Another email or language is another intent: it gets a new key.
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );

  const invite = useMutation({
    mutationFn: ({
      invitation,
      key,
    }: {
      invitation: Invitation;
      key: string;
    }) => inviteOwner(organisation.id, invitation, { "Idempotency-Key": key }),
    onSuccess: () => refreshOrganisation(queryClient, organisation.id),
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
      if (savedUnderKey(error)) renew();
      if (!(error instanceof ProblemDetailsError)) return;
      const action = actionFor(error.status, error.problem);
      const [code] =
        action.kind === "field-errors" ? (action.fields.email ?? []) : [];
      if (code) form.setError("email", { message: code });
    },
  });

  const failure = (() => {
    if (!invite.error) return null;
    switch (problemOf(invite.error)?.code) {
      case ErrorCode.RequestValidationFailed:
        return null;
      case ErrorCode.TenantHasOwner:
        return { title: t("hasOwner", { name }), text: t("hasOwnerText") };
      case ErrorCode.TenantNotActive:
        return {
          title: t("inviteNotActive", { name }),
          text: t("inviteNotActiveText"),
        };
      default:
        return {
          title: t("notDone", { name }),
          text: failureText(invite.error),
        };
    }
  })();

  // The button is disabled while the request runs; Enter in the email field is held back the same way (F47).
  const submit = form.handleSubmit(
    (invitation) =>
      invite.isPending ||
      invite.mutate(
        { invitation, key },
        {
          onSuccess: (sent) => {
            renew();
            onClose();
            showToast({
              title: t("invited"),
              description: t("invitedText", {
                email: sent.email,
                date: date(sent.expiresAt),
              }),
            });
          },
        },
      ),
  );
  const emailError = emailText(form.formState.errors.email?.message);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("inviteTitle", { name })}
      description={t("inviteText")}
      actionLabel={t("inviteAction")}
      cancelLabel={common("cancel")}
      pending={invite.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        <Input
          label={t("email")}
          type="email"
          autoComplete="off"
          maxLength={schemas.inviteOwnerBodyEmailMax}
          hint={last ? t("emailHint") : undefined}
          error={emailError && t(emailError)}
          {...form.register("email")}
          onKeyDown={(event) => {
            // The language Combobox is a field too, so the browser does not send the form on Enter; the email field does.
            if (event.key !== "Enter") return;
            event.preventDefault();
            void submit();
          }}
        />
        <LanguageChoice
          label={t("emailLanguage")}
          value={form.watch("language") ?? Language.En}
          onChange={(language) => form.setValue("language", language)}
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
