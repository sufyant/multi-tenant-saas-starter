"use client";

import { actionFor, ProblemDetailsError, problemOf } from "@repo/api-client";
import { EmptyState } from "@repo/ui/components/empty-state";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { useReferenceText } from "@repo/ui/lib/use-failure-text";
import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useState } from "react";
import { AccountBlocked } from "./account-blocked";
import { NoAccess } from "./no-access";
import { accountBlockOf } from "./problem";

/**
 * What a page shows when a read fails (F4): sign-in for a 401, the account's page for a suspended or deleted user, "No access yet"
 * for 404, else the message and, for Problem Details, the reference that support searches for, as a list error has it.
 */
export function ProblemScreen({ error }: { error: Error }) {
  const t = useTranslations();
  const referenceText = useReferenceText();
  const [at] = useState(() => new Date());
  const status = error instanceof ProblemDetailsError ? error.status : 500;
  const action = actionFor(
    status,
    error instanceof ProblemDetailsError ? error.problem : null,
  );
  useEffect(() => {
    if (action.kind === "sign-in")
      window.location.assign(signInAgainUrl(window.location));
  }, [action.kind]);
  if (status === 404) return <NoAccess />;
  const block = accountBlockOf(error);
  if (block) return <AccountBlocked block={block} />;
  const key = action.kind === "message" ? action.key : "problems.errorText";
  return (
    <EmptyState
      kind="error"
      title={t("problems.errorTitle")}
      description={t.has(key) ? t(key) : t("problems.errorText")}
      code={referenceText(problemOf(error), at)}
    />
  );
}

/**
 * What a list shows in place of its error when the answer leaves the list (`ListError`'s `leave`): a 401 goes to sign-in and a
 * suspended user to their screen (F4). Any other answer stays in the list.
 */
export function leaveOf(error: Error): ReactNode {
  const kind =
    error instanceof ProblemDetailsError
      ? actionFor(error.status, error.problem).kind
      : null;
  return kind === "sign-in" || kind === "account-suspended" ? (
    <ProblemScreen error={error} />
  ) : null;
}
