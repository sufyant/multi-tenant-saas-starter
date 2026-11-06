"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { type InvitationPreviewResponse, schemas } from "@repo/api-client";
import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Input } from "@repo/ui/components/input";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { InvitationHeading } from "./join-card";

// The generated schema of AcceptInvitation (F11). A person who is not a user yet must give their name: 1 to 100 characters,
// trimmed, as the API counts it (A7). The generated schema has it optional, because a known user sends none.
const names = schemas.AcceptInvitationBody.pick({ name: true }).extend({
  name: z.string().trim().min(1).max(schemas.acceptInvitationBodyNameMax),
});

export type Names = z.infer<typeof names>;

/** A new person types their name before they join (the AcceptNewPerson board): the identity provider keeps none (A7). */
export function NewPersonForm({
  preview,
  email,
  onJoin,
  pending,
  error,
  fieldErrors,
}: {
  preview: InvitationPreviewResponse;
  email: string;
  onJoin: (names: Names) => void;
  pending: boolean;
  error?: string;
  /** The API's field errors, as message keys, keyed by the field's JSON name (F4). */
  fieldErrors?: Partial<Record<keyof Names, string>>;
}) {
  const t = useTranslations();
  const form = useForm<Names>({
    resolver: zodResolver(names),
    defaultValues: { name: "" },
  });
  const message = (field: keyof Names) => {
    const key = form.formState.errors[field]?.message ?? fieldErrors?.[field];
    return key && t(key as "validation.required");
  };
  return (
    <AuthCard>
      <InvitationHeading preview={preview} withRole />
      <p className="text-body text-muted-foreground">
        {t("invitations.newPersonText")}
      </p>
      <form
        noValidate
        onSubmit={form.handleSubmit(onJoin)}
        className="flex flex-col gap-3"
      >
        <Input
          label={t("invitations.name")}
          autoComplete="name"
          error={message("name")}
          {...form.register("name")}
        />
        <Button
          type="submit"
          variant="primary"
          size="lg"
          className="w-full"
          disabled={pending}
        >
          {t("invitations.join", { tenant: preview.tenantName })}
        </Button>
        {error && (
          <p role="alert" className="text-small text-destructive-text">
            {error}
          </p>
        )}
      </form>
      <p className="text-small text-muted-foreground">
        {t.rich("invitations.joinsWith", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      </p>
    </AuthCard>
  );
}
