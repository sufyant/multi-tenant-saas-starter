"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";

/** A list names at most three, then "and n more" (the Messages board, F15). */
const named = 3;

/**
 * The organisations a dialog names in its sentence (5.10, 5.11, 5.13, 5.14): "A, B and C", then "and n more". Each name is isolated
 * from the sentence around it; the list's words stay text. A message puts the stop that follows the list in `<stop>`, which drops
 * it when the list ends with one already ("… Cooperative Ltd.", 5.11).
 */
export function useOrganisationList() {
  const t = useTranslations("users");
  const locale = useTranslations("locale")("format");
  return (names: string[]) => ({
    list: () => listOf(names),
    stop: (chunks: ReactNode) =>
      names.length <= named && names.at(-1)?.endsWith(".") ? null : chunks,
  });

  function listOf(names: string[]) {
    const more = names.length - named;
    return new Intl.ListFormat(locale, { type: "conjunction" })
      .formatToParts(
        more > 0
          ? [...names.slice(0, named), t("moreOrganisations", { count: more })]
          : names,
      )
      .map((part, index) =>
        part.type === "element" && names.includes(part.value) ? (
          // Two organisations may share a name; a part's place in the one formatted list never changes.
          // biome-ignore lint/suspicious/noArrayIndexKey: the parts of a formatted list have no id of their own.
          <bdi key={index}>{part.value}</bdi>
        ) : (
          part.value
        ),
      );
  }
}
