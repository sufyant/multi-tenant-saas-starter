"use client";

import {
  type InvitationPreviewResponse,
  InvitationResponseState,
} from "@repo/api-client";
import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { formatDate } from "@repo/ui/lib/format-date-time";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { inviterOf } from "./inviter";

type Ended =
  | Exclude<InvitationResponseState, typeof InvitationResponseState.Pending>
  | "notFound";

/**
 * An invitation that cannot be accepted (frame 1.16): expired, withdrawn, already accepted, or a link that does not work. An
 * expired one names its end date in the person's time zone (F18), in place of the frame's "7 days", and who can invite again; a
 * withdrawn one has its own text (the roadmap, 37a-frontend-1).
 */
export function InvitationEnded({
  preview,
  timeZone,
  onSignOut,
}: {
  /** Absent for a link that does not work (404). */
  preview?: InvitationPreviewResponse & { state: Ended };
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
  const state: Ended = preview?.state ?? "notFound";
  const accepted = state === InvitationResponseState.Accepted;
  return (
    <>
      <AuthHeading
        title={t(`invitations.${state}Title`)}
        text={t(`invitations.${state}Text`, values)}
      />
      <Button
        variant={accepted ? "primary" : "secondary"}
        className="w-full"
        render={<Link href="/" />}
        nativeButton={false}
      >
        {accepted
          ? t("invitations.open", values)
          : t("invitations.goToOrganisations")}
      </Button>
      <Button variant="ghost" className="self-center" onClick={onSignOut}>
        {t("common.signOut")}
      </Button>
    </>
  );
}
