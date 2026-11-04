"use client";

import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { SearchXIcon } from "@repo/ui/components/icon";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useTenant } from "./tenant-context";
import { useRoleName } from "./use-role-name";

/**
 * Permission denied, inside the shell (the PermissionDenied board, 2.15): the search-x tile, the page and the person's role, who
 * can help, and the way back to Home. The API still refuses the page's calls (F5).
 */
export function PermissionDenied({ page }: { page: string }) {
  const t = useTranslations();
  const roleName = useRoleName();
  const { tenant, detail } = useTenant();
  const role = roleName(detail.role);
  return (
    <EmptyState
      kind="error"
      size="page"
      icon={SearchXIcon}
      title={t("permissionDenied.title", { page })}
      description={t("permissionDenied.text", {
        organisation: tenant.name,
        role,
        page,
      })}
      action={
        <Button render={<Link href={`/${tenant.slug}`} />} nativeButton={false}>
          {t("permissionDenied.back")}
        </Button>
      }
    />
  );
}
