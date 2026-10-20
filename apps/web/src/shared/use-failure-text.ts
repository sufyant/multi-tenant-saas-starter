"use client";

import { formatUtc } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { problemOf, referenceOf } from "./problem";

/** The text of a failure the dialog has no text of its own for: the message of the API's code (F4), and its reference. */
export function useFailureText() {
  const t = useTranslations();
  return (error: unknown): string => {
    const problem = problemOf(error);
    if (!problem) return t("errors.server.error");
    const reference = t("common.reference", {
      id: referenceOf(problem.traceId),
      time: formatUtc(new Date(), { locale: t("locale.format") }),
    });
    return `${t(`errors.${problem.code}`)} ${reference}`;
  };
}
