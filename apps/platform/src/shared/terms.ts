"use client";

import {
  getGetTermsOfUseQueryKey,
  getGetTermsVersionQueryKey,
  getListTermsVersionPeopleInfiniteQueryKey,
  type TermsVersionResponse,
  TermsVersionResponseState,
} from "@repo/api-client";
import { formatDate } from "@repo/ui/lib/format-date-time";
import type { QueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";

// What the terms screens and the Activity sentences share about the terms versions (step 38).

/** Settings › Terms of use: the versions. */
export const termsPage = "/settings/terms";

/** New version (11.02). */
export const newTermsVersionPage = `${termsPage}/new`;

/** A version's page (11.05, 11.12, 11.15). */
export const termsVersionPage = (id: string) => `${termsPage}/${id}`;

/** The scheduled version's edit (11.10). */
export const editTermsVersionPage = (id: string) =>
  `${termsVersionPage(id)}/edit`;

/** A version's link without its scheme, as the list and the phone write it: `starter.example/terms/2026-12`. */
export const linkText = (url: string) => url.replace(/^https?:\/\//, "");

/** Today in UTC, as `2026-11-14`: a version's day starts at 00:00 UTC, so the API counts "today or later" in UTC. */
export const todayUtc = () => new Date().toISOString().slice(0, 10);

/**
 * A version's day in the language's format: `1 Dec 2026`. The day is a calendar day of UTC (the API's `effectiveDate`), so it is
 * written in UTC, never moved into the system admin's zone (F18).
 */
export function useDay() {
  const t = useTranslations();
  return (day: string) =>
    formatDate(day, { locale: t("locale.format"), timeZone: "UTC" });
}

/** The pill tone of a state (the Pill tones pattern): info scheduled, success in force, neutral replaced or cancelled. */
export const stateTone = (state: TermsVersionResponseState) =>
  state === TermsVersionResponseState.Scheduled
    ? "info"
    : state === TermsVersionResponseState.InForce
      ? "success"
      : "neutral";

/**
 * What a version asks of people, as the list says it (11.01): its acceptances against the users who may accept, a notice only, or
 * nothing for a small change.
 */
export function useAcceptanceText() {
  const t = useTranslations("terms");
  return (version: TermsVersionResponse, activeUserCount: number) => {
    if (!version.requiresAcceptance)
      return version.sendsEmail ? t("noticeOnly") : t("noNotice");
    return version.acceptedCount >= activeUserCount
      ? t("everyoneAccepted")
      : t("acceptedOf", {
          accepted: version.acceptedCount,
          total: activeUserCount,
        });
  };
}

/** After a terms command, or a refusal that says a version changed meanwhile, the screens read again (F47). */
export const refreshTerms = (queryClient: QueryClient, id?: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetTermsOfUseQueryKey() }),
    id &&
      queryClient.invalidateQueries({
        queryKey: getGetTermsVersionQueryKey(id),
      }),
    id &&
      queryClient.invalidateQueries({
        queryKey: getListTermsVersionPeopleInfiniteQueryKey(id),
      }),
  ]);
