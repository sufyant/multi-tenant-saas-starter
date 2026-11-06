"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { AccountSecurity } from "@repo/ui/components/account-security";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { reportError } from "@/error-tracking/report-error";
import { ProblemScreen } from "@/shared/problem-screen";

/**
 * Settings › Account and security (F62, the platform's Account and security screens): the shared page with the platform's
 * words. Two-step verification is required on the platform (F42), so it is on and has no action here.
 */
export function AccountPage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;
  if (!me.data) return <Skeleton label={t("common.loading")} />;
  const name = [me.data.firstName, me.data.lastName].filter(Boolean).join(" ");
  return (
    <AccountSecurity
      intro={t.rich("securityPage.intro", {
        name: () => <bdi>{name}</bdi>,
      })}
      email={me.data.email}
      twoStep={{ text: t("securityPage.twoStep"), required: true }}
      returnPath="/settings/account"
      timeZone={me.data.timeZone}
      onUnexpectedError={reportError}
    />
  );
}
