"use client";

import {
  type InvitationPreviewResponse,
  InvitationResponseState,
} from "@repo/api-client";
import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Status } from "@repo/ui/components/status";
import { formatDate } from "@repo/ui/lib/format-date-time";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { inviterOf } from "./inviter";

type Ended = "expired" | "cancelled" | "accepted" | "notFound";

/**
 * An invitation that cannot be accepted (the AcceptEnded board): expired, withdrawn, already accepted, or a link that does not
 * work. An unknown link says only that; the others name the organisation and who can invite again.
 */
export function InvitationEnded({
  preview,
  timeZone,
  onSignOut,
}: {
  /** Absent for a link that does not work (404). */
  preview?: InvitationPreviewResponse;
  timeZone?: string | null;
  onSignOut: () => void;
}) {
  const t = useTranslations();
  const inviter = preview && inviterOf(preview.inviterName);
  const who =
    inviter &&
    (inviter.kind === "person"
      ? inviter.name
      : t(`invitations.inviterObject.${inviter.kind}`));
  const values = {
    tenant: preview?.tenantName ?? "",
    inviter: who ?? "",
    date: preview
      ? formatDate(preview.expiresAt, { locale: t("locale.format"), timeZone })
      : "",
  };
  // The page shows a pending invitation elsewhere, so the state here has ended.
  const state = (preview?.state ?? "notFound") as Ended;
  const tone =
    state === InvitationResponseState.Accepted ? "success" : "neutral";
  const accepted = state === InvitationResponseState.Accepted;
  return (
    <AuthCard>
      <Status tone={tone}>{t(`invitations.${state}Status`)}</Status>
      <h1 className="text-page-title">{t(`invitations.${state}Title`)}</h1>
      <p className="text-body text-muted-foreground">
        {t(`invitations.${state}Text`, values)}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="primary"
          render={<Link href="/" />}
          nativeButton={false}
        >
          {accepted
            ? t("invitations.open", values)
            : t("invitations.goToOrganisations")}
        </Button>
        <Button variant="ghost" onClick={onSignOut}>
          {t("common.signOut")}
        </Button>
      </div>
    </AuthCard>
  );
}
