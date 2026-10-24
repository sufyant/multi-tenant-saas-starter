"use client";

import { Language } from "@repo/api-client";
import { Combobox } from "@repo/ui/components/combobox";
import { directionOf } from "@repo/ui/lib/locale";
import { useTranslations } from "next-intl";

const languages = Object.values(Language);

/** The language of an invitation's email (K8): each option in its own language and direction. */
export function LanguageChoice({
  label,
  value,
  onChange,
}: {
  label: string;
  value: Language;
  onChange: (language: Language) => void;
}) {
  const t = useTranslations();
  return (
    <Combobox
      label={label}
      value={value}
      options={languages.map((language) => ({
        id: language,
        label: t(`languages.${language}`),
        lang: language,
        dir: directionOf(language),
      }))}
      onValueChange={(id) => {
        const language = languages.find((choice) => choice === id);
        if (language) onChange(language);
      }}
      searchPlaceholder={t("organisations.languageSearch")}
      emptyLabel={t("organisations.languageEmpty")}
    />
  );
}
