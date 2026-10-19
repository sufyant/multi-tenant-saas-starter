"use client";

import type { Language } from "@repo/api-client";
import { useLocale, useTranslations } from "next-intl";
import { type Direction, directions } from "../../lib/locale";
import { Combobox } from "../combobox";
import { GlobeIcon } from "../icon";

export type LanguageSwitcherProps = {
  /** Sets the locale cookie of F13: the app's `setLocale` server action. The page then loads again in the language. */
  onChange: (language: Language) => Promise<void>;
};

/** The language before sign-in (the PhoneSignInAr board): each language in its own name and script. */
export function LanguageSwitcher({ onChange }: LanguageSwitcherProps) {
  const t = useTranslations();
  const locale = useLocale();
  const languages = Object.entries(directions) as [Language, Direction][];
  return (
    <Combobox
      variant="compact"
      icon={GlobeIcon}
      label={t("signIn.language")}
      searchPlaceholder={t("signIn.languageSearch")}
      emptyLabel={t("signIn.languageEmpty")}
      value={locale}
      options={languages.map(([id, dir]) => ({
        id,
        label: t(`languages.${id}`),
        lang: id,
        dir,
      }))}
      onValueChange={(language) =>
        // The ids are the options above, so each one is a Language.
        void onChange(language as Language).then(() => window.location.reload())
      }
    />
  );
}
