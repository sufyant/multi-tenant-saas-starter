"use client";

import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";

/** "No access yet": a person who is not a user yet (GetMe 404), or a user who is no system admin. */
export function NoAccess() {
  const t = useTranslations();
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState
        title={t("problems.noAccessTitle")}
        description={t("access.noAccessText")}
      />
    </main>
  );
}
