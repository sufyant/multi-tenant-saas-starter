"use client";

import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";

/**
 * "No access yet": the same text for a person who is not a user yet (GetMe 404), a user with no organisation and a slug that is
 * not in the user's list. It never says whether an invitation exists.
 */
export function NoAccess() {
  const t = useTranslations("problems");
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState title={t("noAccessTitle")} description={t("noAccessText")} />
    </main>
  );
}
