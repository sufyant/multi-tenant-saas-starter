"use client";

import { useTranslations } from "next-intl";
import { useAppFrame } from "../app-frame";
import { useEnvironmentText } from "../environment-strip";

/**
 * The browser tab's title of a signed-in page (the Browser tab screens): "(2) Members · Starter". Outside production the
 * environment's mark comes first: "[Staging] (2) Members · Starter". At zero unread the count leaves the title.
 */
export function useTabTitleText(product: string) {
  const t = useTranslations("appShell");
  const { environment } = useAppFrame();
  const mark = useEnvironmentText()(environment)?.tab;
  return (page: string, unread: number | undefined) => {
    const title = unread
      ? t("tabTitleUnread", { count: unread, page, product })
      : t("tabTitle", { page, product });
    return mark ? `${mark} ${title}` : title;
  };
}
