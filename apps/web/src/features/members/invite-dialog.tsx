"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  ErrorCode,
  getGetMeQueryOptions,
  inviteMember,
  Language,
  MemberRole,
  ProblemDetailsError,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { directionOf } from "@repo/ui/lib/locale";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useTenant } from "@/shared/tenant-context";
import { daysLeft } from "./invitation-status";
import { problemOf } from "./problem";
import { afterRefusal, refreshInvitations } from "./refresh";
import { rolesFor } from "./role-choice";
import { useFailureText } from "./use-failure-text";
import { useIntentKey } from "./use-intent-key";

type Invitation = z.infer<typeof schemas.InviteMemberBody>;

/**
 * Invite a member (the Invitations board): Email, Role, Email language, with the generated schema of InviteMember (F11). Owner
 * is in the list only for an Owner (K1). The language starts as the viewer's own (GetMe). One Idempotency-Key per intent (F48):
 * a new key when the dialog opens and when the email, the role or the language changes; the same key for a retry.
 */
export function InviteDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations();
  const locale = useLocale();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant, detail } = useTenant();
  const me = useQuery(getGetMeQueryOptions());
  const { key, renew } = useIntentKey();
  const viewerIsOwner = detail.role.builtIn === MemberRole.Owner;
  const roles = rolesFor(viewerIsOwner);
  const languages = Object.values(Language);
  const form = useForm<Invitation>({
    resolver: zodResolver(schemas.InviteMemberBody),
    defaultValues: {
      email: "",
      role: MemberRole.Member,
      language:
        me.data?.language ??
        languages.find((language) => language === locale) ??
        Language.En,
    },
  });
  // Another email, role or language is another intent: it gets a new key.
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
    }) => inviteMember(tenant.id, invitation, { "Idempotency-Key": key }),
    onSuccess: () => refreshInvitations(queryClient, tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      // The API saved this answer under the key (I6): sending the same intent later needs a new key to run again. A request
      // still running (409 idempotency.key_in_progress) and a server failure (5xx, nothing saved) keep the key.
      if (
        error.status < 500 &&
        error.problem?.code !== ErrorCode.IdempotencyKeyInProgress
      )
        renew();
      const action = actionFor(error.status, error.problem);
      if (action.kind === "field-errors")
        for (const [field, [code]] of Object.entries(action.fields))
          if (field === "email" && code)
            form.setError("email", { message: `validation.${code}` });
      if (error.problem?.code === ErrorCode.MemberExists)
        form.setError("email", {
          message: t("members.alreadyMember", {
            email: form.getValues("email"),
            organisation: tenant.name,
          }),
        });
    },
  });

  const banner = (() => {
    if (!invite.error) return null;
    const code = problemOf(invite.error)?.code;
    if (code === ErrorCode.MemberExists) return null;
    if (code === ErrorCode.RequestValidationFailed) return null;
    if (code === ErrorCode.InvitationDailyLimitReached)
      return {
        title: t("members.dailyLimit"),
        text: t("members.dailyLimitText", { organisation: tenant.name }),
      };
    if (code === ErrorCode.InvitationOwnerRequiresOwner)
      return {
        title: t("members.inviteOwner"),
        text: t("members.inviteOwnerText"),
      };
    return { title: t("members.notSent"), text: failureText(invite.error) };
  })();

  const emailError = form.formState.errors.email?.message;
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
              title: t("members.sent"),
              description: t("members.sentText", {
                email: sent.email,
                days: daysLeft(sent.expiresAt, new Date()),
              }),
            });
          },
        },
      ),
  );

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("members.inviteTitle")}
      description={t("members.inviteText")}
      actionLabel={t("members.send")}
      cancelLabel={t("common.cancel")}
      pending={invite.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        <Input
          label={t("members.email")}
          type="email"
          autoComplete="off"
          placeholder={t("members.inviteEmailPlaceholder")}
          maxLength={schemas.inviteMemberBodyEmailMax}
          error={
            emailError?.startsWith("validation.")
              ? t(emailError as "validation.required")
              : emailError
          }
          {...form.register("email")}
          onKeyDown={(event) => {
            // The Comboboxes are fields too, so the browser does not send the form on Enter; the email field does.
            if (event.key !== "Enter") return;
            event.preventDefault();
            void submit();
          }}
        />
        <Combobox
          label={t("members.role")}
          value={form.watch("role")}
          options={roles.map((role) => ({
            id: role,
            label: t(`shell.roles.${role}`),
          }))}
          onValueChange={(id) => {
            const role = roles.find((choice) => choice === id);
            if (role) form.setValue("role", role);
          }}
          hint={
            viewerIsOwner
              ? t("members.inviteRoleHintOwner")
              : t("members.inviteRoleHintAdmin")
          }
          searchPlaceholder={t("members.roleSearch")}
          emptyLabel={t("members.roleEmpty")}
        />
        <Combobox
          label={t("members.inviteLanguage")}
          value={form.watch("language") ?? undefined}
          options={languages.map((language) => ({
            id: language,
            label: t(`languages.${language}`),
            lang: language,
            dir: directionOf(language),
          }))}
          onValueChange={(id) => {
            const language = languages.find((choice) => choice === id);
            if (language) form.setValue("language", language);
          }}
          hint={t("members.inviteLanguageHint")}
          searchPlaceholder={t("members.languageSearch")}
          emptyLabel={t("members.languageEmpty")}
        />
        {banner && (
          <Banner tone="destructive" title={banner.title}>
            {banner.text}
          </Banner>
        )}
      </form>
    </Dialog>
  );
}
