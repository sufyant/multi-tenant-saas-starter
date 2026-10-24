"use client";

import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Status } from "@repo/ui/components/status";
import Link from "next/link";
import { useTranslations } from "next-intl";

/**
 * 422 `invitation.system_admin_cannot_join` (the AcceptSystemAdmin board): a system admin never joins an organisation (T5). Switch
 * account signs out and comes back to this page through sign-in.
 */
export function SystemAdminCannotJoin({
  onSwitchAccount,
  onSignOut,
}: {
  onSwitchAccount: () => void;
  onSignOut: () => void;
}) {
  const t = useTranslations();
  return (
    <AuthCard>
      <Status tone="neutral">{t("invitations.systemAdminStatus")}</Status>
      <h1 className="text-page-title">{t("invitations.systemAdminTitle")}</h1>
      <p className="text-body text-muted-foreground">
        {t("invitations.systemAdminText")}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" onClick={onSwitchAccount}>
          {t("invitations.switchAccount")}
        </Button>
        <Button variant="ghost" onClick={onSignOut}>
          {t("common.signOut")}
        </Button>
      </div>
    </AuthCard>
  );
}

/** 409 `tenant.suspended` on the join (the Messages board): the invitation stays, and the person joins when the organisation is active. */
export function TenantSuspended({ onSignOut }: { onSignOut: () => void }) {
  const t = useTranslations();
  return (
    <AuthCard>
      <Status tone="warning">{t("organisations.status.suspended")}</Status>
      <h1 className="text-page-title">
        {t("invitations.tenantSuspendedTitle")}
      </h1>
      <p className="text-body text-muted-foreground">
        {t("invitations.tenantSuspendedText")}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="primary"
          render={<Link href="/" />}
          nativeButton={false}
        >
          {t("invitations.goToOrganisations")}
        </Button>
        <Button variant="ghost" onClick={onSignOut}>
          {t("common.signOut")}
        </Button>
      </div>
    </AuthCard>
  );
}
