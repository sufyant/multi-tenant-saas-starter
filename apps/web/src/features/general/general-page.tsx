"use client";

import {
  getGetMeQueryOptions,
  MyTenantResponseStatus,
  Permission,
} from "@repo/api-client";
import { Status, type StatusTone } from "@repo/ui/components/status";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { DetailList } from "@/shared/detail-list";
import { PermissionDenied } from "@/shared/permission-denied";
import { SettingsNav } from "@/shared/settings-nav";
import { useCan, useTenant } from "@/shared/tenant-context";

const tones: Record<MyTenantResponseStatus, StatusTone> = {
  [MyTenantResponseStatus.Active]: "success",
  [MyTenantResponseStatus.Suspended]: "warning",
  [MyTenantResponseStatus.Closed]: "neutral",
};

/**
 * Settings > General (the Settings board): the organisation's name, status, creation date and the viewer's role, from GetMyTenant.
 * Read-only for everyone: only the platform renames an organisation.
 */
export function GeneralPage() {
  const t = useTranslations();
  const can = useCan();
  const { detail } = useTenant();
  const me = useQuery(getGetMeQueryOptions());

  if (!can(Permission.TenantProfileRead))
    return (
      <Frame>
        <PermissionDenied page={t("settings.general")} />
      </Frame>
    );

  return (
    <Frame>
      <h1 className="text-page-title">{t("general.title")}</h1>
      <DetailList
        rows={[
          { term: t("general.name"), value: detail.name },
          {
            term: t("general.status"),
            value: (
              <Status tone={tones[detail.status]}>
                {t(`organisations.status.${detail.status}`)}
              </Status>
            ),
          },
          {
            term: t("general.created"),
            value: (
              <Time
                at={detail.createdAt}
                show="date"
                locale={t("locale.format")}
                timeZone={me.data?.timeZone}
              />
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
    </Frame>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="general" />
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        {children}
      </section>
    </div>
  );
}
