"use client";

import type { UserNameResponse } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { nameOf } from "@/shared/name-of";

/**
 * A system admin's name inside a sentence of an organisation's page ("by Deniz Kaya"): "Deleted user" once the account is deleted
 * (A8), as the Activity log's sentences write it.
 */
export function usePersonName() {
  const t = useTranslations("activity");
  return (person: UserNameResponse) =>
    person.deleted ? t("deletedUser") : (nameOf(person) ?? t("unnamedUser"));
}
