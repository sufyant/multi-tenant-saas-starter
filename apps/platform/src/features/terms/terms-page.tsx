"use client";

import {
  getGetTermsOfUseQueryOptions,
  problemOf,
  type TermsVersionResponse,
  TermsVersionResponseState,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { ChevronRightIcon, Icon, PlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { Pill } from "@repo/ui/components/pill";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { cn } from "@repo/ui/lib/utils";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { leaveOf } from "@/shared/problem-screen";
import {
  linkText,
  newTermsVersionPage,
  stateTone,
  termsVersionPage,
  useAcceptanceText,
  useDay,
} from "@/shared/terms";

/**
 * Settings › Terms of use (11.01, 11.07 to 11.09, 11.14, 11.16, 11.17): every version but a cancelled one, the latest day first, from
 * one read (`GetTermsOfUse`; the versions are few, so no Load more). Each row says its day and state, its link and what it asks of
 * people. While a version is scheduled, Add version is disabled and says why (F52): the API keeps one scheduled version at a time.
 * On a phone each version is a card that opens its page (11.14). Every system admin holds every platform permission (the API's
 * A4), so nothing checks one; the API still decides.
 */
export function TermsPage() {
  const t = useTranslations();
  const day = useDay();
  const acceptance = useAcceptanceText();
  const reasonId = useId();
  const terms = useQuery(getGetTermsOfUseQueryOptions());
  const scheduled = terms.data?.versions.find(
    (version) => version.state === TermsVersionResponseState.Scheduled,
  );
  // A cancelled version leaves the list (11.13); its page stays, and the Activity log links to it.
  const versions =
    terms.data?.versions.filter(
      (version) => version.state !== TermsVersionResponseState.Cancelled,
    ) ?? [];
  const activeUserCount = terms.data?.activeUserCount ?? 0;
  const stateText = (state: TermsVersionResponseState) =>
    t(`terms.state.${state}`);

  const columns: TableColumn<TermsVersionResponse>[] = [
    {
      key: "effectiveDate",
      header: t("terms.appliesFrom"),
      render: (version) => (
        <span className="flex min-w-0 items-center gap-2">
          <Link
            href={termsVersionPage(version.id)}
            className="truncate hover:underline"
          >
            {day(version.effectiveDate)}
          </Link>
          <Pill tone={stateTone(version.state)}>
            {stateText(version.state)}
          </Pill>
        </span>
      ),
    },
    {
      key: "url",
      header: t("terms.link"),
      width: "w-62",
      render: (version) => (
        <bdi dir="ltr" className="block truncate">
          {linkText(version.url)}
        </bdi>
      ),
    },
    {
      key: "asks",
      header: t("terms.acceptance"),
      width: "w-62",
      render: (version) => acceptance(version, activeUserCount),
    },
  ];

  const body = (() => {
    if (terms.isPending)
      return <Skeleton rows={3} label={t("terms.loading")} />;
    if (!terms.data)
      return (
        <ListError
          title={t("terms.error")}
          problem={problemOf(terms.error)}
          leave={leaveOf(terms.error)}
          at={terms.errorUpdatedAt}
          onRetry={() => void terms.refetch()}
        />
      );
    // Before the first version the page has its text and Add version only.
    if (versions.length === 0) return null;
    return (
      <>
        <Table
          className="max-tablet:hidden"
          columns={columns}
          rows={versions}
          rowKey="id"
          label={t("terms.title")}
          rowCount={versions.length}
          page={1}
          pageSize={Math.max(versions.length, 1)}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
        {/* 11.14: a card for each version, which opens its page. */}
        <ul
          aria-label={t("terms.title")}
          className="flex flex-col overflow-hidden rounded-lg border border-border bg-card tablet:hidden"
        >
          {versions.map((version) => (
            <li
              key={version.id}
              className="border-b border-border last:border-b-0"
            >
              <Link
                href={termsVersionPage(version.id)}
                className="flex min-h-16 items-center gap-3 px-4 py-3"
              >
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="text-body font-medium">
                    {day(version.effectiveDate)}
                  </span>
                  <span className="text-small text-muted-foreground">
                    {version.requiresAcceptance
                      ? t("terms.requiresAcceptance")
                      : acceptance(version, activeUserCount)}
                  </span>
                </span>
                <Pill tone={stateTone(version.state)}>
                  {version.state === TermsVersionResponseState.InForce
                    ? t("terms.inForce")
                    : stateText(version.state)}
                </Pill>
                <Icon
                  icon={ChevronRightIcon}
                  size={16}
                  className="shrink-0 text-muted-foreground rtl:rotate-180"
                />
              </Link>
            </li>
          ))}
        </ul>
      </>
    );
  })();

  const addClass = cn(
    buttonVariants({ variant: "default" }),
    "max-tablet:h-control-lg max-tablet:w-full",
  );
  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="flex flex-col gap-4 tablet:flex-row tablet:items-start tablet:justify-between">
        {/* On a phone the top bar names the page (11.14), so its header stays for screen readers. */}
        <div className="flex min-w-0 flex-col gap-0.5">
          <div className="sr-only flex flex-col gap-0.5 tablet:not-sr-only">
            <h1 className="text-page-title">{t("terms.title")}</h1>
            <p className="max-w-[720px] text-body text-muted-foreground">
              {t("terms.text")}
            </p>
          </div>
          {scheduled && (
            <p
              id={reasonId}
              className="max-w-[720px] text-body text-muted-foreground max-tablet:order-last max-tablet:text-small"
            >
              {t("terms.scheduledExists", {
                date: day(scheduled.effectiveDate),
              })}
            </p>
          )}
        </div>
        {scheduled || !terms.data ? (
          <Button
            variant="primary"
            className="max-tablet:-order-1 max-tablet:h-control-lg max-tablet:w-full"
            disabled
            aria-describedby={scheduled ? reasonId : undefined}
          >
            <Icon icon={PlusIcon} />
            {t("terms.add")}
          </Button>
        ) : (
          // A link to a page, drawn as the primary button: a link, not a button, because it navigates.
          <Link href={newTermsVersionPage} className={addClass}>
            <Icon icon={PlusIcon} />
            {t("terms.add")}
          </Link>
        )}
      </div>
      {body}
    </section>
  );
}
