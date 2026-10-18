import type { Language } from "@repo/api-client";

// The language of a request (F13): the cookie that the app sets from GetMe.language after sign-in, else the best match of the
// browser's Accept-Language, else the fallback. The app passes the languages of the contract (`Object.values(Language)`).

/** The cookie that holds the user's language. */
export const localeCookie = "NEXT_LOCALE";

export type Direction = "ltr" | "rtl";

/** The language of a request, from the cookie, then Accept-Language, then the fallback. */
export function chooseLocale<L extends string>(
  cookie: string | undefined,
  acceptLanguage: string | null,
  supported: readonly L[],
  fallback: L,
): L {
  const known = (tag: string | undefined) =>
    supported.find(
      (language) => language === tag?.trim().split("-")[0]?.toLowerCase(),
    );
  const fromCookie = known(cookie);
  if (fromCookie) return fromCookie;
  const ranked = (acceptLanguage ?? "")
    .split(",")
    .map((part) => {
      const [tag, ...parameters] = part.split(";");
      const quality = parameters.find((parameter) =>
        parameter.trim().startsWith("q="),
      );
      return { tag, quality: quality ? Number(quality.trim().slice(2)) : 1 };
    })
    .filter((entry) => entry.quality > 0)
    .sort((a, b) => b.quality - a.quality);
  for (const { tag } of ranked) {
    const match = known(tag);
    if (match) return match;
  }
  return fallback;
}

/**
 * The writing direction of each language of the contract (W3C, "Structural markup and right-to-left text in HTML"). It is a fact
 * of ours, written once: `Record<Language, Direction>` makes a new language of the contract fail the type check until it has
 * its entry. The Intl API's `getTextInfo` is not in every browser or Node, so the direction never comes from there.
 */
export const directions: Record<Language, Direction> = {
  en: "ltr",
  tr: "ltr",
  ar: "rtl",
};

/** The writing direction of a language: `rtl` for Arabic, `ltr` for the others and for a tag the contract does not list. */
export function directionOf(language: string): Direction {
  return directions[language as Language] ?? "ltr";
}
