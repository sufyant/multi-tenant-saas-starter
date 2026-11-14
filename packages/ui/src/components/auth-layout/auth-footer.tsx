"use client";

import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { LanguageSwitcher } from "../sign-in/language-switcher";

export type AuthFooterProps = {
  /** Sets the locale cookie of F13: the app's `setLocale` server action. */
  onLanguageChange: (language: Language) => Promise<void>;
  /** The brand's privacy policy. */
  privacyUrl: string;
  /** The link of the terms version in force, or undefined before the first version, when "Terms" stays hidden. */
  termsUrl?: string;
};

const linkClass =
  "rounded-sm text-small text-link underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring";

/**
 * The footer of every screen before sign-in, in both apps (the Auth footer): the language switcher at the start, then "Terms ·
 * Privacy" at the end. "Terms" opens the version in force, which the app reads from the public endpoint; "Privacy" opens the brand's
 * privacy policy. It mirrors in right-to-left.
 */
export function AuthFooter({
  onLanguageChange,
  privacyUrl,
  termsUrl,
}: AuthFooterProps) {
  const t = useTranslations("appShell.authFooter");
  return (
    <nav
      aria-label={t("label")}
      className="flex w-full flex-wrap items-center justify-between gap-4"
    >
      <LanguageSwitcher onChange={onLanguageChange} />
      <span className="flex items-center gap-1.5 text-small text-muted-foreground">
        {termsUrl && (
          <>
            <a
              href={termsUrl}
              target="_blank"
              rel="noreferrer"
              className={linkClass}
            >
              {t("terms")}
            </a>
            <span aria-hidden="true">·</span>
          </>
        )}
        <a
          href={privacyUrl}
          target="_blank"
          rel="noreferrer"
          className={linkClass}
        >
          {t("privacy")}
        </a>
      </span>
    </nav>
  );
}
