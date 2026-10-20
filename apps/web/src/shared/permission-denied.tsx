"use client";

import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useTenant } from "./tenant-context";

/**
 * Permission denied, inside the shell (the PermissionDenied board): it names the page and the person's role, says who can help,
 * and offers the way back to Overview. The API still refuses the page's calls (F5).
 */
export function PermissionDenied({ page }: { page: string }) {
  const t = useTranslations();
  const { tenant, detail } = useTenant();
  const role = detail.role.builtIn
    ? t(`shell.roles.${detail.role.builtIn}`)
    : detail.role.name;
  return (
    <EmptyState
      kind="error"
      title={t("permissionDenied.title", { page })}
      description={t("permissionDenied.text", {
        organisation: tenant.name,
        role,
      })}
      action={
        <Button render={<Link href={`/${tenant.slug}`} />} nativeButton={false}>
          {t("permissionDenied.back")}
        </Button>
      }
    />
  );
}
