"use client";

import {
  getGetMeQueryOptions,
  MyTenantResponseStatus,
  Permission,
} from "@repo/api-client";
import { CopyButton } from "@repo/ui/components/copy-button";
import { Pill, type PillTone } from "@repo/ui/components/pill";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { useOrganisationAddress } from "@/shared/organisation-address";
import { PermissionDenied } from "@/shared/permission-denied";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useRoleName } from "@/shared/use-role-name";

const tones: Record<MyTenantResponseStatus, PillTone> = {
  [MyTenantResponseStatus.Active]: "success",
  [MyTenantResponseStatus.Suspended]: "warning",
  [MyTenantResponseStatus.Closed]: "neutral",
  [MyTenantResponseStatus.Purging]: "neutral",
};

/**
 * Settings > General (7.01 to 7.05): the organisation's name, address, status, creation date, the viewer's role and the plan's
 * name (F56), from GetMyTenant. Read-only for everyone: only the platform renames an organisation or changes its plan.
 */
export function GeneralPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const { detail } = useTenant();
  const address = useOrganisationAddress(detail.slug);
  const me = useQuery(getGetMeQueryOptions());

  if (!can(Permission.TenantProfileRead))
    return (
      <Frame>
        <PermissionDenied page={t("settings.general")} />
      </Frame>
    );

  return (
    <Frame>
      <div className="flex flex-col gap-0.5">
        <h1 className="text-page-title">{t("general.title")}</h1>
        <p className="text-body text-muted-foreground">
          {t("general.description", { organisation: detail.name })}
        </p>
      </div>
      <dl className="overflow-clip rounded-lg border border-border bg-card text-body">
        <Row
          term={t("general.name")}
          value={<bdi>{detail.name}</bdi>}
          hint={t("general.nameHint")}
        />
        <Row
          term={t("general.address")}
          value={
            <span className="flex flex-wrap items-center gap-2">
              <bdi dir="ltr">{address}</bdi>
              <CopyButton value={address} label={t("general.copyAddress")} />
            </span>
          }
          hint={t("general.addressHint")}
        />
        <Row
          term={t("general.status")}
          value={
            <Pill tone={tones[detail.status]}>
              {t(`organisations.status.${detail.status}`)}
            </Pill>
          }
        />
        <Row
          term={t("general.created")}
          value={
            <Time
              at={detail.createdAt}
              show="date"
              locale={t("locale.format")}
              timeZone={me.data?.timeZone}
            />
          }
        />
        <Row term={t("general.yourRole")} value={roleName(detail.role)} />
        <Row term={t("general.plan")} value={<bdi>{detail.planName}</bdi>} />
      </dl>
    </Frame>
  );
}

/** One row of the card: the term in its 200 px column, the value, and a hint under it. On a phone the value goes under the term. */
function Row({
  term,
  value,
  hint,
}: {
  term: string;
  value: ReactNode;
  hint?: string;
}) {
  return (
    <div className="flex flex-col gap-1 border-b border-border px-5 py-3.5 last:border-b-0 tablet:flex-row tablet:items-start tablet:gap-4">
      <dt className="shrink-0 text-small font-medium text-muted-foreground tablet:w-50">
        {term}
      </dt>
      <dd className="flex min-w-0 flex-1 flex-col gap-0.5">
        {value}
        {hint && (
          <span className="text-small text-muted-foreground">{hint}</span>
        )}
      </dd>
    </div>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
