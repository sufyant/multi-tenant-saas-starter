"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import {
  AccountSecurity,
  YourData,
} from "@repo/ui/components/account-security";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { reportError } from "@/error-tracking/report-error";
import { ProblemScreen } from "@/shared/problem-screen";
import { useEmailChange } from "./use-email-change";
import { useYourData } from "./use-your-data";

/**
 * Settings › Account and security (F62, the platform's Account and security screens): the shared page with the platform's
 * words. Two-step verification is required on the platform (F42), so it is on and has no action here. Under the shared cards
 * comes Your data, with no Delete my account (the maintainer's decision of 2026-11-05).
 */
export function AccountPage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  const emailChange = useEmailChange();
  const yourData = useYourData();
  if (me.error) return <ProblemScreen error={me.error} />;
  if (!me.data) return <Skeleton label={t("common.loading")} />;
  return (
    <AccountSecurity
      intro={t.rich("securityPage.intro", {
        name: () => <bdi>{me.data.name}</bdi>,
      })}
      email={me.data.email}
      emailChange={emailChange}
      twoStep={{ text: t("securityPage.twoStep"), required: true }}
      returnPath="/settings/account"
      timeZone={me.data.timeZone}
      onUnexpectedError={reportError}
    >
      <YourData
        {...yourData}
        email={me.data.email}
        timeZone={me.data.timeZone}
      />
    </AccountSecurity>
  );
}
