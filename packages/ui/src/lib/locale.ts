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

/** The writing direction of a language, from Unicode's data in the Intl API: `ar` is right to left. */
export function directionOf(language: string): Direction {
  const locale = new Intl.Locale(language) as Intl.Locale & {
    getTextInfo?: () => { direction: Direction };
  };
  return locale.getTextInfo?.().direction ?? "ltr";
}
