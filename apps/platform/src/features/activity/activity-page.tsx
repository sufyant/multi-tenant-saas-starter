"use client";

import { useTranslations } from "next-intl";
import {
  PlatformActivity,
  usePlatformActivity,
} from "@/shared/platform-activity";

/**
 * Activity log (7.01 to 7.08): the whole platform log under the frame's description and no Freshness (F59). On a phone the top bar
 * names the page, so its title and description stay for screen readers (7.08).
 */
export function ActivityPage() {
  const t = useTranslations();
  const activity = usePlatformActivity();
  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="sr-only flex flex-col gap-0.5 tablet:not-sr-only">
        <h1 className="text-page-title">{t("shell.activityLog")}</h1>
        <p className="text-body text-muted-foreground">
          {t("activity.description")}
        </p>
      </div>
      <PlatformActivity
        activity={activity}
        emptyText={t("activity.emptyText")}
      />
    </section>
  );
}
