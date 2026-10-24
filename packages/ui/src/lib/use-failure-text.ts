"use client";

import type { FailedCall } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { formatUtc } from "./format-date-time";

/** What a screen shows of a failed call: its code and its reference. The app reads it with `problemOf` of `@repo/api-client`. */
export type Problem = Pick<FailedCall, "code" | "reference"> | null;

/**
 * The text of a failure the dialog has no text of its own for: the message of the API's code (F4), and its reference. Without a
 * problem (a dropped connection, an answer that is no Problem Details) it is the message of a server error.
 */
export function useFailureText() {
  const t = useTranslations();
  return (problem: Problem): string => {
    if (!problem) return t("errors.server.error");
    const reference = t("common.reference", {
      id: problem.reference,
      time: formatUtc(new Date(), { locale: t("locale.format") }),
    });
    return `${t(`errors.${problem.code}`)} ${reference}`;
  };
}
