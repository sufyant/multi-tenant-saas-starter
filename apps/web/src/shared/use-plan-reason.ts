"use client";

import { ErrorCode, FeatureKey, LimitKey, problemOf } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { usePlan } from "./plan";

/** A reason in two parts: the fact, and what to do next, when there is something to do. */
export type PlanReason = { title: string; text: string | null };

/**
 * The texts of the plan (the Messages board, Plans): why a feature is off, why a limit closes an action, and the text of a refusal
 * of the plan. A disabled action and the refusal of the same action show the same text (the PlanErrors board). A key the web does
 * not know shows the neutral text of its code.
 */
export function usePlanReason(): {
  feature: (key: string) => string;
  limit: (key: string) => PlanReason;
  /** The text of 403 `plan.feature_unavailable` or 409 `plan.limit_reached` by its key, or null for any other error. */
  refusal: (error: unknown) => PlanReason | null;
} {
  const t = useTranslations();
  const plan = usePlan();

  const feature = (key: string) =>
    key === FeatureKey.RolesCustom
      ? t("plans.rolesCustomOff")
      : t("errors.plan.feature_unavailable");

  const limit = (key: string): PlanReason => {
    const neutral = { title: t("errors.plan.limit_reached"), text: null };
    if (key === LimitKey.RolesCustomMax) {
      const { limit } = plan.limit(key);
      return limit === null
        ? neutral
        : {
            title: t("plans.rolesLimit", { limit }),
            text: t("plans.rolesLimitAction"),
          };
    }
    if (key === LimitKey.MembersMax) {
      const { limit } = plan.limit(key);
      return limit === null
        ? neutral
        : {
            title: t("plans.membersLimit", { limit }),
            text: t("plans.membersLimitAction"),
          };
    }
    return neutral;
  };

  const refusal = (error: unknown): PlanReason | null => {
    const problem = problemOf(error);
    const key = problem?.entitlement ?? "";
    if (problem?.code === ErrorCode.PlanFeatureUnavailable)
      return { title: feature(key), text: null };
    if (problem?.code === ErrorCode.PlanLimitReached) return limit(key);
    return null;
  };

  return { feature, limit, refusal };
}

/** A reason as one sentence, for the description of a disabled control. */
export const sentenceOf = (reason: PlanReason) =>
  reason.text ? `${reason.title} ${reason.text}` : reason.title;
