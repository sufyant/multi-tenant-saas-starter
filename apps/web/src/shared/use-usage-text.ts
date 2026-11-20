"use client";

import {
  MeterSettingResponseOverage,
  type UsageResponse,
} from "@repo/api-client";
import { usageFormat } from "@repo/ui/lib/usage-text";
import { useTranslations } from "next-intl";

/**
 * The Usage screens' values as text: `usageFormat` of `packages/ui` (a count, an amount, the days of the month in UTC), a meter's name,
 * and the plan's terms.
 */
export function useUsageText(usage: UsageResponse) {
  const t = useTranslations();
  const format = usageFormat(usage, t("locale.format"));
  const { money, count } = format;
  // The product's own word for a meter; the template ships none, so a meter reads as its key, as in the emails (the API's E10).
  const meterName = (key: string) => {
    const message = `usage.meters.${key}` as Parameters<typeof t>[0];
    return t.has(message) ? t(message) : key;
  };
  const planText = usage.meters
    .map((meter) =>
      meter.overage === MeterSettingResponseOverage.Stop
        ? t("usage.stopText", {
            plan: usage.planName,
            quota: count(meter.quota),
            meter: meterName(meter.key),
          })
        : t("usage.chargeText", {
            plan: usage.planName,
            quota: count(meter.quota),
            meter: meterName(meter.key),
            price: money(meter.unitPrice),
            size: count(meter.unitSize),
          }),
    )
    .join(" ");
  return { ...format, meterName, planText };
}

export type UsageText = ReturnType<typeof useUsageText>;
