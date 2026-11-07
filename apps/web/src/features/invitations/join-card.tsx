"use client";

import type { InvitationPreviewResponse } from "@repo/api-client";
import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { formatRelative } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { roleSelect } from "@/shared/use-role-name";
import { type Inviter, inviterOf } from "./inviter";

/** The head of both accept screens (frame 1.13): "Join <organisation>", and who invited the person with which role. */
export function InvitationHeading({
  preview,
}: {
  preview: InvitationPreviewResponse;
}) {
  const t = useTranslations();
  const inviter: Inviter = inviterOf(preview.inviterName);
  const values = {
    inviter:
      inviter.kind === "person"
        ? inviter.name
        : t(`invitations.inviterSubject.${inviter.kind}`),
    tenant: preview.tenantName,
    role: preview.role ? roleSelect(preview.role) : "other",
    roleName: preview.role?.name ?? "",
  };
  return (
    <AuthHeading
      title={t("invitations.join", { tenant: preview.tenantName })}
      text={
        preview.role
          ? t("invitations.invitedAs", values)
          : t("invitations.invited", values)
      }
    />
  );
}

/** A known person joins in one click (frame 1.13). The expiry is relative to the preview's `expiresAt` (F44). */
export function JoinCard({
  preview,
  timeZone,
  onJoin,
  pending,
  error,
}: {
  preview: InvitationPreviewResponse;
  timeZone?: string | null;
  onJoin: () => void;
  pending: boolean;
  error?: string;
}) {
  const t = useTranslations();
  return (
    <>
      <InvitationHeading preview={preview} />
      <p className="text-center text-small text-muted-foreground">
        {t("invitations.expires", {
          when: formatRelative(preview.expiresAt, {
            locale: t("locale.format"),
            timeZone,
          }),
        })}
      </p>
      <Button
        variant="primary"
        className="w-full"
        onClick={onJoin}
        disabled={pending}
      >
        {t("invitations.accept")}
      </Button>
      {error && (
        <p role="alert" className="text-small text-destructive-text">
          {error}
        </p>
      )}
    </>
  );
}
