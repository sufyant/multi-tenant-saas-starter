"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { ChevronRightIcon, Icon } from "@repo/ui/components/icon";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { organisationPages } from "@/shared/organisation-pages";
import { useCan, useTenant } from "@/shared/tenant-context";

/** Home's cards, in the design's order, each with its text under `home` and its title; General is "Settings". */
const cards = [
  { id: "members", text: "members", title: "members" },
  { id: "roles", text: "roles", title: "roles" },
  { id: "activity", text: "activity", title: "activityLog" },
  { id: "general", text: "settings", title: "settings" },
] as const;

/**
 * An organisation's Home (the Home screens): "Welcome" with the person's first name, the organisation's name, and a card for each
 * page the person can open (F5). The product's own work comes after it in the menu (`productSections`).
 */
export function HomePage() {
  const t = useTranslations();
  const can = useCan();
  const { detail } = useTenant();
  const me = useQuery(getGetMeQueryOptions());
  const firstName = me.data?.firstName;
  const organisation = () => <bdi>{detail.name}</bdi>;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-0.5">
        <h1 className="text-page-title">
          {firstName
            ? t.rich("home.welcome", { name: () => <bdi>{firstName}</bdi> })
            : t("home.welcomeNoName")}
        </h1>
        <p className="text-body text-muted-foreground">
          <bdi>{detail.name}</bdi>
        </p>
      </div>
      <ul className="grid max-w-190 gap-4 tablet:grid-cols-2">
        {cards
          .map((card) => ({
            ...card,
            page: organisationPages.find((page) => page.id === card.id),
          }))
          .filter(({ page }) => page && can(page.permission))
          .map(({ id, text, title, page }) => (
            <li key={id}>
              <Link
                href={`/${detail.slug}/settings/${id}`}
                className="flex h-full items-center gap-3 rounded-lg border border-border bg-card p-4 shadow-xs outline-none hover:bg-row-hover focus-visible:ring-2 focus-visible:ring-ring"
              >
                {page && (
                  <Icon icon={page.icon} size={16} className="shrink-0" />
                )}
                <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span
                    data-slot="card-title"
                    className="text-body font-medium"
                  >
                    {t(`shell.${title}`)}
                  </span>
                  <span className="text-small text-muted-foreground">
                    {t.rich(`home.${text}`, { organisation })}
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
