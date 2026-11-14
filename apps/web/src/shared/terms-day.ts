import { formatDate } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";

/**
 * A terms version's day, from 00:00 UTC, written in the reader's language and in no time zone: the same day for everyone (the API's
 * terms flow, section 1). The gate and Home's notice write it.
 */
export function useTermsDay(): (day: string) => string {
  const t = useTranslations();
  return (day) =>
    formatDate(day, { locale: t("locale.format"), timeZone: "UTC" });
}
