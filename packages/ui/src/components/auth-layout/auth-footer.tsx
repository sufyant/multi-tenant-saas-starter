"use client";

import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { LanguageSwitcher } from "../sign-in/language-switcher";

export type AuthFooterProps = {
  /** Sets the locale cookie of F13: the app's `setLocale` server action. */
  onLanguageChange: (language: Language) => Promise<void>;
  /** The brand's privacy policy. */
  privacyUrl: string;
};

/**
 * The footer of every screen before sign-in, in both apps (the Auth footer): the language switcher at the start, "Privacy" at the
 * end, which opens the brand's privacy policy. It mirrors in right-to-left. "Terms" joins it with its endpoint in a later step.
 */
export function AuthFooter({ onLanguageChange, privacyUrl }: AuthFooterProps) {
  const t = useTranslations("appShell.authFooter");
  return (
    <nav
      aria-label={t("label")}
      className="flex w-full flex-wrap items-center justify-between gap-4"
    >
      <LanguageSwitcher onChange={onLanguageChange} />
      <a
        href={privacyUrl}
        target="_blank"
        rel="noreferrer"
        className="rounded-sm text-small text-muted-foreground underline-offset-4 outline-none hover:text-foreground hover:underline focus-visible:ring-2 focus-visible:ring-ring"
      >
        {t("privacy")}
      </a>
    </nav>
  );
}
