"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { formatDate, type Instant } from "@repo/ui/lib/format-date-time";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";

/** A date inside a sentence, in the language's format and the system admin's time zone (F18). Only the formatter formats. */
export function useDates() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  const options = { locale: t("locale.format"), timeZone: me.data?.timeZone };
  return {
    timeZone: me.data?.timeZone,
    date: (at: Instant) => formatDate(at, options),
  };
}
