"use client";

import {
  ErrorCode,
  type FeatureFlagChangeResponse,
  FeatureFlagChangeResponseKind,
  type FeatureFlagDetailResponse,
  FeatureFlagResponseCleanup,
  FeatureFlagResponseType,
  getGetFeatureFlagQueryOptions,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { Icon, PlusIcon, SearchXIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import { formatTime } from "@repo/ui/lib/format-date-time";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { BackLink } from "@/shared/back-link";
import {
  featureFlagsPage,
  isRemoved,
  useActorName,
  usePercentText,
  useTypeText,
} from "@/shared/feature-flags";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { daysSince } from "./flags-page";
import { type RuleChange, RuleDialog } from "./rule-dialogs";

/**
 * One flag (12.05 to 12.12, 12.18, 12.20 to 12.33): Back to Feature flags, its key and type, the banner of its state, its values,
 * "Who it is on for" with its three rules in order, each with its own change, and its History. A flag removed from code is
 * read-only (12.12). A key that no flag has is "Page not found" (12.31).
 */
export function FlagPage({ flagKey }: { flagKey: string }) {
  const t = useTranslations();
  const flag = useQuery(getGetFeatureFlagQueryOptions(flagKey));
  if (problemOf(flag.error)?.code === ErrorCode.FeatureFlagNotFound)
    return (
      <EmptyState
        icon={SearchXIcon}
        title={t("problems.notFoundTitle")}
        description={t("featureFlags.notFoundText")}
        action={
          <Button
            render={<Link href={featureFlagsPage} />}
            nativeButton={false}
          >
            {t("featureFlags.title")}
          </Button>
        }
      />
    );
  if (flag.isPending) return <Skeleton rows={4} label={t("common.loading")} />;
  if (!flag.data)
    return (
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        <BackLink href={featureFlagsPage} label={t("featureFlags.title")} />
        <ListError
          title={t("featureFlags.flagError")}
          problem={problemOf(flag.error)}
          leave={flag.error && leaveOf(flag.error)}
          at={flag.errorUpdatedAt}
          onRetry={() => void flag.refetch()}
        />
      </section>
    );
  return <Detail detail={flag.data} />;
}

/** The latest change of everyone else, which says since when a kill switch is off and who turned it off (12.23, 12.27). */
const lastEveryoneElse = (history: FeatureFlagChangeResponse[]) =>
  history.find(
    (change) =>
      change.kind === FeatureFlagChangeResponseKind.EveryoneElseChanged,
  );

function Detail({ detail }: { detail: FeatureFlagDetailResponse }) {
  const t = useTranslations("featureFlags");
  const { date, timeZone } = useDates();
  const all = useTranslations();
  const typeText = useTypeText();
  const [change, setChange] = useState<RuleChange | null>(null);
  const { flag } = detail;
  const removed = isRemoved(flag);
  const killSwitch = flag.type === FeatureFlagResponseType.KillSwitch;
  const time = (at: string) =>
    formatTime(at, { locale: all("locale.format"), timeZone });
  const dayAndRevision = (at: string, revision: string | null) =>
    revision ? t("dayAndRevision", { date: date(at), revision }) : date(at);
  const turnedOff =
    killSwitch && !flag.everyoneElse && !removed
      ? lastEveryoneElse(detail.history)
      : undefined;

  const subtitle = killSwitch
    ? removed
      ? t("killSwitchRemoved")
      : t("killSwitchInCode")
    : removed
      ? t("releaseFlagRemoved")
      : t("releaseFlagInCode");

  const banner = (() => {
    if (removed && flag.removedAt)
      return (
        <Banner tone="info" title={t("removedFromCode")}>
          {t("removedBanner", {
            date: date(flag.removedAt),
            revision: detail.removedRevision ?? "",
          })}
        </Banner>
      );
    if (turnedOff)
      return (
        <Banner
          tone="destructive"
          title={t("offTitle", { time: time(turnedOff.at) })}
        >
          {t("offText")}
        </Banner>
      );
    if (
      flag.cleanup === FeatureFlagResponseCleanup.ReadyToRemove &&
      flag.cleanupSince
    )
      return (
        <Banner tone="warning" title={t("readyTitle")}>
          {t("readyText", { days: daysSince(flag.cleanupSince) })}
        </Banner>
      );
    if (flag.cleanup === FeatureFlagResponseCleanup.Unused && flag.cleanupSince)
      return (
        <Banner tone="warning" title={t("unusedTitle")}>
          {t("unusedText", { days: daysSince(flag.cleanupSince) })}
        </Banner>
      );
    return null;
  })();

  const details: { term: string; value: ReactNode }[] = [
    // The description comes from the code, in its own language and direction (39-design's copy note).
    { term: t("description"), value: <bdi>{detail.description}</bdi> },
    {
      term: t("type"),
      value: killSwitch ? t("typeKillSwitch") : t("typeRelease"),
    },
    {
      term: t("defaultValue"),
      value: detail.defaultValue ? t("defaultOn") : t("defaultOff"),
    },
    {
      term: t("added"),
      value: dayAndRevision(detail.addedAt, detail.addedRevision),
    },
  ];

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <BackLink href={featureFlagsPage} label={t("title")} />
      <div className="flex min-w-0 items-start justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h1 className="text-page-title">
            <bdi dir="ltr">{flag.key}</bdi>
          </h1>
          <p className="text-body text-muted-foreground max-tablet:hidden">
            {subtitle}
          </p>
        </div>
        {/* A phone names the type beside the key and the description under it (12.20). */}
        <span className="tablet:hidden">
          <Label>{typeText(flag.type)}</Label>
        </span>
      </div>
      <p className="text-body text-muted-foreground tablet:hidden">
        <bdi>{detail.description}</bdi>
      </p>
      {banner}
      <dl className="flex flex-col rounded-lg border border-border bg-card max-tablet:hidden">
        {details.map(({ term, value }, index) => (
          <div
            key={term}
            className={
              index > 0
                ? "flex items-center gap-6 border-t border-border px-5 py-3"
                : "flex items-center gap-6 px-5 py-3"
            }
          >
            <dt className="w-65 shrink-0 text-body font-medium">{term}</dt>
            <dd className="min-w-0 text-body">{value}</dd>
          </div>
        ))}
      </dl>
      <div className="flex flex-col gap-5 desktop:flex-row desktop:items-start">
        <Rules
          detail={detail}
          readOnly={removed}
          turnedOff={turnedOff}
          time={time}
          onChange={setChange}
        />
        <History detail={detail} dayAndRevision={dayAndRevision} />
      </div>
      {change && (
        <RuleDialog
          detail={detail}
          change={change}
          onClose={() => setChange(null)}
        />
      )}
    </section>
  );
}

/** "Who it is on for" (12.05): the three rules in order, each with its own change; none on a flag removed from code (12.12). */
function Rules({
  detail,
  readOnly,
  turnedOff,
  time,
  onChange,
}: {
  detail: FeatureFlagDetailResponse;
  readOnly: boolean;
  turnedOff: FeatureFlagChangeResponse | undefined;
  time: (at: string) => string;
  onChange: (change: RuleChange) => void;
}) {
  const t = useTranslations("featureFlags");
  const percent = usePercentText();
  const actorName = useActorName();
  const { flag } = detail;
  const killSwitch = flag.type === FeatureFlagResponseType.KillSwitch;
  const rule = (
    number: number,
    title: string,
    body: ReactNode,
    action?: ReactNode,
  ) => (
    <li className="flex items-start gap-3 border-t border-border px-4 py-3.5">
      <div className="flex min-w-0 flex-1 flex-col gap-1 text-body">
        <h3 className="font-medium">
          <span className="me-2">{number}</span>
          {title}
        </h3>
        {body}
      </div>
      {!readOnly && action}
    </li>
  );
  const killOff = killSwitch && flag.everyoneElse;
  return (
    <section
      aria-labelledby="rules-title"
      className="flex min-w-0 flex-col rounded-lg border border-border bg-card desktop:w-150 desktop:shrink-0"
    >
      <div className="flex flex-col gap-1 px-4 py-3.5 text-body">
        <h2 id="rules-title" className="font-medium">
          {t("rulesTitle")}
        </h2>
        <p className="text-muted-foreground">{t("rulesText")}</p>
      </div>
      <ol>
        {rule(
          1,
          t("rule1"),
          <>
            <p className="text-muted-foreground">{t("rule1Text")}</p>
            {detail.listed.length === 0 ? (
              <p>{t("noneListed")}</p>
            ) : (
              <ul className="flex flex-col">
                {detail.listed.map((tenant) => (
                  <li
                    key={tenant.tenantId}
                    className="flex min-h-control-md items-center gap-2"
                  >
                    <Link
                      href={`/organisations/${tenant.tenantId}`}
                      className="hover:underline"
                    >
                      <bdi>{tenant.name}</bdi>
                    </Link>
                    {!readOnly && (
                      <Button
                        variant="ghost"
                        aria-label={t("removeName", {
                          organisation: tenant.name,
                        })}
                        onClick={() => onChange({ kind: "remove", tenant })}
                      >
                        {t("remove")}
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </>,
          <Button onClick={() => onChange({ kind: "add" })}>
            <Icon icon={PlusIcon} />
            {t("addOrganisation")}
          </Button>,
        )}
        {rule(
          2,
          t("rule2"),
          <>
            <p>
              {flag.percentage > 0 ? percent(flag.percentage) : <EmptyValue />}
            </p>
            {/* The sentence explains a percentage; a flag at 0 has none to explain (12.27). */}
            {flag.percentage > 0 && (
              <p className="text-muted-foreground">{t("rule2Text")}</p>
            )}
          </>,
          <Button onClick={() => onChange({ kind: "setPercentage" })}>
            {t("change")}
          </Button>,
        )}
        {rule(
          3,
          t("rule3"),
          <>
            <p>{flag.everyoneElse ? t("on") : t("off")}</p>
            {turnedOff?.actor && (
              <p className="text-muted-foreground">
                {t("turnedOffBy", {
                  actor: actorName(turnedOff.actor),
                  time: time(turnedOff.at),
                })}
              </p>
            )}
          </>,
          <Button
            variant={killOff ? "destructive" : "secondary"}
            onClick={() =>
              onChange({ kind: "everyoneElse", on: !flag.everyoneElse })
            }
          >
            {flag.everyoneElse ? t("turnOff") : t("turnOn")}
          </Button>,
        )}
      </ol>
    </section>
  );
}

/** The flag's History (12.05): each change, newest first, with when and who; a deploy's change names its commit. */
function History({
  detail,
  dayAndRevision,
}: {
  detail: FeatureFlagDetailResponse;
  dayAndRevision: (at: string, revision: string | null) => string;
}) {
  const t = useTranslations("featureFlags");
  const percent = usePercentText();
  const actorName = useActorName();
  const { date, dateTime } = useDates();
  const onOff = (value: string | null) =>
    value === "on" ? t("onWord") : t("offWord");
  const percentOf = (value: string | null) => percent(Number(value ?? 0));
  const line = (change: FeatureFlagChangeResponse) => {
    const meta = change.actor
      ? t("changedBy", {
          date: dateTime(change.at),
          name: actorName(change.actor),
        })
      : dateTime(change.at);
    const organisation = () => <bdi>{change.tenant?.name}</bdi>;
    switch (change.kind) {
      case FeatureFlagChangeResponseKind.AddedToCode:
        return {
          title: t("historyAdded", {
            date: date(change.at),
            revision: change.revision ?? "",
          }),
          meta: detail.defaultValue
            ? t("historyDefaultOn")
            : t("historyDefaultOff"),
        };
      case FeatureFlagChangeResponseKind.RemovedFromCode:
        return {
          title: change.revision
            ? t("historyRemoved", {
                date: date(change.at),
                revision: change.revision,
              })
            : dayAndRevision(change.at, null),
          meta: t("historyArchived"),
        };
      case FeatureFlagChangeResponseKind.Listed:
        return { title: t.rich("historyListed", { organisation }), meta };
      case FeatureFlagChangeResponseKind.Unlisted:
        return { title: t.rich("historyUnlisted", { organisation }), meta };
      case FeatureFlagChangeResponseKind.PercentageChanged:
        return {
          title: t("historyPercentage", {
            from: percentOf(change.from),
            to: percentOf(change.to),
          }),
          meta,
        };
      case FeatureFlagChangeResponseKind.EveryoneElseChanged:
        return {
          title: t("historyEveryoneElse", {
            from: onOff(change.from),
            to: onOff(change.to),
          }),
          meta,
        };
    }
  };
  return (
    <section
      aria-labelledby="history-title"
      className="flex min-w-0 flex-1 flex-col rounded-lg border border-border bg-card"
    >
      <h2 id="history-title" className="px-4 py-3.5 text-body font-medium">
        {t("history")}
      </h2>
      <ol>
        {detail.history.map((change) => {
          const { title, meta } = line(change);
          return (
            <li
              key={`${change.kind}-${change.at}`}
              className="flex items-start gap-3 border-t border-border px-4 py-3.5 text-body"
            >
              <span
                aria-hidden="true"
                className="mt-1.5 size-2 shrink-0 rounded-full bg-border"
              />
              <div className="flex min-w-0 flex-col gap-0.5">
                <p>{title}</p>
                <p className="text-muted-foreground">{meta}</p>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
