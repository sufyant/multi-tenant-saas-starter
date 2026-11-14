"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  type InvitationPreviewResponse,
  schemas,
  type TermsInForceResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Input } from "@repo/ui/components/input";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { InvitationHeading } from "./join-card";

// The generated schema of AcceptInvitation (F11). A person who is not a user yet must give their name: 1 to 100 characters,
// trimmed, as the API counts it (A7). The generated schema has it optional, because a known user sends none.
const names = schemas.AcceptInvitationBody.pick({ name: true }).extend({
  name: z.string().trim().min(1).max(schemas.acceptInvitationBodyNameMax),
});

export type Names = z.infer<typeof names>;

/** What a new person sends with the invitation: their name, and the terms version in force they accepted, or null without one. */
export type NewPerson = Names & { termsVersionId: string | null };

/**
 * A new person types their name before they join (the AcceptNewPerson board, in frame 1.13's card): the identity provider keeps
 * none (A7). With a terms version in force, they read it and tick the box, which enables Accept (frame 1.15); the API records the
 * acceptance with the new user. The box covers the version in force only: a scheduled version that requires acceptance asks at the
 * gate right after.
 */
export function NewPersonForm({
  preview,
  email,
  terms,
  onJoin,
  pending,
  error,
  fieldErrors,
}: {
  preview: InvitationPreviewResponse;
  email: string;
  /** The terms version in force, or undefined before the first version. */
  terms?: TermsInForceResponse;
  onJoin: (person: NewPerson) => void;
  pending: boolean;
  error?: string;
  /** The API's field errors, as message keys, keyed by the field's JSON name (F4). */
  fieldErrors?: Partial<Record<keyof Names, string>>;
}) {
  const t = useTranslations();
  const agreeId = useId();
  const [agreed, setAgreed] = useState(false);
  const form = useForm<Names>({
    resolver: zodResolver(names),
    defaultValues: { name: "" },
  });
  const message = (field: keyof Names) => {
    const key = form.formState.errors[field]?.message ?? fieldErrors?.[field];
    return key && t(key as "validation.required");
  };
  return (
    <>
      <InvitationHeading preview={preview} />
      <p className="text-body text-muted-foreground">
        {t("invitations.newPersonText")}
      </p>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) =>
          onJoin({ ...values, termsVersionId: terms?.id ?? null }),
        )}
        className="flex flex-col gap-3"
      >
        <Input
          label={t("invitations.name")}
          autoComplete="name"
          error={message("name")}
          {...form.register("name")}
        />
        {terms && (
          <>
            <Button
              variant="ghost"
              className="self-center"
              render={<a href={terms.url} target="_blank" rel="noreferrer" />}
              nativeButton={false}
            >
              {t("terms.read")}
            </Button>
            <div className="flex items-start justify-center gap-2 text-body">
              <Checkbox
                id={agreeId}
                checked={agreed}
                onCheckedChange={setAgreed}
                className="mt-0.5"
              />
              <label htmlFor={agreeId}>{t("terms.agree")}</label>
            </div>
          </>
        )}
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={pending || (terms !== undefined && !agreed)}
        >
          {t("invitations.accept")}
        </Button>
        {error && (
          <p role="alert" className="text-small text-destructive-text">
            {error}
          </p>
        )}
      </form>
      <p className="text-center text-small text-muted-foreground">
        {t.rich("invitations.joinsWith", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      </p>
    </>
  );
}
