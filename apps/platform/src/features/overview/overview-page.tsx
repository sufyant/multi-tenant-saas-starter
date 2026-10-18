"use client";

import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";

/** The platform's Overview: a placeholder that names the sections to come: Organisations, Users and Activity. */
export function OverviewPage() {
  const t = useTranslations("overview");
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-page-title">{t("title")}</h1>
      <EmptyState title={t("emptyTitle")} description={t("emptyText")} />
    </div>
  );
}
