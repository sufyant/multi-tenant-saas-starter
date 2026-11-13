"use client";

import { getGetPlatformAuditRetentionQueryOptions } from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEntitlementText } from "@/shared/entitlements";
import {
  PlatformActivity,
  usePlatformActivity,
} from "@/shared/platform-activity";

/**
 * Activity log (7.01 to 7.08): the whole platform log under the frame's description and no Freshness (F59), and how long the log keeps
 * its entries, the deployment's setting. On a phone the top bar names the page, so its title and description stay for screen readers,
 * and the period is the first line of the body (7.08). The period shows once it is read; the entries never wait for it.
 */
export function ActivityPage() {
  const t = useTranslations();
  const text = useEntitlementText();
  const activity = usePlatformActivity();
  const period = useQuery(getGetPlatformAuditRetentionQueryOptions());
  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="flex flex-col gap-0.5">
        <h1 className="sr-only tablet:not-sr-only text-page-title">
          {t("shell.activityLog")}
        </h1>
        <p className="sr-only tablet:not-sr-only text-body text-muted-foreground">
          {t("activity.description")}
        </p>
        {period.data && (
          <p className="text-body text-muted-foreground">
            {t("activity.period", {
              days: text.days(period.data.days),
              daysCount: period.data.days,
            })}
          </p>
        )}
      </div>
      <PlatformActivity
        activity={activity}
        emptyText={t("activity.emptyText")}
      />
    </section>
  );
}
