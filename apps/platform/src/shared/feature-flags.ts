"use client";

import {
  type FeatureFlagDetailResponse,
  type FeatureFlagResponse,
  FeatureFlagResponseType,
  getGetFeatureFlagQueryKey,
  getGetFeatureFlagsQueryKey,
  getGetTenantFeatureFlagsQueryKey,
  type UserNameResponse,
} from "@repo/api-client";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { environmentTexts } from "@repo/ui/lib/environment";
import type { QueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";

// What the feature flags' screens, the organisation's card and the Activity sentences share (step 39).

/** Settings › Feature flags: every flag of this environment (12.01). */
export const featureFlagsPage = "/settings/feature-flags";

/** A flag's page, by its key (12.05). */
export const featureFlagPage = (key: string) =>
  `${featureFlagsPage}/${encodeURIComponent(key)}`;

/** A flag that a deploy archived: its page is read-only (12.12). */
export const isRemoved = (flag: FeatureFlagResponse) => flag.removedAt !== null;

/** A percentage as the language writes it: "10%", "%10" in Turkish. */
export function usePercentText() {
  const t = useTranslations("featureFlags");
  return (percentage: number) => t("percentValue", { percentage });
}

/**
 * A flag's state in the list (12.01, 12.19): "On for everyone", "Off", or its first two rules, "10% · 1 organisation". A flag that
 * is off for everyone for 30 days says how long on a phone (12.19); a flag removed from code has no state (12.04).
 */
export function useStateText() {
  const t = useTranslations("featureFlags");
  const percent = usePercentText();
  return (flag: FeatureFlagResponse) => {
    if (flag.everyoneElse) return t("onForEveryone");
    const percentage = flag.percentage > 0 ? percent(flag.percentage) : null;
    const organisations =
      flag.listedCount > 0
        ? t("organisationCount", { count: flag.listedCount })
        : null;
    if (percentage && organisations)
      return t("stateParts", { percentage, organisations });
    return percentage ?? organisations ?? t("off");
  };
}

/** The type's label beside a key (12.01): Release or Kill switch. */
export function useTypeText() {
  const t = useTranslations("featureFlags");
  return (type: FeatureFlagResponseType) =>
    type === FeatureFlagResponseType.KillSwitch
      ? t("killSwitch")
      : t("releaseLabel");
}

/**
 * The environment's name in a confirmation (section 7 of the roadmap: "Turn off emails.send in Production?"): the name of the
 * environment strip, and "Production" where no strip shows.
 */
export function useEnvironmentName() {
  const t = useTranslations();
  const { environment } = useAppFrame();
  const texts = environmentTexts(environment);
  return texts
    ? t(`appShell.environment.${texts.strip}`, texts.values)
    : t("featureFlags.production");
}

/**
 * After a change of a flag's rules: the answer is the flag's page, so its cache takes it, and the list and every organisation's
 * flags read again (F47).
 */
export const refreshFlag = (
  queryClient: QueryClient,
  detail: FeatureFlagDetailResponse,
) => {
  queryClient.setQueryData(getGetFeatureFlagQueryKey(detail.flag.key), detail);
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetFeatureFlagsQueryKey() }),
    queryClient.invalidateQueries({
      queryKey: getGetTenantFeatureFlagsQueryKey(""),
    }),
  ]);
};

/** A system admin of a change, by their name now: "Deleted user" once deleted (A8), "Unnamed user" without a name. */
export function useActorName() {
  const t = useTranslations();
  return (actor: UserNameResponse) =>
    actor.deleted
      ? t("users.deletedUser")
      : (actor.name ?? t("activity.unnamedUser"));
}
