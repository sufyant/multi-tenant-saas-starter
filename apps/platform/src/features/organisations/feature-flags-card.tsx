"use client";

import {
  FeatureFlagResponseType,
  getGetTenantFeatureFlagsQueryOptions,
  problemOf,
  type TenantFeatureFlagResponse,
  TenantFeatureFlagResponseReason,
} from "@repo/api-client";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { featureFlagPage, usePercentText } from "@/shared/feature-flags";
import { leaveOf } from "@/shared/problem-screen";

/**
 * The flags that are on for one organisation (4.09), under its Overview: each key with the rule that turns it on, Listed, a
 * percentage or on for everyone; a kill switch says so. The key opens the flag's page, where Settings › Feature flags manages it.
 */
export function FeatureFlagsCard({ tenantId }: { tenantId: string }) {
  const t = useTranslations("featureFlags");
  const common = useTranslations("common");
  const percent = usePercentText();
  const flags = useQuery(getGetTenantFeatureFlagsQueryOptions(tenantId));

  const reason = (flag: TenantFeatureFlagResponse) => {
    switch (flag.reason) {
      case TenantFeatureFlagResponseReason.Listed:
        return { text: t("reasonListed"), note: t("reasonListedText") };
      case TenantFeatureFlagResponseReason.Percentage:
        return {
          text: t("reasonPercentage", { percentage: percent(flag.percentage) }),
        };
      case TenantFeatureFlagResponseReason.Everyone:
        return { text: t("reasonEveryone") };
    }
  };

  const body = (() => {
    if (flags.isPending) return <Skeleton rows={2} label={common("loading")} />;
    if (!flags.data)
      return (
        <ListError
          title={t("organisationError")}
          problem={problemOf(flags.error)}
          leave={flags.error && leaveOf(flags.error)}
          at={flags.errorUpdatedAt}
          onRetry={() => void flags.refetch()}
        />
      );
    if (flags.data.length === 0)
      return (
        <p className="px-5 py-3 text-body text-muted-foreground">
          {t("organisationNone")}
        </p>
      );
    return (
      <dl className="flex flex-col divide-y divide-border">
        {flags.data.map((flag) => {
          const { text, note } = reason(flag);
          const killSwitch = flag.type === FeatureFlagResponseType.KillSwitch;
          return (
            <div
              key={flag.key}
              className="flex flex-col gap-0.5 px-4 py-3 tablet:flex-row tablet:items-center tablet:gap-6 tablet:px-5"
            >
              <dt className="text-small text-muted-foreground tablet:w-48 tablet:shrink-0">
                <Link
                  href={featureFlagPage(flag.key)}
                  className="hover:underline"
                >
                  <bdi dir="ltr">{flag.key}</bdi>
                </Link>
              </dt>
              <dd className="flex min-w-0 flex-col gap-0.5 text-body">
                <span>{text}</span>
                {(note || killSwitch) && (
                  <span className="text-small text-muted-foreground">
                    {note ?? t("reasonKillSwitch")}
                  </span>
                )}
              </dd>
            </div>
          );
        })}
      </dl>
    );
  })();

  return (
    <section
      aria-labelledby="organisation-flags"
      className="flex flex-col rounded-lg border border-border bg-card"
    >
      <div className="flex flex-col gap-0.5 border-b border-border px-4 py-3 tablet:flex-row tablet:items-center tablet:gap-6 tablet:px-5">
        <h2
          id="organisation-flags"
          className="text-small text-muted-foreground tablet:w-48 tablet:shrink-0"
        >
          {t("organisationTitle")}
        </h2>
        <p className="text-small text-muted-foreground">
          {t("organisationText")}
        </p>
      </div>
      {body}
    </section>
  );
}
