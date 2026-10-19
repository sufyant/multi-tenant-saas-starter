"use client";

import type { InvitationPreviewResponse } from "@repo/api-client";
import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { formatRelative } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { type Inviter, inviterOf } from "./inviter";

/** The organisation, who invited and the role: the head of both accept screens. */
export function InvitationHeading({
  preview,
  withRole,
}: {
  preview: InvitationPreviewResponse;
  withRole?: boolean;
}) {
  const t = useTranslations();
  const inviter: Inviter = inviterOf(preview.inviterName);
  const values = {
    inviter:
      inviter.kind === "person"
        ? inviter.name
        : t(`invitations.inviterSubject.${inviter.kind}`),
    tenant: preview.tenantName,
    role: preview.role.builtIn ?? "other",
    roleName: preview.role.name,
  };
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="inline-flex size-10 items-center justify-center rounded-lg bg-muted text-section-title"
        >
          {preview.tenantName.charAt(0)}
        </span>
        <span className="font-medium">{preview.tenantName}</span>
      </div>
      <h1 className="text-page-title">
        {withRole
          ? t("invitations.invitedAs", values)
          : t("invitations.invited", values)}
      </h1>
    </div>
  );
}

/** The role of the invitation in the user's language: a built-in role from the messages, a custom role by its stored name. */
export function useRoleName(preview: InvitationPreviewResponse): string {
  const t = useTranslations("shell.roles");
  return preview.role.builtIn ? t(preview.role.builtIn) : preview.role.name;
}

/** A known person joins in one click (the AcceptInvitation board). */
export function JoinCard({
  preview,
  email,
  timeZone,
  onJoin,
  pending,
  error,
}: {
  preview: InvitationPreviewResponse;
  email: string;
  timeZone?: string | null;
  onJoin: () => void;
  pending: boolean;
  error?: string;
}) {
  const t = useTranslations();
  const role = useRoleName(preview);
  return (
    <AuthCard>
      <InvitationHeading preview={preview} />
      <dl className="grid grid-cols-[8rem_1fr] gap-y-3 text-body">
        <dt className="text-muted-foreground">{t("invitations.yourRole")}</dt>
        <dd>{role}</dd>
        <dt className="text-muted-foreground">{t("invitations.expires")}</dt>
        <dd>
          {formatRelative(preview.expiresAt, {
            locale: t("locale.format"),
            timeZone,
          })}
        </dd>
      </dl>
      <Button
        variant="primary"
        size="lg"
        className="w-full"
        onClick={onJoin}
        disabled={pending}
      >
        {t("invitations.join", { tenant: preview.tenantName })}
      </Button>
      {error && (
        <p role="alert" className="text-small text-destructive-text">
          {error}
        </p>
      )}
      <p className="text-small text-muted-foreground">
        {t.rich("invitations.joinsWith", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      </p>
    </AuthCard>
  );
}
