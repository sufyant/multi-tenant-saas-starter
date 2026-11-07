"use client";

import { brand } from "@repo/brand";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { supportLink } from "@repo/ui/lib/support-link";
import Link from "next/link";
import { useTranslations } from "next-intl";

/**
 * 422 `invitation.system_admin_cannot_join` (frame 1.48): a system admin never joins an organisation (T5). Sign out comes back to
 * this page, which signs the person in with another email. "Contact support" names the version; inside the sentence it is
 * underlined, so it is not told apart by colour alone (WCAG 2.2, 1.4.1).
 */
export function SystemAdminCannotJoin({
  onSwitchAccount,
}: {
  onSwitchAccount: () => void;
}) {
  const t = useTranslations();
  const { version } = useAppFrame();
  return (
    <>
      <AuthHeading
        title={t("invitations.systemAdminTitle")}
        text={t("invitations.systemAdminText")}
      />
      <Button variant="primary" className="w-full" onClick={onSwitchAccount}>
        {t("common.signOut")}
      </Button>
      <p className="text-center text-small text-muted-foreground">
        {t.rich("invitations.needHelp", {
          link: (chunks) => (
            <a
              href={supportLink(
                brand.supportUrl,
                version && t("appShell.supportBody", { version }),
              )}
              className="rounded-sm text-link underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {chunks}
            </a>
          ),
        })}
      </p>
    </>
  );
}

/** 409 `tenant.suspended` on the join (the Messages board): the invitation stays, and the person joins when the organisation is active. */
export function TenantSuspended({ onSignOut }: { onSignOut: () => void }) {
  const t = useTranslations();
  return (
    <>
      <AuthHeading
        title={t("invitations.tenantSuspendedTitle")}
        text={t("invitations.tenantSuspendedText")}
      />
      <Button
        className="w-full"
        render={<Link href="/" />}
        nativeButton={false}
      >
        {t("invitations.goToOrganisations")}
      </Button>
      <Button variant="ghost" className="self-center" onClick={onSignOut}>
        {t("common.signOut")}
      </Button>
    </>
  );
}
