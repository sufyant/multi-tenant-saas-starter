"use client";

import type { Language } from "@repo/api-client";
import { useRouter } from "next/navigation";
import { useLocale } from "next-intl";
import { useEffect } from "react";
import { setLocale } from "@/i18n/set-locale";

/** After sign-in, the user's language comes from GetMe (F13). When it differs from the page's, the cookie changes and the page renders again. */
export function LocaleSync({ language }: { language: Language }) {
  const locale = useLocale();
  const router = useRouter();
  useEffect(() => {
    if (language === locale) return;
    void setLocale(language).then(() => router.refresh());
  }, [language, locale, router]);
  return null;
}
