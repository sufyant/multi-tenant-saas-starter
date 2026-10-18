"use client";

import { actionFor, ProblemDetailsError } from "@repo/api-client";
import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { NoAccess } from "./no-access";

/** What a page shows when a read fails (F4): sign-in for a 401, the suspended screen, "No access yet" for 404, else the message. */
export function ProblemScreen({ error }: { error: Error }) {
  const t = useTranslations();
  const status = error instanceof ProblemDetailsError ? error.status : 500;
  const action = actionFor(
    status,
    error instanceof ProblemDetailsError ? error.problem : null,
  );
  useEffect(() => {
    if (action.kind === "sign-in") window.location.assign("/sign-in");
  }, [action.kind]);
  if (status === 404) return <NoAccess />;
  if (action.kind === "account-suspended") {
    return (
      <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
        <EmptyState
          kind="error"
          title={t("problems.suspendedTitle")}
          description={t("problems.suspendedText")}
        />
      </main>
    );
  }
  const key = action.kind === "message" ? action.key : "problems.errorText";
  return (
    <EmptyState
      kind="error"
      title={t("problems.errorTitle")}
      description={t.has(key) ? t(key) : t("problems.errorText")}
    />
  );
}
