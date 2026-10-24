"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { formatUtc } from "../../lib/format-date-time";
import type { Problem } from "../../lib/use-failure-text";
import { Button } from "../button";
import { EmptyState } from "../empty-state";

export type ListErrorProps = {
  title: string;
  /** The failed read, from `problemOf` of `@repo/api-client`: null when the answer was no Problem Details. */
  problem: Problem;
  /** When the error came: `errorUpdatedAt` of the query. */
  at: number;
  onRetry: () => void;
  /** What the app shows in place of the list when the answer leaves it: sign-in, or the account's page (F4). */
  leave?: ReactNode;
};

/**
 * A list that could not load (the ListStates board): what failed, the message of the API's code (F4), the reference that helps
 * support find the request, and Try again.
 */
export function ListError({
  title,
  problem,
  at,
  onRetry,
  leave,
}: ListErrorProps) {
  const t = useTranslations();
  if (leave) return leave;
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
              id: problem.reference,
              time: formatUtc(new Date(at), { locale: t("locale.format") }),
            })
          : undefined
      }
      action={<Button onClick={onRetry}>{t("common.tryAgain")}</Button>}
    />
  );
}
