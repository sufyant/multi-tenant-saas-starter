"use client";

import { getGetMeQueryOptions, Permission } from "@repo/api-client";
import {
  ActivityIcon,
  ChevronRightIcon,
  Icon,
  type LucideIcon,
  SettingsIcon,
  ShieldIcon,
  UsersIcon,
} from "@repo/ui/components/icon";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useTenant } from "@/shared/tenant-context";

/** The pages Home offers, each with its read permission (F5). */
const pages = [
  ["members", "members", "members", UsersIcon, Permission.TenantMembersRead],
  ["roles", "roles", "roles", ShieldIcon, Permission.TenantRolesRead],
  [
    "activity",
    "activityLog",
    "activity",
    ActivityIcon,
    Permission.TenantAuditRead,
  ],
  [
    "general",
    "settings",
    "settings",
    SettingsIcon,
    Permission.TenantProfileRead,
  ],
] as const satisfies readonly (readonly [
  string,
  string,
  string,
  LucideIcon,
  Permission,
])[];

/**
 * An organisation's Home (the Home screens): "Welcome" with the person's first name, the organisation's name, and a card for each
 * page the person can open. The product's own work comes after it in the menu (`productSections`).
 */
export function HomePage() {
  const t = useTranslations();
  const { detail } = useTenant();
  const me = useQuery(getGetMeQueryOptions());
  const firstName = me.data?.firstName;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-0.5">
        <h1 className="text-page-title">
          {firstName
            ? t("home.welcome", { name: firstName })
            : t("home.welcomeNoName")}
        </h1>
        <bdi className="text-body text-muted-foreground">{detail.name}</bdi>
      </div>
      <ul className="grid max-w-190 gap-4 tablet:grid-cols-2">
        {pages
          .filter(([, , , , permission]) =>
            detail.permissions.includes(permission),
          )
          .map(([page, title, text, icon]) => (
            <li key={page}>
              <Link
                href={`/${detail.slug}/settings/${page}`}
                className="flex h-full items-center gap-3 rounded-lg border border-border bg-card p-4 shadow-xs outline-none hover:bg-row-hover focus-visible:ring-2 focus-visible:ring-ring"
              >
                <Icon icon={icon} size={16} className="shrink-0" />
                <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span
                    data-slot="card-title"
                    className="text-body font-medium"
                  >
                    {t(`shell.${title}`)}
                  </span>
                  <span className="text-small text-muted-foreground">
                    {t(`home.${text}`, { organisation: detail.name })}
                  </span>
                </span>
                <Icon
                  icon={ChevronRightIcon}
                  size={16}
                  directional
                  className="shrink-0"
                />
              </Link>
            </li>
          ))}
      </ul>
    </div>
  );
}
