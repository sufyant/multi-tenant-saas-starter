"use client";

import {
  ErrorCode,
  getGetTermsOfUseQueryOptions,
  getGetTermsVersionQueryOptions,
  getListTermsVersionPeopleInfiniteQueryOptions,
  problemOf,
  schemas,
  type TermsPersonResponse,
  TermsPersonResponseStatus,
  type TermsVersionDetailResponse,
  TermsVersionResponseState,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { Pill } from "@repo/ui/components/pill";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Tabs } from "@repo/ui/components/tabs";
import { pageSize } from "@repo/ui/lib/pages";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { parseAsStringLiteral, useQueryState } from "nuqs";
import { type ReactNode, useState } from "react";
import { BackLink } from "@/shared/back-link";
import { useLoadMore } from "@/shared/load-more";
import { NotFoundPage } from "@/shared/not-found-page";
import { leaveOf, ProblemScreen } from "@/shared/problem-screen";
import {
  editTermsVersionPage,
  linkText,
  stateTone,
  termsPage,
  useDay,
} from "@/shared/terms";
import { useDates } from "@/shared/use-dates";
import { CancelDialog } from "./cancel-dialog";

/** The two tabs of a version's people (11.05, 11.06), in the URL (F9): `?people=not_yet`. */
const statusParser = parseAsStringLiteral(
  Object.values(TermsPersonResponseStatus),
).withDefault(TermsPersonResponseStatus.Accepted);

/**
 * One terms version (11.05, 11.06, 11.12, 11.15): Back to Terms of use, its day and state, its values and, for a version that
 * requires acceptance, its people in two tabs, Accepted and Not yet, which grow by a cursor (F9). The scheduled version has Edit
 * version and Cancel version; a version in force says it never changes. On a phone the values stack, and the people stay on the
 * desktop (11.15).
 */
export function VersionPage({ versionId }: { versionId: string }) {
  const known =
    schemas.GetTermsVersionParams.shape.versionId.safeParse(versionId).success;
  const detail = useQuery(
    getGetTermsVersionQueryOptions(versionId, { query: { enabled: known } }),
  );
  const t = useTranslations("common");
  if (
    !known ||
    problemOf(detail.error)?.code === ErrorCode.TermsVersionNotFound
  )
    return <NotFoundPage />;
  if (detail.error) return <ProblemScreen error={detail.error} />;
  if (!detail.data) return <Skeleton rows={4} label={t("loading")} />;
  return <Detail detail={detail.data} />;
}

function Detail({ detail }: { detail: TermsVersionDetailResponse }) {
  const t = useTranslations();
  const day = useDay();
  const { date } = useDates();
  const router = useFreshRouter(useRouter());
  const [cancelling, setCancelling] = useState(false);
  const terms = useQuery(getGetTermsOfUseQueryOptions());
  const { version, activeUserCount } = detail;
  const scheduled = version.state === TermsVersionResponseState.Scheduled;
  const inForce = version.state === TermsVersionResponseState.InForce;
  const effectiveDate = day(version.effectiveDate);
  const state = t(`terms.state.${version.state}`);
  const link = (text: string, arrow: boolean) => (
    <a
      href={version.url}
      target="_blank"
      rel="noreferrer"
      className="min-w-0 break-all hover:underline"
    >
      <bdi dir="ltr">{text}</bdi>
      {arrow && <span aria-hidden="true"> ↗</span>}
      <span className="sr-only"> {t("terms.opensNewTab")}</span>
    </a>
  );
  const email = version.emailSentAt
    ? t("terms.sentOn", { date: date(version.emailSentAt) })
    : t("terms.off");
  const summary = version.summary ? (
    <bdi>{version.summary}</bdi>
  ) : (
    <EmptyValue />
  );
  // A row's phone forms (11.15): its term, its value and a note under it, where they differ from the desktop's (11.05).
  const rows: {
    term: string;
    value: ReactNode;
    phoneTerm?: string;
    phoneValue?: ReactNode;
    note?: string;
  }[] = [
    {
      term: t("terms.appliesFrom"),
      phoneTerm: t("terms.effectiveDate"),
      value: effectiveDate,
    },
    {
      term: t("terms.link"),
      value: link(version.url, true),
      phoneValue: link(linkText(version.url), false),
    },
    { term: t("terms.summary"), value: summary },
    {
      term: t("terms.requiresAcceptance"),
      value: version.requiresAcceptance ? t("terms.yes") : t("terms.no"),
      note: version.requiresAcceptance ? t("terms.acceptsAtSignIn") : undefined,
    },
    { term: t("terms.email"), value: email },
  ];
  const inForceVersion = terms.data?.versions.find(
    (item) => item.state === TermsVersionResponseState.InForce,
  );

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4 max-tablet:gap-3">
      <BackLink href={termsPage} label={t("terms.title")} />
      {/* The header (11.05): the title, the state and the actions. On a phone the day and its pill stand in for it (11.15), and
          the title stays for screen readers. */}
      <div className="flex min-w-0 flex-col gap-0.5">
        <h1 className="text-page-title max-tablet:sr-only">
          {t("terms.versionTitle", { date: effectiveDate })}
        </h1>
        <div className="flex min-w-0 flex-col gap-0.5 max-tablet:hidden">
          <p className="text-body text-muted-foreground">
            {version.requiresAcceptance
              ? t("terms.stateRequiresAcceptance", { state })
              : state}
          </p>
          {inForce && (
            <p className="max-w-[720px] text-body text-muted-foreground">
              {t("terms.inForceText")}
            </p>
          )}
          {scheduled && (
            <div className="flex gap-2">
              <Button
                variant="secondary"
                onClick={() =>
                  void router.push(editTermsVersionPage(version.id))
                }
              >
                {t("terms.edit")}
              </Button>
              <Button variant="secondary" onClick={() => setCancelling(true)}>
                {t("terms.cancel")}
              </Button>
            </div>
          )}
        </div>
        <div className="flex items-center gap-2 tablet:hidden">
          <p aria-hidden="true" className="min-w-0 flex-1 text-page-title">
            {effectiveDate}
          </p>
          <Pill tone={stateTone(version.state)}>
            {inForce ? t("terms.inForce") : state}
          </Pill>
        </div>
      </div>
      <dl className="flex flex-col overflow-hidden rounded-lg border border-border bg-card max-tablet:gap-3 max-tablet:p-4">
        {rows.map((row) => (
          <div
            key={row.term}
            className="flex flex-col gap-0.5 tablet:flex-row tablet:items-center tablet:gap-6 tablet:border-b tablet:border-border tablet:px-5 tablet:py-3 tablet:last:border-b-0"
          >
            <dt className="text-small font-medium text-muted-foreground tablet:w-65 tablet:shrink-0 tablet:text-body tablet:text-foreground">
              <span className="tablet:hidden">{row.phoneTerm ?? row.term}</span>
              <span className="max-tablet:hidden">{row.term}</span>
            </dt>
            <dd className="flex min-w-0 flex-col text-body">
              {row.phoneValue ? (
                <>
                  <span className="max-tablet:hidden">{row.value}</span>
                  <span className="tablet:hidden">{row.phoneValue}</span>
                </>
              ) : (
                row.value
              )}
              {row.note && (
                <span className="text-small text-muted-foreground tablet:hidden">
                  {row.note}
                </span>
              )}
            </dd>
          </div>
        ))}
      </dl>
      {scheduled && (
        <div className="flex flex-col gap-3 tablet:hidden">
          <Button
            variant="secondary"
            size="lg"
            onClick={() => void router.push(editTermsVersionPage(version.id))}
          >
            {t("terms.edit")}
          </Button>
          <Button variant="ghost" size="lg" onClick={() => setCancelling(true)}>
            {t("terms.cancel")}
          </Button>
        </div>
      )}
      {version.requiresAcceptance && (
        <People
          versionId={version.id}
          accepted={version.acceptedCount}
          notYet={Math.max(activeUserCount - version.acceptedCount, 0)}
        />
      )}
      {cancelling && (
        <CancelDialog
          version={version}
          inForce={inForceVersion?.effectiveDate ?? null}
          onClose={() => setCancelling(false)}
          onCancelled={() => void router.push(termsPage)}
        />
      )}
    </section>
  );
}

/** A version's people (11.05, 11.06): who accepted it and when, and who did not yet; a page at a time by the cursor (F9). */
function People({
  versionId,
  accepted,
  notYet,
}: {
  versionId: string;
  accepted: number;
  notYet: number;
}) {
  const t = useTranslations();
  const { dateTime } = useDates();
  const [status, setStatus] = useQueryState("people", statusParser);
  const list = useInfiniteQuery(
    getListTermsVersionPeopleInfiniteQueryOptions(
      versionId,
      { status, limit: pageSize },
      {
        query: {
          initialPageParam: undefined,
          getNextPageParam: (last) => last.nextCursor ?? undefined,
        },
      },
    ),
  );
  const next = useLoadMore(list, {
    loaded: (count) => t("terms.loadedPeople", { count }),
    failed: t("terms.moreError"),
  });
  const columns: TableColumn<TermsPersonResponse>[] = [
    {
      key: "name",
      header: t("terms.name"),
      render: (person) => (
        <bdi className="block truncate">
          {person.name ?? t("activity.unnamedUser")}
        </bdi>
      ),
    },
    {
      key: "email",
      header: t("terms.email"),
      width: "w-65",
      render: (person) =>
        person.email ? (
          <bdi dir="ltr" className="block truncate text-small">
            {person.email}
          </bdi>
        ) : (
          <EmptyValue />
        ),
    },
    {
      key: "acceptedAt",
      header: t("terms.accepted"),
      render: (person) =>
        person.acceptedAt ? (
          <time
            dateTime={new Date(person.acceptedAt).toISOString()}
            className="text-small"
          >
            {dateTime(person.acceptedAt)}
          </time>
        ) : (
          <EmptyValue />
        ),
    },
  ];

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={3} label={t("terms.loadingPeople")} />;
    if (!list.data)
      return (
        <ListError
          title={t("terms.peopleError")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    const rows = list.data.pages.flatMap((page) => page.items);
    return (
      <div className="flex flex-col gap-2">
        <Table
          columns={columns}
          rows={rows}
          rowKey="userId"
          label={
            status === TermsPersonResponseStatus.Accepted
              ? t("terms.accepted")
              : t("terms.notYet")
          }
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
        <LoadMore
          hasMore={list.hasNextPage}
          loading={list.isFetchingNextPage}
          onMore={next.loadMore}
          label={t("terms.loadMore")}
          loadingLabel={t("terms.loadingMore")}
          announcement={next.announcement}
        />
      </div>
    );
  })();

  return (
    // The people stay on the desktop: the phone's version page has its values and actions only (11.15).
    <div className="flex flex-col gap-4 max-tablet:hidden">
      <Tabs
        label={t("terms.title")}
        current={status}
        onValueChange={(id) =>
          void setStatus(
            id === TermsPersonResponseStatus.NotYet
              ? TermsPersonResponseStatus.NotYet
              : TermsPersonResponseStatus.Accepted,
          )
        }
        items={[
          {
            id: TermsPersonResponseStatus.Accepted,
            label: t("terms.accepted"),
            count: accepted,
          },
          {
            id: TermsPersonResponseStatus.NotYet,
            label: t("terms.notYet"),
            count: notYet,
          },
        ]}
      />
      {body}
    </div>
  );
}
