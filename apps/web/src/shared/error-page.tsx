"use client";

import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { reportBrowserError } from "@/error-tracking/report-error";

/** The body of every route group's error.tsx (F27): what happened, that data is safe, and a retry. */
export function ErrorPage({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  const t = useTranslations();
  useEffect(() => {
    console.error(error);
    reportBrowserError(error);
  }, [error]);
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState
        kind="error"
        title={t("problems.errorTitle")}
        description={t("problems.errorText")}
        code={error.digest}
        action={<Button onClick={() => retry()}>{t("common.tryAgain")}</Button>}
      />
    </main>
  );
}
