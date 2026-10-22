"use client";

import { actionFor, ProblemDetailsError } from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { formatUtc } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { problemOf, referenceOf } from "./problem";
import { ProblemScreen } from "./problem-screen";

/**
 * A list that could not load (the ListStates board): what failed, the message of the API's code (F4), the reference that helps
 * support find the request, and Try again.
 */
export function ListError({
  title,
  error,
  at,
  onRetry,
}: {
  title: string;
  error: Error;
  /** When the error came: `errorUpdatedAt` of the query. */
  at: number;
  onRetry: () => void;
}) {
  const t = useTranslations();
  const problem = problemOf(error);
  // A 401 and a suspended user leave the list: sign-in, or the suspended screen (F4).
  const action =
    error instanceof ProblemDetailsError
      ? actionFor(error.status, error.problem).kind
      : null;
  if (action === "sign-in" || action === "account-suspended")
    return <ProblemScreen error={error} />;
  return (
    <EmptyState
      kind="error"
      title={title}
      description={
        problem ? t(`errors.${problem.code}`) : t("problems.errorText")
      }
      code={
        problem
          ? t("common.reference", {
              id: referenceOf(problem.traceId),
              time: formatUtc(new Date(at), { locale: t("locale.format") }),
            })
          : undefined
      }
      action={<Button onClick={onRetry}>{t("common.tryAgain")}</Button>}
    />
  );
}
