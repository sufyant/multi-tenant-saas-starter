"use client";

import {
  getGetMeQueryOptions,
  getGetMyTenantQueryOptions,
} from "@repo/api-client";
import { Freshness } from "@repo/ui/components/freshness";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { DetailList } from "@/shared/detail-list";
import { useTenant } from "@/shared/tenant-context";

/** The Overview of an organisation (the Overview board): a quiet start page with the organisation and the role, until the product adds its own work. */
export function OverviewPage() {
  const t = useTranslations();
  const { tenant, detail } = useTenant();
  // The tenant layout read GetMyTenant; this reads its cache, for when it was loaded.
  const read = useQuery(getGetMyTenantQueryOptions(tenant.id));
  const me = useQuery(getGetMeQueryOptions());
  return (
    <div className="flex max-w-[1120px] flex-col gap-5">
      <div>
        <h1 className="text-page-title">{t("overview.title")}</h1>
        {read.dataUpdatedAt > 0 && (
          <Freshness
            at={new Date(read.dataUpdatedAt)}
            locale={t("locale.format")}
            timeZone={me.data?.timeZone}
            label={t("common.updated")}
          />
        )}
      </div>
      <DetailList
        rows={[
          {
            term: t("overview.organisation"),
            value: (
              <>
                {detail.name}{" "}
                <bdi
                  dir="ltr"
                  className="font-mono text-mono text-muted-foreground"
                >
                  {detail.slug}
                </bdi>
              </>
            ),
          },
          {
            term: t("general.yourRole"),
            value: detail.role.builtIn
              ? t(`shell.roles.${detail.role.builtIn}`)
              : detail.role.name,
          },
        ]}
      />
      <p className="max-w-[640px] text-body text-muted-foreground">
        {t("overview.text")}
      </p>
    </div>
  );
}
