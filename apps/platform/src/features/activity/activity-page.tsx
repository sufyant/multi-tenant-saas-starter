"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { Freshness } from "@repo/ui/components/freshness";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import {
  PlatformActivity,
  usePlatformActivity,
} from "@/shared/platform-activity";

/** Activity (the Activity board, P6): the whole platform log, with the time it was read. */
export function ActivityPage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  const activity = usePlatformActivity();
  const { dataUpdatedAt } = activity.log;
  return (
    <section className="flex min-w-0 max-w-[960px] flex-col gap-4">
      <div>
        <h1 className="text-page-title">{t("activity.title")}</h1>
        {dataUpdatedAt > 0 && (
          <Freshness
            at={new Date(dataUpdatedAt)}
            locale={t("locale.format")}
            timeZone={me.data?.timeZone}
            label={t("common.updated")}
          />
        )}
      </div>
      <PlatformActivity
        activity={activity}
        emptyText={t("activity.emptyText")}
      />
    </section>
  );
}
