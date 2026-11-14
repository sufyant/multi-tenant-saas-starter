"use client";

import {
  type FeatureFlagCountsResponse,
  type FeatureFlagResponse,
  FeatureFlagResponseCleanup,
  getGetFeatureFlagsQueryOptions,
  problemOf,
} from "@repo/api-client";
import { EmptyState } from "@repo/ui/components/empty-state";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { FlagIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { Pill } from "@repo/ui/components/pill";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { parseAsStringLiteral, useQueryState } from "nuqs";
import {
  featureFlagPage,
  isRemoved,
  useActorName,
  useStateText,
  useTypeText,
} from "@/shared/feature-flags";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { ViewChoice } from "@/shared/view-choice";

/**
 * The four tabs (12.01 to 12.04), in the URL (F9): `?tab=unused`. All is the default and leaves the URL clean. The two cleanups are
 * the API's; a flag removed from code is "removed".
 */
const removedTab = "removed";
const tabs = [
  FeatureFlagResponseCleanup.ReadyToRemove,
  FeatureFlagResponseCleanup.Unused,
  removedTab,
] as const;
type Tab = (typeof tabs)[number] | "all";

const tabParser = parseAsStringLiteral(tabs);

const tabLabels = {
  all: "tabAll",
  [FeatureFlagResponseCleanup.ReadyToRemove]: "tabReadyToRemove",
  [FeatureFlagResponseCleanup.Unused]: "tabUnused",
  [removedTab]: "tabRemovedFromCode",
} as const satisfies Record<Tab, string>;

const countOf = (counts: FeatureFlagCountsResponse, tab: Tab) =>
  ({
    all: counts.all,
    [FeatureFlagResponseCleanup.ReadyToRemove]: counts.readyToRemove,
    [FeatureFlagResponseCleanup.Unused]: counts.unused,
    [removedTab]: counts.removedFromCode,
  })[tab];

/** A tab's flags: All holds the flags the code defines; a flag removed from code shows only in its own tab (12.01, 12.04). */
const inTab = (flag: FeatureFlagResponse, tab: Tab) => {
  if (tab === removedTab) return isRemoved(flag);
  if (isRemoved(flag)) return false;
  if (tab === "all") return true;
  return flag.cleanup === tab;
};

/** The cleanup's pill (12.01): amber for Ready to remove and Unused, nothing for a flag in use. */
function CleanupPill({ flag }: { flag: FeatureFlagResponse }) {
  const t = useTranslations("featureFlags");
  if (flag.cleanup === FeatureFlagResponseCleanup.ReadyToRemove)
    return <Pill tone="warning">{t("readyToRemove")}</Pill>;
  if (flag.cleanup === FeatureFlagResponseCleanup.Unused)
    return <Pill tone="warning">{t("cleanupUnused")}</Pill>;
  return <EmptyValue />;
}

/**
 * Settings › Feature flags (12.01 to 12.04, 12.13 to 12.17, 12.19): every flag of this environment, from one read
 * (`GetFeatureFlags`), in four tabs whose counts the API gives. Each flag shows its type, its state, its cleanup and its last change;
 * the key opens its page. A flag removed from code shows only in its tab, with the day it was removed. On a phone the tabs are one
 * choice and each flag is a card (12.19).
 */
export function FlagsPage() {
  const t = useTranslations();
  const { date } = useDates();
  const stateText = useStateText();
  const typeText = useTypeText();
  const actorName = useActorName();
  const [chosen, setChosen] = useQueryState("tab", tabParser);
  const tab: Tab = chosen ?? "all";
  const list = useQuery(getGetFeatureFlagsQueryOptions());
  const removedShown = tab === removedTab;

  const typeLabel = (flag: FeatureFlagResponse) => (
    <Label>{typeText(flag.type)}</Label>
  );
  const columns: TableColumn<FeatureFlagResponse>[] = [
    {
      key: "flag",
      header: t("featureFlags.flag"),
      render: (flag) => (
        <span className="flex min-w-0 items-center gap-2">
          <Link
            href={featureFlagPage(flag.key)}
            className="truncate hover:underline"
          >
            <bdi dir="ltr">{flag.key}</bdi>
          </Link>
          {typeLabel(flag)}
        </span>
      ),
      cardRender: (flag) => (
        <Link href={featureFlagPage(flag.key)} className="hover:underline">
          <bdi dir="ltr">{flag.key}</bdi>
        </Link>
      ),
    },
    {
      key: "state",
      header: t("featureFlags.state"),
      width: "w-52.5",
      render: (flag) =>
        removedShown ? <EmptyValue /> : <span>{stateText(flag)}</span>,
      // The phone says how long an unused flag has been off (12.19).
      cardRender: (flag) =>
        removedShown
          ? t("featureFlags.removedFromCode")
          : flag.cleanup === FeatureFlagResponseCleanup.Unused &&
              flag.cleanupSince
            ? t("featureFlags.offForDays", {
                days: daysSince(flag.cleanupSince),
              })
            : stateText(flag),
    },
    removedShown
      ? {
          key: "status",
          header: t("featureFlags.status"),
          width: "w-40",
          card: "hide",
          render: () => <Pill tone="neutral">{t("featureFlags.removed")}</Pill>,
        }
      : {
          key: "cleanup",
          header: t("featureFlags.cleanup"),
          width: "w-40",
          card: "end",
          render: (flag) => <CleanupPill flag={flag} />,
          // The phone's card ends with the cleanup's pill, or the type when there is no cleanup (12.19).
          cardRender: (flag) =>
            flag.cleanup === FeatureFlagResponseCleanup.InUse ? (
              typeLabel(flag)
            ) : (
              <CleanupPill flag={flag} />
            ),
        },
    {
      key: "change",
      header: removedShown
        ? t("featureFlags.removed")
        : t("featureFlags.lastChange"),
      width: "w-39",
      secondary: true,
      card: "hide",
      render: (flag) => {
        const at = removedShown ? flag.removedAt : flag.lastChange.at;
        if (!at) return <EmptyValue />;
        // A deploy's change has no person: the day alone.
        const name =
          !removedShown && flag.lastChange.actor
            ? actorName(flag.lastChange.actor)
            : null;
        return (
          <span className="block truncate">
            {name
              ? t("featureFlags.changedBy", { date: date(at), name })
              : date(at)}
          </span>
        );
      },
    },
  ];

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={4} label={t("featureFlags.loading")} />;
    if (!list.data)
      return (
        <ListError
          title={t("featureFlags.listError")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    if (list.data.flags.length === 0)
      return (
        <EmptyState
          icon={FlagIcon}
          title={t("featureFlags.noneTitle")}
          description={t("featureFlags.noneText")}
        />
      );
    const rows = list.data.flags.filter((flag) => inTab(flag, tab));
    return (
      <>
        <ViewChoice
          label={t("featureFlags.title")}
          items={(["all", ...tabs] as const).map((item) => ({
            id: item,
            label: t(`featureFlags.${tabLabels[item]}`),
            count: countOf(list.data.counts, item),
          }))}
          current={tab}
          onValueChange={(value) =>
            void setChosen(tabs.find((item) => item === value) ?? null)
          }
        />
        {rows.length === 0 ? (
          <EmptyState title={t("featureFlags.emptyTab")} />
        ) : (
          <Table
            columns={columns}
            rows={rows}
            rowKey="key"
            label={t("featureFlags.title")}
            rowCount={rows.length}
            page={1}
            pageSize={Math.max(rows.length, 1)}
            pager={{
              previous: t("common.previous"),
              next: t("common.next"),
              page: (current, count) =>
                t("common.page", { page: current, count }),
            }}
          />
        )}
        {removedShown && (
          <p className="text-body text-muted-foreground">
            {t("featureFlags.removedText")}
          </p>
        )}
      </>
    );
  })();

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      {/* On a phone the top bar names the page (12.19), so its header stays for screen readers. */}
      <div className="sr-only flex min-w-0 flex-col gap-0.5 tablet:not-sr-only">
        <h1 className="text-page-title">{t("featureFlags.title")}</h1>
        <p className="max-w-[720px] text-body text-muted-foreground">
          {t("featureFlags.text")}
        </p>
      </div>
      {body}
    </section>
  );
}

/** Whole days since a moment, for "Off for everyone for 34 days" (12.19). */
export const daysSince = (at: string, now = Date.now()) =>
  Math.floor((now - new Date(at).getTime()) / 86_400_000);
