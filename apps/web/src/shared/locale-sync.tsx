"use client";

import { useClerk } from "@clerk/nextjs";
import type { Language } from "@repo/api-client";
import { useRouter } from "next/navigation";
import { useLocale } from "next-intl";
import { useEffect } from "react";
import { setLocale } from "@/i18n/set-locale";

/**
 * After sign-in, the user's language comes from GetMe (F13). When it differs from the page's, the cookie changes and the page
 * renders again. The refresh waits for Clerk's script: while it loads in a development instance, it deletes and writes the dev
 * browser cookie again, and a request in that moment reaches the proxy without it and is signed out.
 */
export function LocaleSync({ language }: { language: Language }) {
  const locale = useLocale();
  const router = useRouter();
  const { loaded } = useClerk();
  useEffect(() => {
    if (!loaded || language === locale) return;
    void setLocale(language).then(() => router.refresh());
  }, [loaded, language, locale, router]);
  return null;
}
