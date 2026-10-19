"use client";

import { useLocale, useTranslations } from "next-intl";
import { type Direction, directions } from "../../lib/locale";
import { Combobox } from "../combobox";
import { GlobeIcon } from "../icon";

export type LanguageSwitcherProps = {
  /** Sets the locale cookie of F13: the app's `setLocale` server action. The page then loads again in the language. */
  onChange: (language: string) => Promise<void>;
};

/** The language before sign-in (the PhoneSignInAr board): each language in its own name and script. */
export function LanguageSwitcher({ onChange }: LanguageSwitcherProps) {
  const t = useTranslations();
  const locale = useLocale();
  const languages = Object.entries(directions) as [string, Direction][];
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
        label: t(`languages.${id}` as "languages.en"),
        lang: id,
        dir,
      }))}
      onValueChange={(language) =>
        void onChange(language).then(() => window.location.reload())
      }
    />
  );
}
