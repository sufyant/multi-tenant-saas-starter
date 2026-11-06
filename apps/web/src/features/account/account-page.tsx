"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { AccountSecurity } from "@repo/ui/components/account-security";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { reportError } from "@/error-tracking/report-error";
import { ProblemScreen } from "@/shared/problem-screen";
import { useTenant } from "@/shared/tenant-context";
import { useEmailChange } from "./use-email-change";

/**
 * Settings › Account and security (F62, the Account and security screens): the shared page with the tenant web's words. Its
 * settings are the person's, the same in every organisation; two-step verification is optional here.
 */
export function AccountPage() {
  const t = useTranslations();
  const { tenant } = useTenant();
  const me = useQuery(getGetMeQueryOptions());
  const emailChange = useEmailChange();
  if (me.error) return <ProblemScreen error={me.error} />;
  if (!me.data) return <Skeleton label={t("common.loading")} />;
  return (
    <AccountSecurity
      intro={t.rich("securityPage.intro", {
        name: () => <bdi>{me.data.name}</bdi>,
      })}
      email={me.data.email}
      emailChange={emailChange}
      twoStep={{ text: t("securityPage.twoStep"), required: false }}
      returnPath={`/${tenant.slug}/settings/account`}
      timeZone={me.data.timeZone}
      onUnexpectedError={reportError}
    />
  );
}
