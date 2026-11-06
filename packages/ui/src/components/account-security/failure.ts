"use client";

import { useTranslations } from "next-intl";
import { showToast } from "../toast";
import { isCancelled } from "./identity";

/** A change the identity provider refused: a cancelled check says nothing; any other failure says it did not work. */
export function useFailure(onUnexpectedError?: (error: Error) => void) {
  const t = useTranslations("accountSecurity");
  return (reason: unknown) => {
    if (isCancelled(reason)) return;
    if (reason instanceof Error) onUnexpectedError?.(reason);
    showToast({ title: t("notDone"), tone: "destructive" });
  };
}
